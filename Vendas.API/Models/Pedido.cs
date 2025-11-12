using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vendas.API.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;
        public decimal ValorTotal { get; set; }
        public string Status { get; set; } = "Pendente"; // Pendente, Processando, Enviado, Cancelado
        
        // Relacionamento 1:N com ItemPedido
        public ICollection<ItemPedido>? Itens { get; set; }
    }
}