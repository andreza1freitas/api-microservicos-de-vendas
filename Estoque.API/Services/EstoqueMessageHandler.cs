using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting; 
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.API.Services
{
    public class EstoqueMessageHandler : BackgroundService
    {
        private readonly IModel _channel;
        private readonly IConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EstoqueMessageHandler> _logger;
        private const string MainQueueName = "estoque-baixa";
        private const string DlqExchangeName = "estoque-dlx";
        private const string DlqQueueName = "estoque-baixa-dlq";
        private const string EstoqueExchangeName = "estoque-exchange";

        public EstoqueMessageHandler(
            IConnection connection, 
            IServiceScopeFactory scopeFactory, 
            ILogger<EstoqueMessageHandler> logger)
        {
            _connection = connection;
            _scopeFactory = scopeFactory;
            _logger = logger;
            
            // Criação do canal e configuração do RabbitMQ (DLQ)
            _channel = _connection.CreateModel();
            SetupRabbitMqTopology(_channel);
        }

        private void SetupRabbitMqTopology(IModel channel)
        {
            // Cria a DLQ (Fila de Cartas Mortas) e a Exchange DLX
            channel.ExchangeDeclare(DlqExchangeName, ExchangeType.Fanout);
            channel.QueueDeclare(DlqQueueName, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(DlqQueueName, DlqExchangeName, routingKey: "");
            
            _logger.LogInformation("DLQ e DLX configuradas.");

            // Cria a fila principal com a DLX configurada
            var arguments = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", DlqExchangeName },
                { "x-message-ttl", 60000 } 
            };

            // Garante que a Exchange principal existe (criada pelo EstoqueService na publicação)
            channel.ExchangeDeclare(EstoqueExchangeName, ExchangeType.Direct);
            
            // Note: O EstoqueService declara a Exchange como 'topic', vamos garantir a consistência
            // Mantenho o 'Direct' aqui por enquanto, mas se for Topic, precisa ser Topic nos dois lados.
            
            channel.QueueDeclare(MainQueueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);

            // Liga a fila principal à Exchange
            channel.QueueBind(MainQueueName, EstoqueExchangeName, routingKey: "baixa-estoque");
            
            _logger.LogInformation($"Fila principal '{MainQueueName}' configurada com DLQ.");
            
            // Configura o prefetch para limitar o número de mensagens não processadas que o consumidor recebe
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        // Lógica principal de processamento de mensagens
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            
            consumer.Received += async (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation($"[Mensageria] Mensagem recebida: {content}");
                
                // Variável para armazenar a mensagem desserializada, acessível após o bloco try/catch
                BaixaEstoqueMessage? message = null;

                try
                {
                    // Tenta desserializar a mensagem
                    message = JsonSerializer.Deserialize<BaixaEstoqueMessage>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (message == null)
                    {
                        _logger.LogError("[Mensageria] Falha ao desserializar a mensagem. Enviando NACK para DLQ.");
                        _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    // Cria um escopo para resolver serviços com tempo de vida 'Scoped' (como DbContext)
                    using var scope = _scopeFactory.CreateScope();
                    var estoqueService = scope.ServiceProvider.GetRequiredService<IEstoqueService>();

                    // Tenta baixar o estoque
                    await estoqueService.BaixarEstoque(message);

                    // Confirma o processamento
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation($"[Mensageria] Pedido ID {message.PedidoId} processado e ACK enviado.");
                }
                catch (InvalidOperationException ex)
                {
                    // Erro de Negócio (ex: estoque insuficiente). A mensagem não deve ser re-tentada.
                    _logger.LogError(ex, $"[Mensageria] Erro de Negócio (permanente) ao processar Pedido ID {message?.PedidoId}: {ex.Message}");
                    // NACK sem requeue, para a mensagem ir para a DLQ
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
                // Captura o erro de Persistência (DbUpdateException) envolvido por ApplicationException
                catch (Exception ex) when (ex.InnerException is DbUpdateException && message != null)
                {
                    // Erro de Persistência (temporário). A mensagem deve ser republicada.
                    _logger.LogWarning(ex, $"[Mensageria] Erro de Persistência (temporário) detectado para Pedido ID {message.PedidoId}. Re-publicando...");
                    
                    // Cria um novo escopo para a republição
                    using var republishScope = _scopeFactory.CreateScope();
                    var estoqueService = republishScope.ServiceProvider.GetRequiredService<IEstoqueService>();
                    
                    // Usa a variável 'message' já desserializada
                    await estoqueService.RepublishMessage(message);
                    
                    // Confirma que a mensagem original foi recebida e tratada (republicada)
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation($"[Mensageria] Pedido ID {message.PedidoId} republicado e ACK enviado.");

                }
                catch (Exception ex)
                {
                    // Erros Inesperados ou desserialização falha (message é null)
                    string pid = message != null ? message.PedidoId.ToString() : "N/A";
                    _logger.LogError(ex, $"[Mensageria] Erro inesperado ao processar a mensagem para Pedido ID {pid}. Enviando NACK para DLQ.");
                    // NACK sem requeue, para a mensagem ir para a DLQ
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            // BasicConsume usando o consumidor assíncrono (AsyncEventingBasicConsumer)
            _channel.BasicConsume(queue: MainQueueName, autoAck: false, consumer: consumer);

            // Garante que o serviço fica rodando em segundo plano
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}