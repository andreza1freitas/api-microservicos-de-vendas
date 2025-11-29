using Estoque.API.Data;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Shared.Messaging;
using System.Text.Json;
using System.Text;
using Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Estoque.API.Models;
using System.Collections.Generic;
using System.Linq; 

namespace Estoque.API.Services
{
    public class EstoqueService : IEstoqueService, IDisposable
    {
        private readonly EstoqueDbContext _context;
        private readonly IConfiguration _configuration;
        
        private IModel? _channel;
        private IConnection? _connection;

        public static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNamingPolicy = null 
        };

        // Construtor simplificado
        public EstoqueService(
            EstoqueDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            
            // Cria a conexão e channel lazy (quando necessário)
        }

        private void EnsureConnection()
        {
            if (_connection == null || !_connection.IsOpen)
            {
                var hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
                var factory = new ConnectionFactory() 
                { 
                    HostName = hostName,
                    DispatchConsumersAsync = true 
                };
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                var estoqueExchangeName = _configuration["MessagePublishing:EstoqueExchange"] ?? "estoque-exchange";
                _channel.ExchangeDeclare(exchange: estoqueExchangeName, type: "topic", durable: true);
            }
        }

        // --- Lógica de Baixa de Estoque ---
        public async Task BaixarEstoque(BaixaEstoqueMessage message)
        {
            Console.WriteLine($"[EstoqueService] Iniciando baixa de estoque para Pedido ID: {message.PedidoId}");

            // Usando o _context injetado
            using IDbContextTransaction? transaction = await _context.Database.BeginTransactionAsync(); 
            try
            {
                var motivoFalha = string.Empty;

                if (message.Items == null || message.Items.Count == 0)
                {
                    var motivo = "Lista de itens vazia. Verifique o DTO de mensagem.";
                    Console.WriteLine($"[EstoqueService] AVISO: {motivo}");

                    await transaction.RollbackAsync();
                    Console.WriteLine("[EstoqueService] Transação com Rollback devido a lista de itens vazia. Publicando Falha.");
                    await PublishFailedMessage(message.PedidoId, message.TipoMensagem, motivo);
                    throw new InvalidOperationException(motivo);
                }

                foreach (var item in message.Items)
                {
                    Console.WriteLine($"[EstoqueService] Processando item: Produto ID {item.ProdutoId}, Quantidade: {item.Quantidade}");

                    var produto = await _context.Produtos 
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == item.ProdutoId);

                    if (produto == null)
                    {
                        motivoFalha = $"Produto ID {item.ProdutoId} não encontrado.";
                        Console.WriteLine($"[EstoqueService] Falha: {motivoFalha}");
                        break;
                    }

                    if (produto.QuantidadeEmEstoque < item.Quantidade)
                    {
                        motivoFalha = $"Estoque insuficiente para o produto ID: {item.ProdutoId}. Estoque atual: {produto.QuantidadeEmEstoque}. Requerido: {item.Quantidade}.";
                        Console.WriteLine($"[EstoqueService] Falha de Estoque: {motivoFalha}");
                        break;
                    }

                    // 2. Realiza a baixa de estoque usando ExecuteUpdateAsync
                    int rowsAffected = await _context.Produtos 
                        .Where(p => p.Id == item.ProdutoId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.QuantidadeEmEstoque, p => p.QuantidadeEmEstoque - item.Quantidade));

                    Console.WriteLine($"[EstoqueService] ExecuteUpdateAsync: {rowsAffected} linha(s) afetada(s) para o Produto ID {item.ProdutoId}.");
                }

