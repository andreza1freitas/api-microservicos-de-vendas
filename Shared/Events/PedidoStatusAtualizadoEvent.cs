using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Enums;
using Vendas.API.Enums;

namespace Shared.Events
{
    // Evento publicado pela Vendas.API quando o status de um pedido é atualizado.
    public class PedidoStatusAtualizadoEvent : IEvent
    {
        public Guid PedidoId { get; set; }
        public StatusPedido StatusPedido { get; set; }

        public DateTime DataAtualizacao { get; set; }
    }
}