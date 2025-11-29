using System;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Messaging;

namespace Estoque.API.Services
{
    public interface IEstoqueService
    {
        // Baixa de estoque (fluxo de criação de pedido)
        Task BaixarEstoque(BaixaEstoqueMessage message);

        // Estorno de estoque (fluxo de cancelamento de pedido)
        Task EstornarEstoque(EstornoEstoqueMessage message); 

        // Publica mensagem de confirmação de baixa
        Task PublishConfirmationMessage(BaixaEstoqueMessage originalMessage);
        
        // Publica mensagem de confirmação de estorno
        Task PublishEstornoConfirmationMessage(EstornoEstoqueMessage originalMessage);

        // Publica mensagem de falha na baixa ou estorno
        Task PublishFailedMessage(Guid pedidoId, MessageType tipoOriginal, string reason); 

        // Republica a mensagem em caso de falha temporária 
        // Aceita o objeto da mensagem (BaixaEstoqueMessage ou EstornoEstoqueMessage) e o tipo.
        Task RepublishMessage(object message, MessageType type); 
    }
}