using System.Text;
using System.Text.Json;
using Estoque.API.Models;
using Estoque.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Estoque.API.Services
{
    // O IHostedService é o padrão .NET para serviços de background que rodam continuamente.
    public class RabbitMQConsumer : BackgroundService
    {
        private IConnection? _connection;
        private IModel? _channel;

        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly IServiceProvider _serviceProvider; 

        private const string EXCHANGE_NAME = "vendas_exchange"; 
        private const string QUEUE_NAME = "estoque-queue";
        private const string ROUTING_KEY = "vendas.pedido.criado"; // Usado para binding

        public RabbitMQConsumer(
            IConfiguration configuration,
            ILogger<RabbitMQConsumer> logger,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            try
            {
                // 1. Conexão
                // Acessa a ConnectionString do appsettings.json
                string connectionString = _configuration.GetValue<string>("RabbitMQ:ConnectionString") ?? throw new InvalidOperationException("RabbitMQ:ConnectionString não configurada.");
                var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // 2. Declaração da Exchange (Fanout)
                // Usa EXCHANGE_NAME = "vendas_exchange"
                _channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: "fanout", durable: true);

                // 3. Declaração da Fila e Binding
                _channel.QueueDeclare(queue: QUEUE_NAME, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.QueueBind(queue: QUEUE_NAME, exchange: EXCHANGE_NAME, routingKey: ROUTING_KEY); // Binding à exchange

                _logger.LogInformation("Conexão e fila RabbitMQ configuradas com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERRO: Não foi possível conectar ao RabbitMQ. Certifique-se de que o serviço está rodando.");
            }
        }

        // Método principal do BackgroundService
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            if (_channel == null)
            {
                 _logger.LogWarning("Não é possível iniciar o consumidor, canal RabbitMQ é nulo.");
                 return Task.CompletedTask;
            }

            // Cria um consumidor de eventos (BasicConsumer)
            var consumer = new EventingBasicConsumer(_channel);

            // Define o evento que será disparado ao receber uma mensagem
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                _logger.LogDebug($"[RabbitMQ] Mensagem recebida: {message}");
                
                try
                {
                    // Desserializa a mensagem JSON para o DTO
                    var pedidoCriadoMessage = JsonSerializer.Deserialize<PedidoCriadoMessage>(message);
                    
                    if (pedidoCriadoMessage != null)
                    {
                        // IMPORTANTE: Criamos um escopo para que o EstoqueService e o DbContext
                        // sejam injetados para esta requisição/mensagem.
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var estoqueService = scope.ServiceProvider.GetRequiredService<IEstoqueService>();
                            await estoqueService.ProcessarBaixaEstoqueAsync(pedidoCriadoMessage);
                        }

                        // Confirma o processamento da mensagem (Ack)
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao desserializar a mensagem JSON.");
                    // Nack e rejeita permanentemente se for um erro de formato (não tentar de novo)
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro durante o processamento da baixa de estoque.");
                    // Nack e coloca de volta na fila para nova tentativa
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            // Inicia o consumo da fila
            _channel.BasicConsume(queue: QUEUE_NAME, autoAck: false, consumer: consumer);
            
            return Task.CompletedTask;
        }

        // Limpeza dos recursos do RabbitMQ quando o serviço é parado
        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}