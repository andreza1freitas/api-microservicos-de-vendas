using System;
using Shared.Enums;

namespace Shared.Messaging
{
    // Mensagem de Falha de Negócio (Estoque Insuficiente/Produto não encontrado)
    public class BaixaEstoqueFailedMessage
    {
        public Guid PedidoId { get; set; }
        public string MotivoFalha { get; set; } = string.Empty;
        public MessageType TipoMensagem { get; set; }
    }
}