                if (!string.IsNullOrEmpty(motivoFalha))
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("[EstoqueService] Transação com Rollback. Publicando Falha.");
                    await PublishFailedMessage(message.PedidoId, message.TipoMensagem, motivoFalha); 
                    throw new InvalidOperationException(motivoFalha);
                }

                await transaction.CommitAsync();
                Console.WriteLine("[EstoqueService] Transação com Commit.");

                await PublishConfirmationMessage(message); 
            }
            catch (Exception ex)
            {
                try
                {
                    // Usa o _context injetado
                    if (_context.Database.IsRelational() && transaction?.GetDbTransaction()?.Connection != null)
                    {
                        await transaction.RollbackAsync();
                    }
                }
                catch (Exception rollbackEx)
                {
                    Console.WriteLine($"Erro durante Rollback: {rollbackEx.Message}");
                }

                Console.WriteLine($"Erro CRÍTICO ao processar BaixaEstoque: {ex.Message}");
                throw; 
            }
        }
        
        // --- Lógica de Estorno de Estoque ---
        public async Task EstornarEstoque(EstornoEstoqueMessage message)
        {
            Console.WriteLine($"[EstoqueService] Iniciando estorno de estoque para Pedido ID: {message.PedidoId}");
            
            // Usando o _context injetado
            using IDbContextTransaction? transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in message.Items)
                {
                    // 1. Realiza o estorno de estoque (aumenta a quantidade)
                    int rowsAffected = await _context.Produtos 
                        .Where(p => p.Id == item.ProdutoId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.QuantidadeEmEstoque, p => p.QuantidadeEmEstoque + item.Quantidade));
                    
                    Console.WriteLine($"[EstoqueService] ExecuteUpdateAsync (Estorno): {rowsAffected} linha(s) afetada(s) para o Produto ID {item.ProdutoId}.");
                }
                
                await transaction.CommitAsync();
                Console.WriteLine("[EstoqueService] Estorno: Transação com Commit.");

                await PublishEstornoConfirmationMessage(message); 
            }
            catch (Exception ex)
            {
                try
                {
                    // Usa o _context injetado
                    if (_context.Database.IsRelational() && transaction?.GetDbTransaction()?.Connection != null)
                    {
                        await transaction.RollbackAsync();
                    }
                }
                catch (Exception rollbackEx)
                {
                    Console.WriteLine($"Erro durante Rollback: {rollbackEx.Message}");
                }
                
                Console.WriteLine($"Erro ao processar EstornoEstoque (Cancelamento): {ex.Message}");
                throw; 
            }
        }
        
        // --- Métodos de Publicação (inalterados) ---

        public Task PublishConfirmationMessage(BaixaEstoqueMessage originalMessage)
        {
            EnsureConnection();

            var confirmedMessage = new BaixaEstoqueConfirmedMessage
            {
                PedidoId = originalMessage.PedidoId, 
                TipoMensagem = MessageType.BaixaEstoqueConfirmed
            };
            var json = JsonSerializer.Serialize(confirmedMessage, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var estoqueExchangeName = _configuration["MessagePublishing:EstoqueExchange"] ?? "estoque-exchange";
            _channel!.BasicPublish(
                exchange: estoqueExchangeName,
                routingKey: "baixa-estoque-confirmed",
                basicProperties: null,
                body: body);
            
            Console.WriteLine($"[EstoqueService] Publicada Confirmação de Baixa: Pedido ID {originalMessage.PedidoId}");

            return Task.CompletedTask;
        }
        
        public Task PublishEstornoConfirmationMessage(EstornoEstoqueMessage originalMessage)
        {
            EnsureConnection();

            var confirmedMessage = new EstornoEstoqueConfirmedMessage
            {
                PedidoId = originalMessage.PedidoId, 
                TipoMensagem = MessageType.EstornoEstoqueConfirmed
            };
            var json = JsonSerializer.Serialize(confirmedMessage, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var estoqueExchangeName = _configuration["MessagePublishing:EstoqueExchange"] ?? "estoque-exchange";
            _channel!.BasicPublish(
                exchange: estoqueExchangeName,
                routingKey: "estorno-estoque-confirmed", 
                basicProperties: null,
                body: body);
            
            Console.WriteLine($"[EstoqueService] Publicada Confirmação de Estorno: Pedido ID {originalMessage.PedidoId}");

            return Task.CompletedTask;
        }

        public Task PublishFailedMessage(Guid pedidoId, MessageType tipoOriginal, string reason)
        {
            EnsureConnection();

            var failedMessage = new BaixaEstoqueFailedMessage 
            {
                PedidoId = pedidoId, 
                MotivoFalha = reason,
                TipoMensagem = MessageType.BaixaEstoqueFailed 
            };
            var json = JsonSerializer.Serialize(failedMessage, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var estoqueExchangeName = _configuration["MessagePublishing:EstoqueExchange"] ?? "estoque-exchange";
            _channel!.BasicPublish(
                exchange: estoqueExchangeName,
                routingKey: "baixa-estoque-failed",
                basicProperties: null,
                body: body);
            
            Console.WriteLine($"[EstoqueService] Publicada Falha de Baixa: Pedido ID {pedidoId}. Motivo: {reason}");

            return Task.CompletedTask;
        }
        
        public Task RepublishMessage(object message, MessageType type)
        {
            EnsureConnection();

            string routingKey = type switch
            {
                MessageType.BaixaEstoque => "baixa-estoque",
                MessageType.EstornoEstoque => "estorno-estoque",
                _ => throw new ArgumentException($"Tipo de mensagem desconhecido para republicação: {type}")
            };

            var json = JsonSerializer.Serialize(message, JsonSerializerOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var estoqueExchangeName = _configuration["MessagePublishing:EstoqueExchange"] ?? "estoque-exchange";
            _channel!.BasicPublish(
                exchange: estoqueExchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: body);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close(); 
            _connection?.Dispose(); 
            GC.SuppressFinalize(this); 
        }
    }
}