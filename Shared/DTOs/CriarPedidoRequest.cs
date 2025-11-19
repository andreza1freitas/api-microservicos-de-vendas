using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.DTOs;

namespace Shared
{
    public class CriarPedidoRequest
    {
        public int ClienteId { get; set; }
        public List<ItemPedidoRequest>? Itens { get; set; }
    }
}