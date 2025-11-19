using System;
using Shared.Enums;

namespace Shared.Messaging
{
    // Mensagem de Confirmação (Estoque OK)
    public class BaixaEstoqueConfirmedMessage
    {
        // Usando Guid para PedidoId, conforme o contrato.
        public Guid PedidoId { get; set; }
        public MessageType TipoMensagem { get; set; }
    }
}