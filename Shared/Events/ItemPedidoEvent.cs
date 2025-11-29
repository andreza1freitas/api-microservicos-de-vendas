using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Events
{
    public class ItemPedidoEvent : IEvent
    {
        public Guid PedidoId { get; set; } 
        public int ProdutoId { get; set; } 
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
    }
}