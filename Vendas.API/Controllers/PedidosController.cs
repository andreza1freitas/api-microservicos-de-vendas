using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vendas.API.Data;
using Vendas.API.Models;
using Vendas.API.Enums;
using System.Net;
using Shared;
using Vendas.API.Services;
using Shared.Events; 
using Microsoft.AspNetCore.Authorization;
using Shared.Services;
using Shared.Messaging; 
using Shared.Enums; 

namespace Vendas.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PedidosController : ControllerBase
    {
        private readonly VendasDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IMessageBusPublisher _publisher;
        private readonly IConfiguration _configuration;

        public PedidosController(VendasDbContext context, IHttpClientFactory httpClientFactory,
        IMessageBusPublisher messageBusPublisher, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClientFactory.CreateClient("EstoqueApiClient");
            _publisher = messageBusPublisher;
            _configuration = configuration;
        }

        // GET api/pedidos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pedido>>> ConsultarPedidos()
        {
            return await _context.Pedidos
            .Include(p => p.Items)
            .ToListAsync();
        }

        // POST api/pedidos
        [HttpPost]
        public async Task<ActionResult<Pedido>> CriarPedido(CriarPedidoRequest request)
        {
            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest("O pedido deve conter itens.");
            }

            // Validação de Estoque síncrona
            foreach (var item in request.Items)
            {
                var validationUrl = $"api/Produtos/validar?produtoId={item.ProdutoId}&quantidade={item.Quantidade}";

                var response = await _httpClient.GetAsync(validationUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var statusCode = (int)response.StatusCode;

                    return StatusCode(statusCode, $"Falha na validação de estoque para Produto ID {item.ProdutoId}: {content}");
                }
            }

        // Criação e persistência do pedido (VendasDB)
            var novoPedido = new Pedido
            {
                DataPedido = DateTime.UtcNow,
                Status = StatusPedido.Processando,
                Items = request.Items.Select(i => new ItemPedido
                {
                    ProdutoId = i.ProdutoId,
                    Quantidade = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario
                }).ToList()
            };

            novoPedido.ValorTotal = novoPedido.Items.Sum(i => i.PrecoUnitario * i.Quantidade);

            _context.Pedidos.Add(novoPedido);
            await _context.SaveChangesAsync();

            // 3. Notificação assíncrona via Message Bus (RabbitMQ) - Baixa de Estoque
            try
            {
                var itensParaEstoque = novoPedido.Items.Select(i => new BaixaEstoqueItemMessage
                {
                    ProdutoId = i.ProdutoId,
                    Quantidade = i.Quantidade
                }).ToList();

                var mensagemEstoque = new EstoqueMessage
                {
                    PedidoId = novoPedido.Id,
                    TipoMensagem = MessageType.BaixaEstoque,
                    Items = itensParaEstoque
                };

                // Publica a mensagem de baixa de estoque diretamente na exchange 'estoque-exchange'
                _publisher.PublishEvent(mensagemEstoque, "estoque-exchange", "baixa-estoque");
                
                Console.WriteLine($"[Mensageria] Mensagem de baixa de estoque publicada para Pedido ID: {novoPedido.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO] Falha ao publicar mensagem no Message Bus: {ex.Message}. O pedido foi salvo, mas a baixa de estoque pode não ter sido iniciada.");
            }

            return CreatedAtAction(nameof(ConsultarPedidos), new { id = novoPedido.Id }, novoPedido);
        }

        // DELETE: api/Pedidos/5
        // Cancela o pedido, publica o evento de cancelamento e devolve os itens
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePedido(Guid id)
        {
            var pedido = await _context.Pedidos
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound();
            }

            if (pedido.Status == StatusPedido.Entregue || pedido.Status == StatusPedido.Cancelado)
            {
                return BadRequest($"Não é possível cancelar um pedido que está no status: {pedido.Status}.");
            }

            // 1. Atualiza o status no banco de dados
            pedido.Status = StatusPedido.Cancelado;
            _context.Pedidos.Update(pedido);
            await _context.SaveChangesAsync();
                        
            // Mapeia os itens do pedido para o DTO de mensagem
            var itensParaEstorno = pedido.Items.Select(i => new BaixaEstoqueItemMessage
            {
                ProdutoId = i.ProdutoId,
                Quantidade = i.Quantidade
            }).ToList();

            // 2. Cria e publica o EstornoEstoqueMessage
            var estornoMessage = new EstornoEstoqueMessage
            {
                PedidoId = pedido.Id,
                TipoMensagem = MessageType.EstornoEstoque,
                Items = itensParaEstorno
            };

            // Publica no Exchange de estoque com a chave 'estorno-estoque'
            _publisher.PublishEvent(estornoMessage, "estoque-exchange", "estorno-estoque");

            // 3. Publica PedidoStatusAtualizadoEvent
             var eventoStatusAtualizado = new PedidoStatusAtualizadoEvent
            {
                PedidoId = id,
                StatusPedido = pedido.Status,
                DataAtualizacao = DateTime.UtcNow
            };
            
            _publisher.PublishEvent(eventoStatusAtualizado);

            // 4. Retorna sucesso (204 No Content)
            return NoContent();
        }

        // Define apenas o que pode ser atualizado (o Status)
        public class AtualizarPedidoStatusRequest
        {
            public StatusPedido Status { get; set; }
        }

        // PUT: api/Pedidos/5 - atualiza apenas o Status
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPedido(Guid id, AtualizarPedidoStatusRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var pedidoExistente = await _context.Pedidos.FindAsync(id);

            if (pedidoExistente == null)
            {
                return NotFound($"Pedido com ID {id} não encontrado.");
            }

            var statusAtual = pedidoExistente.Status;
            var novoStatus = request.Status;

            // Validação de transição de status 
            if (statusAtual == novoStatus)
            {
                return BadRequest("O pedido já está neste status.");
            }

            if (statusAtual == StatusPedido.Enviado && novoStatus == StatusPedido.Processando)
            {
                return BadRequest("Não é possível reverter o status de Enviado para Processando.");
            }

            // Exemplo: Não permitir alterar status se já foi Entregue ou Cancelado
            if (statusAtual == StatusPedido.Entregue || statusAtual == StatusPedido.Cancelado)
            {
                return BadRequest($"Não é possível alterar o status de um pedido que está como {statusAtual}.");
            }

            // Aplica a mudança de status
            pedidoExistente.Status = novoStatus;

            try
            {
                await _context.SaveChangesAsync();

                // Publica um evento de atualização (se necessário)
                var eventoAtualizado = new PedidoStatusAtualizadoEvent
                {
                    PedidoId = id,
                    StatusPedido = novoStatus, 
                    DataAtualizacao = DateTime.UtcNow
                };
                _publisher.PublishEvent(eventoAtualizado);

            }
            catch (DbUpdateConcurrencyException) when (!PedidoExists(id))
            {
                return NotFound();
            }

            // 204 No Content: O pedido foi atualizado com sucesso
            return NoContent();
        }

        private bool PedidoExists(Guid id)
        {
            return _context.Pedidos.Any(e => e.Id == id);
        }
    }
}