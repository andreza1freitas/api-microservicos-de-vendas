using System;
using System.Collections.Generic;
using Shared.Enums;
using Shared.Events;

namespace Shared.Messaging
{
    // Mensagem para estornar (devolver) itens ao estoque.
    public class EstornoEstoqueMessage : BaseMessage, IEvent
    {
        public List<BaixaEstoqueItemMessage> Items { get; set; } = new List<BaixaEstoqueItemMessage>();

        public EstornoEstoqueMessage()
        {
            TipoMensagem = MessageType.EstornoEstoque;
        }
    }
}