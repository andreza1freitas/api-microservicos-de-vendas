using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vendas.API.Data;
using Vendas.API.Models;
using System.Net;
using Shared;
using Vendas.API.Services;
using Shared.Events;
using Microsoft.AspNetCore.Authorization;

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
            .Include(p => p.Itens)
            .ToListAsync();
    }

    // POST api/pedidos
    [HttpPost]
    public async Task<ActionResult<Pedido>> CriarPedido(CriarPedidoRequest request)
    {
        if (request.Itens == null || !request.Itens.Any())
        {
            return BadRequest("O pedido deve conter itens.");
        }

        // Validação de Estoque síncrona
        foreach (var item in request.Itens)
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
            Status = "Processando",
            Itens = request.Itens.Select(i => new ItemPedido
            {
                ProdutoId = i.ProdutoId,
                Quantidade = i.Quantidade,
                PrecoUnitario = i.PrecoUnitario
            }).ToList()
        };

        novoPedido.ValorTotal = novoPedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);

        _context.Pedidos.Add(novoPedido);
        await _context.SaveChangesAsync();

        // Notificação assíncrona via RabbitMQ
        try
        {
            var itensParaEvento = novoPedido.Itens.Select(i => new ItemPedidoEvent
            {
                ProdutoId = i.ProdutoId,
                Quantidade = i.Quantidade
            }).ToList();

            var mensagem = new PedidoCriadoEvent
            {
                PedidoId = novoPedido.Id,
                Itens = itensParaEvento 
            };
            // Publica o evento para o Estoque.API fazer a baixa assíncrona
            _publisher.PublishEvent(mensagem);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AVISO] Falha ao publicar mensagem no RabbitMQ: {ex.Message}. O pedido foi salvo, mas o estoque pode não ter sido baixado.");
        }

        return CreatedAtAction(nameof(ConsultarPedidos), new { id = novoPedido.Id }, novoPedido);
    }
}