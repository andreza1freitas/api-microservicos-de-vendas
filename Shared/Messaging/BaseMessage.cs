using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Enums;

namespace Shared.Messaging
{
    public class BaseMessage
    {
        public Guid PedidoId { get; set; }
        public MessageType TipoMensagem { get; set; }
    }
}