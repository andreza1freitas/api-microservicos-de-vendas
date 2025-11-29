using System;
using System.Collections.Generic;
using Shared.Enums;
using Shared.Events;

namespace Shared.Messaging
{
    public class EstoqueMessage : BaseMessage, IEvent
    {
        // Lista de itens do pedido que precisam de operação no estoque.
        public List<BaixaEstoqueItemMessage> Items { get; set; } = new();
    }
}
