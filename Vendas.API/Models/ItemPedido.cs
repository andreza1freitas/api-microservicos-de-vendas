using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vendas.API.Models
{
    public class ItemPedido
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public int ProdutoId { get; set; } // Referência ao ID do Produto no Estoque.API
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }

        // Propriedade de navegação para Pedido
        public Pedido? Pedido { get; set; }
    }
}