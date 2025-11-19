using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Events
{
    public class ItemPedidoEvent
    {
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
    }
}