using Estoque.API.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging;
using System.Text;
using System.Text.Json;
using Shared.Enums;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Estoque.API.Messaging
{
    public class EstoqueMessageHandler : BackgroundService
    {
        private readonly ILogger<EstoqueMessageHandler> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;

        // Constantes da Mensageria
        private const string QueueName = "estoque-baixa";
        private const string ExchangeName = "estoque-exchange";
        private const string DlqExchangeName = "estoque-dlx";
        private const string DlqQueueName = "estoque-dlq";

        public EstoqueMessageHandler(
            ILogger<EstoqueMessageHandler> logger,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            _logger.LogInformation("[EstoqueMessageHandler] ========== INICIANDO CONSUMER NA FILA '{QueueName}' ==========", QueueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                IConnection? connection = null;
                IModel? channel = null;

                try
                {
                    // Cria uma nova conexão a cada tentativa
                    var factory = new ConnectionFactory()
                    {
                        HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                        DispatchConsumersAsync = true
                    };

                    connection = factory.CreateConnection();
                    channel = connection.CreateModel();

                    _logger.LogInformation("[EstoqueMessageHandler] Conexão com RabbitMQ estabelecida.");

                    // Configuração do RabbitMQ
                    ConfigureRabbitMQ(channel);

                    _logger.LogInformation("[EstoqueMessageHandler] Configuração do RabbitMQ concluída. Aguardando mensagens...");

                    // Registra o consumer
                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += (model, ea) => HandleMessage(ea, channel);

                    channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

                    _logger.LogInformation("[EstoqueMessageHandler] ========== CONSUMER INICIADO E AGUARDANDO MENSAGENS NA FILA '{QueueName}' ==========", QueueName);

                    // Mantém a conexão aberta até ser cancelado
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("[EstoqueMessageHandler] Operação cancelada. Encerrando Consumer...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[EstoqueMessageHandler] Erro no Consumer. Tentando reconectar em 10 segundos...");
                    
                    try
                    {
                        await Task.Delay(10000, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    // Limpa recursos
                    channel?.Close();
                    channel?.Dispose();
                    connection?.Close();
                    connection?.Dispose();
                }
            }

            _logger.LogInformation("[EstoqueMessageHandler] Consumer encerrado.");
        }

        private void ConfigureRabbitMQ(IModel channel)
        {
            // Declaração do Exchange principal e DLX
            channel.ExchangeDeclare(exchange: ExchangeName, type: "topic", durable: true);
            channel.ExchangeDeclare(exchange: DlqExchangeName, type: "fanout", durable: true);

            // Configuração da DLQ e DLX
            var dlqArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", ExchangeName },
                { "x-message-ttl", 30000 } // Tenta reprocessar após 30 segundos
            };
            channel.QueueDeclare(queue: DlqQueueName, durable: true, exclusive: false, autoDelete: false, arguments: dlqArgs);
            channel.QueueBind(queue: DlqQueueName, exchange: DlqExchangeName, routingKey: "");
            _logger.LogInformation("DLQ e DLX configuradas.");

            // Configuração da fila principal com Dead Letter Exchange
            var queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", DlqExchangeName }
            };
            channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);

            // Binds para a fila principal
            channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "baixa-estoque");
            channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: "estorno-estoque");

            _logger.LogInformation("Fila principal '{QueueName}' configurada para baixa-estoque e estorno-estoque.", QueueName);

            // Define o QoS para limitar o número de mensagens não confirmadas
            channel.BasicQos(0, 1, false);
        }

        private async Task HandleMessage(BasicDeliverEventArgs ea, IModel channel)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation("[EstoqueMessageHandler] *** MENSAGEM RECEBIDA DA FILA *** RoutingKey: {RoutingKey}", routingKey);
            _logger.LogInformation("[EstoqueMessageHandler] Payload: {Message}", message);

            Guid pedidoIdParaLog = Guid.Empty;
            BaseMessage? baseMessage = null;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var estoqueService = scope.ServiceProvider.GetRequiredService<IEstoqueService>();

                // Desserializa a BaseMessage para obter o tipo
                baseMessage = JsonSerializer.Deserialize<BaseMessage>(message, EstoqueService.JsonSerializerOptions);

                if (baseMessage == null)
                {
                    throw new JsonException("Não foi possível desserializar a BaseMessage.");
                }

                pedidoIdParaLog = baseMessage.PedidoId;
                _logger.LogInformation("[Mensageria] Mensagem recebida. Routing Key: {RoutingKey}. Pedido ID (Base): {PedidoId}. Tipo: {Tipo}.", routingKey, pedidoIdParaLog, baseMessage.TipoMensagem);

                // Lógica de roteamento baseada na Routing Key ou Tipo
                if (routingKey.Equals("baixa-estoque", StringComparison.OrdinalIgnoreCase) || baseMessage.TipoMensagem == MessageType.BaixaEstoque)
                {
                    var baixaMessage = JsonSerializer.Deserialize<BaixaEstoqueMessage>(message, EstoqueService.JsonSerializerOptions);
                    if (baixaMessage != null)
                    {
                        pedidoIdParaLog = baixaMessage.PedidoId;
                        await estoqueService.BaixarEstoque(baixaMessage);
                        _logger.LogInformation("[Mensageria] Pedido ID {PedidoId} (Baixa) processado.", pedidoIdParaLog);
                    }
                }
                // TRATAMENTO DO CANCELAMENTO: Usa EstornoEstoqueMessage
                else if (routingKey.Equals("estorno-estoque", StringComparison.OrdinalIgnoreCase) || baseMessage.TipoMensagem == MessageType.EstornoEstoque)
                {
                    var estornoMessage = JsonSerializer.Deserialize<EstornoEstoqueMessage>(message, EstoqueService.JsonSerializerOptions);
                    if (estornoMessage != null)
                    {
                        pedidoIdParaLog = estornoMessage.PedidoId;
                        await estoqueService.EstornarEstoque(estornoMessage);
                        _logger.LogInformation("[Mensageria] Pedido ID {PedidoId} (Estorno) processado.", pedidoIdParaLog);
                    }
                }
                else
                {
                    _logger.LogWarning("[Mensageria] Mensagem recebida com Routing Key '{RoutingKey}' e Tipo '{Tipo}' desconhecidos. Descartando (ACK).", routingKey, baseMessage.TipoMensagem);
                }

                channel.BasicAck(ea.DeliveryTag, false);
                _logger.LogInformation("[Mensageria] ACK enviado para Pedido ID {PedidoId}.", pedidoIdParaLog);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[Mensageria] Erro de desserialização da mensagem. Mensagem: {Message}. Enviando para DLQ.", message);
                channel.BasicNack(ea.DeliveryTag, false, false);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Estoque insuficiente"))
            {
                _logger.LogWarning("[Mensageria] Falha de Negócio para Pedido ID {PedidoId}: {Message}. Confirmando mensagem (ACK).", pedidoIdParaLog, ex.Message);
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                var finalPedidoId = baseMessage?.PedidoId ?? Guid.Empty;
                _logger.LogError(ex, "[Mensageria] Erro ao processar mensagem do Pedido ID {PedidoId}. Mensagem: {Message}. Enviando para DLQ.", finalPedidoId, message);
                channel.BasicNack(ea.DeliveryTag, false, false);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}