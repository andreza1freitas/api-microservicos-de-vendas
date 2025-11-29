using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Events;

namespace Shared.Services
{
    // Interface para consumidores do barramento de mensagens.
    public interface IMessageBusConsumer
    {
        void ConnectToExchange(string exchangeName, string exchangeType);
        void Subscribe<T>(
                   string routingKey,
                   string queueName,
                   Action<T> onMessageReceived
               ) where T : IEvent;

    }
}