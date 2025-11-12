// Vendas.API/Services/RabbitMQPublisher.cs

using RabbitMQ.Client;
using Shared.Events;
using System.Text;
using System.Text.Json;

namespace Vendas.API.Services
{
    public class RabbitMQPublisher : IMessageBusPublisher
    {
        private readonly IConfiguration _configuration;
        private IConnection _connection;
        private IModel _channel;
        private const string EXCHANGE_NAME = "vendas_exchange"; // Nome da Exchange

        public RabbitMQPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            try
            {
                var factory = new ConnectionFactory() 
                { 
                    HostName = _configuration["RabbitMQ:Host"],
                    Port = int.Parse(_configuration["RabbitMQ:Port"])
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declara a Exchange do tipo 'fanout' para distribuir a mensagem (simples)
                _channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: ExchangeType.Fanout);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> Não foi possível conectar ao Message Bus: {ex.Message}");
            }
        }

        public void PublishPedidoCriado(PedidoCriadoEvent evento)
        {
            var message = JsonSerializer.Serialize(evento);
            var body = Encoding.UTF8.GetBytes(message);

            if (_connection != null && _connection.IsOpen)
            {
                _channel.BasicPublish(
                    exchange: EXCHANGE_NAME,
                    routingKey: "", // Fanout não usa routing key
                    basicProperties: null,
                    body: body
                );
                Console.WriteLine($"--> Pedido Publicado no Message Bus: Pedido ID {evento.PedidoId}");
            }
            else
            {
                Console.WriteLine("--> Conexão com RabbitMQ fechada ou nula. Não publicou.");
            }
        }

        public void Dispose()
        {
            if (_channel.IsOpen)
            {
                _channel.Close();
                _connection.Close();
            }
        }
    }
}