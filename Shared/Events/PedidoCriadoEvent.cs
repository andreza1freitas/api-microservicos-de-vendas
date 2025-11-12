using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Events
{
    public class PedidoCriadoEvent
    {
        public int PedidoId { get; set; }
        public DateTime DataPedido { get; set; }
        public List<ItemPedidoEvent> Itens { get; set; } = new List<ItemPedidoEvent>();
    }

    public class ItemPedidoEvent
    {
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
    }
}