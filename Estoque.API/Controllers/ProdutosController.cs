using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Estoque.API.Data;
using Estoque.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase
    {
        private readonly EstoqueDbContext _context;

        public ProdutosController(EstoqueDbContext context)
        {
            _context = context;
        }

        // POST api/produtos
        [HttpPost]
        public async Task<ActionResult<Produto>> CadastrarProduto(Produto produto)
        {
            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(ConsultarProdutoPorId), new { id = produto.Id }, produto);
        }

        // GET api/produtos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Produto>>> ConsultarProdutos()
        {
            return await _context.Produtos.ToListAsync();
        }

        // GET api/produtos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Produto>> ConsultarProdutoPorId(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);

            if (produto == null)
            {
                return NotFound();
            }

            return produto;
        }

        // GET api/produtos/validar?produtoId=5&quantidade=2
        [HttpGet("/api/Produtos/validar")]
        public async Task<IActionResult> ValidarEstoque(int produtoId, int quantidade)
        {
            var produto = await _context.Produtos.FindAsync(produtoId);

            if (produto is null)
                return NotFound($"Produto ID {produtoId} não encontrado.");

            if (produto.QuantidadeEmEstoque < quantidade)
                return BadRequest($"Estoque insuficiente. Disponível: {produto.QuantidadeEmEstoque}. Requerido: {quantidade}.");

            return Ok(true);
        }
    }
}