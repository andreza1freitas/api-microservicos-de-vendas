using Estoque.API.Data;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Shared.Messaging;
using System.Text.Json;
using System.Text;
using Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading.Tasks;

namespace Estoque.API.Services
{
    public class EstoqueService : IEstoqueService, IDisposable
    {
        private readonly EstoqueDbContext _dbContext;
        private readonly IConnection _rabbitMQConnection;
        private readonly string _estoqueExchangeName;
        private readonly IModel _channel;

        public static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public EstoqueService(
            EstoqueDbContext dbContext,
            IConnection rabbitMQConnection,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _rabbitMQConnection = rabbitMQConnection;

            _estoqueExchangeName = configuration["MessagePublishing:EstoqueExchange"]
                ?? throw new ArgumentException("A chave 'MessagePublishing:EstoqueExchange' não pode ser nula.");

            _channel = _rabbitMQConnection.CreateModel();
            _channel.ExchangeDeclare(exchange: _estoqueExchangeName, type: "topic", durable: true);
        }

        public async Task BaixarEstoque(BaixaEstoqueMessage message)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var motivoFalha = string.Empty;

                foreach (var item in message.Items)
                {
                    var produto = await _dbContext.Produtos.FindAsync(item.ProdutoId);

                    if (produto == null)
                    {
                        motivoFalha = $"Produto ID {item.ProdutoId} não encontrado.";
                        break;
                    }

                    if (produto.QuantidadeEmEstoque < item.Quantidade)
                    {
                        motivoFalha = $"Estoque insuficiente para o produto ID: {item.ProdutoId}.";
                        break;
                    }

                    produto.QuantidadeEmEstoque -= item.Quantidade;
                    _dbContext.Produtos.Update(produto);
                }

                if (!string.IsNullOrEmpty(motivoFalha))
                {
                    await transaction.RollbackAsync();
                    // Chama o método da interface
                    await PublishFailedMessage(message, motivoFalha); 
                    throw new InvalidOperationException(motivoFalha);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // Chama o método da interface
                await PublishConfirmationMessage(message); 
            }
            catch (Exception ex)
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }
                Console.WriteLine($"Erro ao processar BaixaEstoque: {ex.Message}");
                throw; 
            }
        }

        /// <summary>
        /// Implementação do método da interface para publicar mensagem de confirmação.
        /// </summary>
        public Task PublishConfirmationMessage(BaixaEstoqueMessage originalMessage)
        {
            var confirmedMessage = new BaixaEstoqueConfirmedMessage
            {
                PedidoId = originalMessage.PedidoId, 
                TipoMensagem = MessageType.BaixaEstoqueConfirmed
            };
            var json = JsonSerializer.Serialize(confirmedMessage, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: _estoqueExchangeName,
                routingKey: "baixa-estoque-confirmed",
                basicProperties: null,
                body: body);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Implementação do método da interface para publicar mensagem de falha.
        /// </summary>
        public Task PublishFailedMessage(BaixaEstoqueMessage originalMessage, string reason)
        {
            var failedMessage = new BaixaEstoqueFailedMessage
            {
                PedidoId = originalMessage.PedidoId, 
                MotivoFalha = reason,
                TipoMensagem = MessageType.BaixaEstoqueFailed
            };
            var json = JsonSerializer.Serialize(failedMessage, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: _estoqueExchangeName,
                routingKey: "baixa-estoque-failed",
                basicProperties: null,
                body: body);

            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Implementação do método da interface para republicar a mensagem (usado para DLQ ou retentativas).
        /// </summary>
        public Task RepublishMessage(BaixaEstoqueMessage message)
        {
            var json = JsonSerializer.Serialize(message, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: _estoqueExchangeName,
                routingKey: "baixa-estoque-inbound", // Chave para a fila principal/original
                basicProperties: null,
                body: body);

            return Task.CompletedTask;
        }

        // Método para garantir o dispose do canal (corrigido para ser implementado explicitamente do IDisposable)
        public void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();

            GC.SuppressFinalize(this); 
        }
    }
}