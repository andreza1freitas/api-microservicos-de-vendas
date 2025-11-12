using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vendas.API.Services
{
        // Interface para o serviço de publicação de mensagens RabbitMQ
        public interface IRabbitMQPublisher
        {
            void Publish(object message, string exchange, string routingKey);
        }
}