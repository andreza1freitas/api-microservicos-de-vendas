using Estoque.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Estoque.API.Models;
using Estoque.API.Services;
using Microsoft.AspNetCore.Authorization; 

[Route("api/[controller]")]
[ApiController]
[Authorize] 
public class EstoqueController : ControllerBase
{
    private readonly EstoqueDbContext _context;
    private readonly IEstoqueService _estoqueService;

    public EstoqueController(EstoqueDbContext context, IEstoqueService estoqueService)
    {
        _context = context;
        _estoqueService = estoqueService;
    }

    // GET: api/Estoque
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Produto>>> GetProdutos()
    {
        return await _context.Produtos.ToListAsync();
    }

    // GET: api/Estoque/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Produto>> GetProduto(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);

        if (produto == null)
        {
            return NotFound();
        }

        return produto;
    }
    
    // GET: api/Estoque/validar?produtoId=1&quantidade=5
    // Endpoint usado pelo Vendas.API para validação síncrona de estoque
    [HttpGet("validar")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidarEstoque([FromQuery] int produtoId, [FromQuery] int quantidade)
    {
        var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == produtoId);

        if (produto == null)
        {
            return NotFound($"Produto ID {produtoId} não encontrado.");
        }

        if (produto.QuantidadeEmEstoque < quantidade)
        {
            return BadRequest($"Estoque insuficiente. Disponível: {produto.QuantidadeEmEstoque}");
        }

        return Ok("Estoque validado com sucesso.");
    }
    
}