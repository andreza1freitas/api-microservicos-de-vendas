using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared
{
    public class CriarPedidoRequest
    {
        public int ClienteId { get; set; }
        public List<ItemPedidoRequest>? Itens { get; set; }
    }

    public class ItemPedidoRequest
    {
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; } // Valor que o Vendas registra
    }
}