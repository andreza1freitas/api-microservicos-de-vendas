using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vendas.API.Models
{
    public class PedidoCriadoMessage
    {
        public int PedidoId { get; set; }
        public List<PedidoItemDto>? Itens { get; set; }
    }

    public record PedidoItemDto(int ProdutoId, int Quantidade);
}