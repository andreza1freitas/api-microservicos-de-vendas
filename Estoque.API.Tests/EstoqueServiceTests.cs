using Estoque.API.Data;
using Estoque.API.Models;
using Estoque.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Messaging; 
using Shared.Enums;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Threading.Tasks;
using System; 
using System.Linq;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite; 

namespace Estoque.API.Tests.Services
{
    // A implementação de IDisposable garante que o banco de dados seja limpo após cada teste
    public class EstoqueServiceTests : IDisposable
    {
        private readonly EstoqueDbContext _context;
        private readonly SqliteConnection _connection; 
        private readonly Mock<IConfiguration> _configurationMock;

        private readonly EstoqueService _estoqueService;

        private readonly string _estoqueExchangeName = "estoque-exchange-test";
        
        private readonly Guid TestPedidoId = Guid.Parse("12345678-1234-5678-1234-567812345678"); 

        public EstoqueServiceTests()
        {
            // 1. Configura e abre a conexão SQLite In-Memory
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            // 2. Configuração do DbContext para usar o SQLite
            var options = new DbContextOptionsBuilder<EstoqueDbContext>()
                .UseSqlite(_connection) 
                .Options;

            _context = new EstoqueDbContext(options);
            _context.Database.EnsureCreated(); // Cria o schema do banco de dados

            // Mocking da Configuração
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock
                .SetupGet(c => c["MessagePublishing:EstoqueExchange"])
                .Returns(_estoqueExchangeName);
            
            _configurationMock
                .SetupGet(c => c["RabbitMQ:HostName"])
                .Returns("localhost");
            
            // Criação do serviço sob teste
            _estoqueService = new EstoqueService(
                _context,
                _configurationMock.Object);
        }

        private void AddProductsToDatabase(IEnumerable<Produto> produtos)
        {
            _context.Produtos.AddRange(produtos);
            _context.SaveChanges();
            _context.ChangeTracker.Clear(); 
        }

        [Fact]
        public async Task BaixarEstoque_DeveBaixarCorretamente()
        {
            // Arrange
            var produto1 = new Produto { Id = 1, QuantidadeEmEstoque = 10, Nome = "Item A", Preco = 10.0m };
            var produto2 = new Produto { Id = 2, QuantidadeEmEstoque = 5, Nome = "Item B", Preco = 20.0m };
            AddProductsToDatabase(new[] { produto1, produto2 });

            var message = new BaixaEstoqueMessage
            {
                PedidoId = TestPedidoId, 
                Items = new List<BaixaEstoqueItemMessage>
                {
                    new BaixaEstoqueItemMessage { ProdutoId = 1, Quantidade = 3 }, 
                    new BaixaEstoqueItemMessage { ProdutoId = 2, Quantidade = 5 }
                }
            };

            // Act
            await _estoqueService.BaixarEstoque(message);

            // Verifica se o estoque foi atualizado corretamente
            Assert.Equal(7, _context.Produtos.Find(1)!.QuantidadeEmEstoque);
            Assert.Equal(0, _context.Produtos.Find(2)!.QuantidadeEmEstoque);
        }

        [Fact]
        public async Task BaixarEstoque_DeveFalhar_SeProdutoNaoExistir()
        {
            // Arrange
            var produtoExistente = new Produto { Id = 100, QuantidadeEmEstoque = 10, Nome = "Item C", Preco = 5.0m };
            AddProductsToDatabase(new[] { produtoExistente });

            var nonExistingProductId = 999;
            var failedPedidoId = Guid.NewGuid();

            var message = new BaixaEstoqueMessage
            {
                PedidoId = failedPedidoId, 
                Items = new List<BaixaEstoqueItemMessage>
                {
                    new BaixaEstoqueItemMessage { ProdutoId = nonExistingProductId, Quantidade = 1 }
                }
            };

            var initialStock = _context.Produtos.Find(100)!.QuantidadeEmEstoque;

            // Act & Assert
            // Espera-se que lance uma InvalidOperationException
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _estoqueService.BaixarEstoque(message)
            );

            // Verifica se o estoque do produto existente NÃO foi alterado (Rollback)
            Assert.Equal(initialStock, _context.Produtos.Find(100)!.QuantidadeEmEstoque);
            
            Assert.Contains("não encontrado", exception.Message);
        }
        
        [Fact]
        public async Task BaixarEstoque_DeveFalhar_SeEstoqueInsuficiente()
        {
            // Arrange
            var produto1 = new Produto { Id = 50, QuantidadeEmEstoque = 5, Nome = "Item com pouco estoque", Preco = 10.0m };
            AddProductsToDatabase(new[] { produto1 });

            var failedPedidoId = Guid.NewGuid();

            var message = new BaixaEstoqueMessage
            {
                PedidoId = failedPedidoId,
                Items = new List<BaixaEstoqueItemMessage>
                {
                    // Tentativa de baixar 6, mas só tem 5
                    new BaixaEstoqueItemMessage { ProdutoId = 50, Quantidade = 6 } 
                }
            };

            var initialStock = _context.Produtos.Find(50)!.QuantidadeEmEstoque;

            // Act & Assert
            // Espera-se que lance uma InvalidOperationException
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _estoqueService.BaixarEstoque(message)
            );
            
            // Verifica se o estoque NÃO foi alterado (Rollback)
            Assert.Equal(initialStock, _context.Produtos.Find(50)!.QuantidadeEmEstoque);

            // Verifica a mensagem de erro
            Assert.Contains("Estoque insuficiente", exception.Message);
        }

        // Limpa o banco de dados em memória após cada teste
        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}