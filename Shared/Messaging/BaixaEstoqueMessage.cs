using System;
using System.Collections.Generic;
using Shared.Enums;

namespace Shared.Messaging
{
    // Classe que representa o evento/comando para baixar o estoque após a criação de um pedido.
    public class BaixaEstoqueMessage
    {
        // Usando Guid como identificador global exclusivo (melhor prática em microsserviços).
        public Guid PedidoId { get; set; }

        public MessageType TipoMensagem { get; set; }

        // A lista de itens do pedido que precisam de baixa no estoque.
        public List<BaixaEstoqueItemMessage> Items { get; set; } = new List<BaixaEstoqueItemMessage>();
    }
}