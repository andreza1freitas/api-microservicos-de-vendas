using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Vendas.API.Enums;

namespace Vendas.API.Models
{
    public class Pedido
    {
        [Key]
        public Guid Id { get; set; }
        
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;
        public decimal ValorTotal { get; set; }
        
        // 2. Usando a enumeração em vez de string
        public StatusPedido Status { get; set; }
        
        // Relacionamento 1:N com ItemPedido
        public ICollection<ItemPedido> Items { get; set; } = new List<ItemPedido>();
    }
}