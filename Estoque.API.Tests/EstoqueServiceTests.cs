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

namespace Estoque.API.Tests.Services
{
    // A implementação de IDisposable garante que o banco de dados em memória seja limpo após cada teste
    public class EstoqueServiceTests : IDisposable
    {
        private readonly EstoqueDbContext _context;
        private readonly Mock<IConnection> _rabbitMQConnectionMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IModel> _channelMock;

        private readonly EstoqueService _estoqueService;

        private readonly string _estoqueExchangeName = "estoque-exchange-test";
        
        private readonly Guid TestPedidoId = Guid.Parse("12345678-1234-5678-1234-567812345678"); 

        public EstoqueServiceTests()
        {
            // Configuração do DbContext em memória para testes
            var options = new DbContextOptionsBuilder<EstoqueDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                // Garante que o Entity Framework In-Memory ignore o aviso de transação
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new EstoqueDbContext(options);

            // Mocking da Conexão e do Canal (IConnection e IModel)
            _rabbitMQConnectionMock = new Mock<IConnection>();
            _channelMock = new Mock<IModel>();

            // Configura a conexão para retornar o canal mockado
            _rabbitMQConnectionMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);
            // Setup do ExchangeDeclare para não reclamar
            _channelMock.Setup(c => c.ExchangeDeclare(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>(), 
                It.IsAny<bool>(), 
                It.IsAny<IDictionary<string, object>>()));

            // Mocking da Configuração
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock
                .SetupGet(c => c["MessagePublishing:EstoqueExchange"])
                .Returns(_estoqueExchangeName);
            
            // Criação do serviço sob teste
            _estoqueService = new EstoqueService(
                _context,
                _rabbitMQConnectionMock.Object,
                _configurationMock.Object);
        }

        private void AddProductsToDatabase(IEnumerable<Produto> produtos)
        {
            _context.Produtos.AddRange(produtos);
            _context.SaveChanges();
        }

        [Fact]
        public async Task BaixarEstoque_DeveBaixarCorretamente_E_PublicarConfirmacao()
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
            
            // Variáveis para capturar os argumentos do BasicPublish
            string? capturedRoutingKey = null;
            ReadOnlyMemory<byte> capturedBody = default;

            // CAPTURA DO ARGUMENTO: Configura o Mock para capturar o body e a routingKey
            _channelMock.Setup(c => c.BasicPublish(
                It.Is<string>(e => e == _estoqueExchangeName),
                It.IsAny<string>(), // Captura a routingKey
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>() // Captura o body
            ))
            .Callback<string, string, bool, IBasicProperties, ReadOnlyMemory<byte>>((exchange, routingKey, mandatory, props, body) => 
            {
                capturedRoutingKey = routingKey;
                capturedBody = body;
            });


            // Act
            await _estoqueService.BaixarEstoque(message);

            // Verifica se o estoque foi atualizado no banco
            Assert.Equal(7, _context.Produtos.Find(1)!.QuantidadeEmEstoque);
            Assert.Equal(0, _context.Produtos.Find(2)!.QuantidadeEmEstoque);

            // Verifica se BasicPublish foi chamado exatamente uma vez (chamada genérica)
            _channelMock.Verify(
                c => c.BasicPublish(
                    It.Is<string>(e => e == _estoqueExchangeName), 
                    It.IsAny<string>(), 
                    It.IsAny<bool>(),
                    It.IsAny<IBasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>()
                ),
                Times.Once,
                "A mensagem de confirmação não foi publicada."
            );

