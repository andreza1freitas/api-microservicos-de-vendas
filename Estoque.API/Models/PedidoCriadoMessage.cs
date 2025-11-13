using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Estoque.API.Models
{
    public class PedidoCriadoMessage
    {
        public int PedidoId { get; set; }
        public List<PedidoItemDto> Itens { get; set; } = new List<PedidoItemDto>();
    }

    // DTO para os itens do pedido
    public record PedidoItemDto(int ProdutoId, int Quantidade);
}