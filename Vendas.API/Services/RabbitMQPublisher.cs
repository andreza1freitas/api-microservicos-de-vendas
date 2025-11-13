using RabbitMQ.Client;
using Shared.Events;
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
        private const string EXCHANGE_NAME = "vendas_exchange"; // Nome da Exchange

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

        private void InitializeRabbitMQ()
        {
            try
            {
                // Lendo a string de conexão completa (e.g., amqp://guest:guest@localhost:5672)
                string connectionString = _configuration.GetValue<string>("RabbitMQ:ConnectionString")
                    ?? throw new InvalidOperationException("RabbitMQ:ConnectionString não configurada.");

                var factory = new ConnectionFactory()
                {
                    Uri = new Uri(connectionString)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declara a Exchange do tipo 'fanout' para distribuir a mensagem
                _channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: ExchangeType.Fanout, durable: true);

                Console.WriteLine("--> Conexão com RabbitMQ estabelecida e Exchange declarada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> ERRO: Não foi possível conectar ao Message Bus: {ex.Message}");
            }
        }

        // NOVO método de publicação, conforme a interface IMessageBusPublisher
        public void PublishEvent(PedidoCriadoEvent evento)
        {
            // Verifica a conexão antes de publicar
            if (_connection == null || !_connection.IsOpen || _channel == null)
            {
                Console.WriteLine("--> Conexão com RabbitMQ fechada ou nula. Não publicou. Tentando restabelecer...");
                InitializeRabbitMQ();

                if (_connection == null || !_connection.IsOpen || _channel == null)
                {
                    Console.WriteLine("--> Falha ao restabelecer a conexão. Abortando publicação.");
                    return;
                }
            }

            var message = JsonSerializer.Serialize(evento);
            var body = Encoding.UTF8.GetBytes(message);

            // Publica a mensagem na Exchange
            _channel!.BasicPublish(
                exchange: EXCHANGE_NAME,
                routingKey: "",
                basicProperties: null,
                body: body
            );
            Console.WriteLine($"--> Pedido Publicado no Message Bus: Pedido ID {evento.PedidoId}");
        }

        public void Dispose()
        {
            // Libera recursos
            _channel?.Close();
            _connection?.Close();
        }
    }
}