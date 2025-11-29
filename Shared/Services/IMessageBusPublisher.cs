using Shared.Events;

namespace Shared.Services
{
    // Interface para publicação de eventos no barramento de mensagens.
    public interface IMessageBusPublisher
    {
        void PublishEvent<T>(T message) where T : IEvent;

        void PublishEvent<T>(T message, string exchange, string routingKey) where T : IEvent;
    }
}