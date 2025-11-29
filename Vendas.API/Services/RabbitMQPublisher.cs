using RabbitMQ.Client;
using Shared.Events;
using Shared.Services;
using System.Text;
using System.Text.Json;

namespace Vendas.API.Services
{
    // Implementa IMessageBusPublisher e IDisposable
    public class RabbitMQPublisher : IMessageBusPublisher, IDisposable
    {
        private readonly IConfiguration _configuration;
        private IConnection? _connection;
        private IModel? _channel;

        // Constantes para comunicação com o Estoque.API
        private const string ESTOQUE_EXCHANGE = "estoque-exchange";
        private const string BAIXA_ESTOQUE_ROUTING_KEY = "baixa-estoque";
        
        // Constante para a exchange de status da própria Vendas (para consumo por outros serviços)
        private const string VENDAS_STATUS_EXCHANGE = "vendas-status-exchange";

        public RabbitMQPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
            InitializeRabbitMQ();

            if (_connection != null)
            {
                _connection.ConnectionShutdown += (sender, reason) =>
                {
                    Console.WriteLine("--> Conexão com RabbitMQ interrompida. Tentando reconectar...");
                };
            }
        }

        // Inicializa a conexão com o RabbitMQ
        private void InitializeRabbitMQ()
        {
            try
            {
                string connectionString = _configuration.GetValue<string>("RabbitMQ:ConnectionString")
                    ?? throw new InvalidOperationException("RabbitMQ:ConnectionString não configurada.");

                var factory = new ConnectionFactory()
                {
                    Uri = new Uri(connectionString)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declara a exchange do Estoque (garante que ela existe e é do tipo Topic)
                _channel.ExchangeDeclare(exchange: ESTOQUE_EXCHANGE, type: ExchangeType.Topic, durable: true);
                
                // Declara a exchange de Vendas
                _channel.ExchangeDeclare(exchange: VENDAS_STATUS_EXCHANGE, type: ExchangeType.Topic, durable: true);

                Console.WriteLine("--> Conexão com RabbitMQ estabelecida e Exchanges declaradas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> ERRO: Não foi possível conectar ao Message Bus: {ex.Message}");
            }
        }

        // Obtém a Exchange e a Routing Key correta para cada tipo de evento.
        private (string Exchange, string RoutingKey) GetRoutingDetails<T>() where T : IEvent
        {
            var eventName = typeof(T).Name;

            if (eventName == nameof(PedidoCriadoEvent))
            {
                // A mensagem vai para a Exchange do Estoque com a chave que a fila está ligada.
                return (ESTOQUE_EXCHANGE, BAIXA_ESTOQUE_ROUTING_KEY);
            }
            // Roteamento para eventos de atualização de status
            else if (eventName == nameof(PedidoStatusAtualizadoEvent))
            {
                // Roteia para a exchange de Vendas/Status.
                return (VENDAS_STATUS_EXCHANGE, eventName);
            }
            
            return ("default-exchange", eventName);
        }

        // Implementação da sobrecarga de 1 argumento (usa o GetRoutingDetails)
        public void PublishEvent<T>(T message) where T : IEvent
        {
            if (_connection == null || !_connection.IsOpen || _channel == null)
            {
                Console.WriteLine("--> Conexão com RabbitMQ não está pronta. Abortando publicação.");
                return;
            }

            // Obtém o destino correto
            var (exchangeName, routingKey) = GetRoutingDetails<T>();
            
            PublishInternal(message, exchangeName, routingKey);
        }

        public void PublishEvent<T>(T message, string exchange, string routingKey) where T : IEvent
        {
             if (_connection == null || !_connection.IsOpen || _channel == null)
            {
                Console.WriteLine("--> Conexão com RabbitMQ não está pronta. Abortando publicação.");
                return;
            }

            PublishInternal(message, exchange, routingKey);
        }

        // Método auxiliar para publicação
        private void PublishInternal<T>(T message, string exchangeName, string routingKey) where T : IEvent
        {
            // Serializa a mensagem
            var messageJson = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageJson);

            // Publica a mensagem no destino
            _channel.BasicPublish(
                exchange: exchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: body
            );
            Console.WriteLine($"--> Evento do tipo '{typeof(T).Name}' Publicado. Destino: {exchangeName}/{routingKey}");
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}