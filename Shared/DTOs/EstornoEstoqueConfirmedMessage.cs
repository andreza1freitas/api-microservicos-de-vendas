using System;
using Shared.Enums;

namespace Shared.Messaging
{
    // Mensagem de Confirmação de Estorno (Estoque Devolvido OK)
    public class EstornoEstoqueConfirmedMessage
    {
        public Guid PedidoId { get; set; }
        public MessageType TipoMensagem { get; set; }

        public EstornoEstoqueConfirmedMessage()
        {
            TipoMensagem = MessageType.EstornoEstoqueConfirmed;
        }
    }
}