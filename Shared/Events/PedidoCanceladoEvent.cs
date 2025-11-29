using System;
using Shared.Enums;

namespace Shared.Events
{
    // Evento publicado pela Vendas.API quando um pedido é cancelado.
    public class PedidoCanceladoEvent : IEvent
    {
        public Guid PedidoId { get; set; }
        public DateTime DataCancelamento { get; set; }
        public List<ItemPedidoEvent> Items { get; set; } = new List<ItemPedidoEvent>();
    }
}