            // Verifica o conteúdo capturado fora da expressão lambda do Verify
            Assert.Equal("baixa-estoque-confirmed", capturedRoutingKey);
            Assert.True(VerifyConfirmationBody(capturedBody.ToArray(), TestPedidoId), "O payload de confirmação está incorreto.");
        }

        [Fact]
        public async Task BaixarEstoque_DeveFalhar_SeProdutoNaoExistir_E_PublicarFalha()
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
            string expectedReason = $"Produto ID {nonExistingProductId} não encontrado.";
            
            // Variáveis para capturar os argumentos do BasicPublish
            string? capturedRoutingKey = null;
            ReadOnlyMemory<byte> capturedBody = default;
            
            // CAPTURA DO ARGUMENTO: Configura o Mock para capturar o body e a routingKey
            _channelMock.Setup(c => c.BasicPublish(
                It.Is<string>(e => e == _estoqueExchangeName),
                It.IsAny<string>(), 
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()
            ))
            .Callback<string, string, bool, IBasicProperties, ReadOnlyMemory<byte>>((exchange, routingKey, mandatory, props, body) => 
            {
                capturedRoutingKey = routingKey;
                capturedBody = body;
            });

            // Act & Assert
            // Espera-se que lance uma InvalidOperationException
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _estoqueService.BaixarEstoque(message)
            );

            // Verifica se o estoque do produto existente NÃO foi alterado (Rollback)
            Assert.Equal(initialStock, _context.Produtos.Find(100)!.QuantidadeEmEstoque);
            
            // Verifica se BasicPublish foi chamado exatamente uma vez
             _channelMock.Verify(
                c => c.BasicPublish(
                    It.Is<string>(e => e == _estoqueExchangeName), 
                    It.IsAny<string>(), 
                    It.IsAny<bool>(),
                    It.IsAny<IBasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>()
                ),
                Times.Once,
                "A mensagem de falha não foi publicada."
            );
            
            // Verifica o conteúdo capturado fora da expressão lambda do Verify
            Assert.Equal("baixa-estoque-failed", capturedRoutingKey);
            Assert.True(VerifyFailureBody(capturedBody.ToArray(), message.PedidoId, expectedReason), "O payload de falha está incorreto.");
        }
        
        [Fact]
        public async Task BaixarEstoque_DeveFalhar_SeEstoqueInsuficiente_E_PublicarFalha()
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
            string expectedReason = $"Estoque insuficiente para o produto ID: 50.";
            
            // Variáveis para capturar os argumentos do BasicPublish
            string? capturedRoutingKey = null;
            ReadOnlyMemory<byte> capturedBody = default;
            
            // CAPTURA DO ARGUMENTO: Configura o Mock para capturar o body e a routingKey
            _channelMock.Setup(c => c.BasicPublish(
                It.Is<string>(e => e == _estoqueExchangeName),
                It.IsAny<string>(), 
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()
            ))
            .Callback<string, string, bool, IBasicProperties, ReadOnlyMemory<byte>>((exchange, routingKey, mandatory, props, body) => 
            {
                capturedRoutingKey = routingKey;
                capturedBody = body;
            });


            // Act & Assert
            // Espera-se que lance uma InvalidOperationException
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _estoqueService.BaixarEstoque(message)
            );
            
            // Verifica se o estoque NÃO foi alterado (Rollback)
            Assert.Equal(initialStock, _context.Produtos.Find(50)!.QuantidadeEmEstoque);

            // Verifica se BasicPublish foi chamado exatamente uma vez
            _channelMock.Verify(
                c => c.BasicPublish(
                    It.Is<string>(e => e == _estoqueExchangeName), 
                    It.IsAny<string>(), 
                    It.IsAny<bool>(),
                    It.IsAny<IBasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>()
                ),
                Times.Once,
                "A mensagem de falha por estoque insuficiente não foi publicada corretamente."
            );
            
            // Verifica o conteúdo capturado fora da expressão lambda do Verify
            Assert.Equal("baixa-estoque-failed", capturedRoutingKey);
            Assert.True(VerifyFailureBody(capturedBody.ToArray(), message.PedidoId, expectedReason), "O payload de falha está incorreto.");
        }

        // Método auxiliar para verificar o corpo (payload) da mensagem de Confirmação
        private bool VerifyConfirmationBody(byte[] body, Guid expectedPedidoId)
        {
            var json = Encoding.UTF8.GetString(body);
            try
            {
                // Usando a classe BaixaEstoqueConfirmedMessage
                var message = JsonSerializer.Deserialize<BaixaEstoqueConfirmedMessage>(json, EstoqueService.JsonSerializerOptions);

                return message != null && 
                       message.PedidoId == expectedPedidoId && 
                       message.TipoMensagem == MessageType.BaixaEstoqueConfirmed;
            }
            catch
            {
                return false;
            }
        }
        
        // Método auxiliar para verificar o corpo (payload) da mensagem de Falha
        private bool VerifyFailureBody(byte[] body, Guid expectedPedidoId, string expectedReason)
        {
            var json = Encoding.UTF8.GetString(body);
            try
            {
                // Usando a classe BaixaEstoqueFailedMessage
                var message = JsonSerializer.Deserialize<BaixaEstoqueFailedMessage>(json, EstoqueService.JsonSerializerOptions);

                return message != null && 
                       message.PedidoId == expectedPedidoId && 
                       message.MotivoFalha == expectedReason &&
                       message.TipoMensagem == MessageType.BaixaEstoqueFailed;
            }
            catch
            {
                return false;
            }
        }

       // Limpa o banco de dados em memória após cada teste
        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}