using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Configuration;
using Shared.Events;
using System.Text;
using System.Text.Json;
using Shared.Messaging;

namespace Shared.Services
{
    public class MessageBusService : IMessageBusPublisher, IMessageBusConsumer
    {
        // Configuração e conexão com o RabbitMQ
        private readonly IConfiguration _configuration;
        private readonly ConnectionFactory _factory;
        private IConnection? _connection;
        private IModel? _channel;

       // Construtor que inicializa a fábrica de conexões
        public MessageBusService(IConfiguration configuration)
        {
            _configuration = configuration;

            _factory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ:HostName"],
                Port = _configuration.GetValue<int>("RabbitMQ:Port", 5672),
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"]
            };
        }

        // Garante que a conexão esteja aberta
        private void EnsureConnection()
        {
            if (_connection == null || !_connection.IsOpen)
            {
                _connection = _factory.CreateConnection();
                _channel = _connection.CreateModel();
            }
        }

       // Publica um evento no barramento de mensagens
        public void PublishEvent<T>(T message) where T : IEvent
        {
            EnsureConnection();

            var exchangeName = "vendas-exchange";
            var routingKey = message.GetType().Name.Replace("Event", "");

            _channel!.ExchangeDeclare(exchange: exchangeName, type: "topic", durable: true);

            var body = JsonSerializer.Serialize(message);
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            _channel.BasicPublish(
                exchange: exchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: bodyBytes
            );

            Console.WriteLine($"[RabbitMQ] Evento '{message.GetType().Name}' publicado com chave '{routingKey}'.");
        }

        // Conecta a uma exchange específica
        public void ConnectToExchange(string exchangeName, string exchangeType)
        {
            EnsureConnection();
            _channel!.ExchangeDeclare(exchange: exchangeName, type: exchangeType, durable: true);
        }

       // Inscreve-se para consumir eventos de uma fila específica
        public void Subscribe<T>(string routingKey, string queueName, Action<T> onMessageReceived)
            where T : IEvent
        {
            EnsureConnection();

            var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not available.");

            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(queue: queueName, exchange: "vendas-exchange", routingKey: routingKey);

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (model, ea) =>
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                try
                {
                    var evento = JsonSerializer.Deserialize<T>(json);
                    if (evento != null)
                    {
                        onMessageReceived(evento);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RabbitMQ][ERRO] Falha ao processar mensagem: {ex.Message}");
                }

                channel.BasicAck(ea.DeliveryTag, false);
            };

            channel.BasicConsume(queueName, autoAck: false, consumer);
        }

       // Publica um evento no barramento de mensagens para uma exchange e routing key específicas
        public void PublishEvent<T>(T message, string exchange, string routingKey) where T : IEvent
        {
            try
            {
                EnsureConnection();

                _channel!.ExchangeDeclare(exchange: exchange, type: "topic", durable: true);

                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                _channel.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body
                );

                Console.WriteLine($"[RabbitMQ] Mensagem do tipo '{typeof(T).Name}' publicada em {exchange}/{routingKey}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RabbitMQ] ERRO ao publicar mensagem em {exchange}/{routingKey}: {ex.Message}");
                throw;
            }
        }
    }
}
