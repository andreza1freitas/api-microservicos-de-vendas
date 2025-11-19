using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Estoque.API.Messaging
{
    public class RabbitMQSettings
    {
        // Classe para mapear as configurações do RabbitMQ do appsettings.json
        public string HostName { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
        // Nova propriedade para a Dead Letter Exchange
        public string DeadLetterExchange { get; set; } = string.Empty;
    }
}