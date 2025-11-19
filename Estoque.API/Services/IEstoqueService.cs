using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Estoque.API.Models;
using Shared.Messaging;

namespace Estoque.API.Services
{
    public interface IEstoqueService
    {
        // Método para baixar o estoque com base na mensagem recebida
        Task BaixarEstoque(BaixaEstoqueMessage message);

        // Método para republicar a mensagem em caso de falha
        Task RepublishMessage(BaixaEstoqueMessage message);

       // Método para publicar mensagem de confirmação
        Task PublishConfirmationMessage(BaixaEstoqueMessage originalMessage);

        // Método para publicar mensagem de falha
        Task PublishFailedMessage(BaixaEstoqueMessage originalMessage, string reason);

    }
}