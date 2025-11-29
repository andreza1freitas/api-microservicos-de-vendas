using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Events
{
    public class PedidoCriadoEvent : IEvent
    {
        public Guid PedidoId { get; set; }
        public DateTime DataPedido { get; set; }
        public List<ItemPedidoEvent> Items { get; set; } = new List<ItemPedidoEvent>();
    }
}