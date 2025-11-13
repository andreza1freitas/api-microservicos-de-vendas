using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Estoque.API.Data;
using Estoque.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Services
{
    public class EstoqueService : IEstoqueService
    {
        private readonly EstoqueDbContext _context;
        private readonly ILogger<EstoqueService> _logger;

        public EstoqueService(EstoqueDbContext context, ILogger<EstoqueService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ProcessarBaixaEstoqueAsync(PedidoCriadoMessage message)
        {
            _logger.LogInformation($"[Estoque] Processando Pedido ID: {message.PedidoId}");

            // 1. Itera sobre cada item do pedido
            foreach (var item in message.Itens)
            {
                // Busca o produto no banco de dados
                var produto = await _context.Produtos
                                            .FirstOrDefaultAsync(p => p.Id == item.ProdutoId);

                if (produto == null)
                {
                    _logger.LogWarning($"[Estoque] Produto ID {item.ProdutoId} não encontrado. Pedido {message.PedidoId} não processado.");
                    // Em um cenário real, você publicaria um evento de falha ou faria retry.
                    continue; 
                }

                if (produto.QuantidadeEmEstoque >= item.Quantidade)
                {
                    // 2. Realiza a baixa de estoque
                    produto.QuantidadeEmEstoque -= item.Quantidade;
                    _logger.LogInformation($"[Estoque] Baixa de {item.Quantidade} unidades do Produto ID {item.ProdutoId}. Novo estoque: {produto.QuantidadeEmEstoque}.");
                }
                else
                {
                    _logger.LogError($"[Estoque] FALHA: Estoque insuficiente para Produto ID {item.ProdutoId}. Estoque atual: {produto.QuantidadeEmEstoque}, Necessário: {item.Quantidade}.");
                    // Marca o item ou o pedido como falho.
                }
            }

            // 3. Salva todas as alterações no banco de dados
            await _context.SaveChangesAsync();
            _logger.LogInformation($"[Estoque] Pedido ID {message.PedidoId} processado com sucesso.");
        }
    }
}