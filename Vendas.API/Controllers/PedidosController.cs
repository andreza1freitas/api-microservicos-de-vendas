// Vendas.API/Controllers/PedidosController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vendas.API.Data;
using Vendas.API.Models;
using System.Net;
using Shared;

[Route("api/[controller]")]
[ApiController]
public class PedidosController : ControllerBase
{
    private readonly VendasDbContext _context;
    private readonly HttpClient _httpClient; // Para comunicação síncrona com Estoque

    public PedidosController(VendasDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        // Cria uma instância de HttpClient para chamadas externas
        _httpClient = httpClientFactory.CreateClient("EstoqueApiClient"); 
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

        // Validação de Estoque síncrona (CHAMADA HTTP PARA ESTOQUE.API)
        foreach (var item in request.Itens)
        {
            // URL de validação: Ex: http://localhost:5154/api/Produtos/validar?produtoId=1&quantidade=5
            var validationUrl = $"http://localhost:5154/api/Produtos/validar?produtoId={item.ProdutoId}&quantidade={item.Quantidade}";

            var response = await _httpClient.GetAsync(validationUrl);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound($"Produto ID {item.ProdutoId} não encontrado ou URL de Estoque inválida.");
            }
            if (!response.IsSuccessStatusCode)
            {
                // Se o Estoque.API retornar 400 Bad Request, assumimos que é falta de estoque
                var content = await response.Content.ReadAsStringAsync();
                return BadRequest($"Falha na validação de estoque para Produto ID {item.ProdutoId}: {content}");
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

        return CreatedAtAction(nameof(ConsultarPedidos), new { id = novoPedido.Id }, novoPedido);
    }
}