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

        // POST api/produtos - Cadastrar Produto
        [HttpPost]
        public async Task<ActionResult<Produto>> CadastrarProduto(Produto produto)
        {
            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(ConsultarProdutoPorId), new { id = produto.Id }, produto);
        }

        // GET api/produtos - Consultar todos os produtos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Produto>>> ConsultarProdutos()
        {
            return await _context.Produtos.ToListAsync();
        }

        // GET api/produtos/5 - Consultar produto por ID
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

        // PUT api/produtos/5 - Atualizar Produto
        [HttpPut("{id}")]
        public async Task<IActionResult> AtualizarProduto(int id, Produto produtoAtualizado)
        {
            if (id != produtoAtualizado.Id)
            {
                return BadRequest("O ID na rota e no corpo da requisição devem ser iguais.");
            }

            var produtoExistente = await _context.Produtos.FindAsync(id);
            if (produtoExistente == null)
            {
                return NotFound($"Produto com ID {id} não encontrado.");
            }

            // Garante que o ID não será alterado
            produtoAtualizado.Id = id;

            // Atualiza as propriedades do produto
            _context.Entry(produtoExistente).CurrentValues.SetValues(produtoAtualizado);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Produtos.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Retorna Ok com mensagem de sucesso
            return Ok(new { mensagem = $"Produto ID {id} atualizado com sucesso." });
        }

        // DELETE api/produtos/5 - Excluir Produto
        [HttpDelete("{id}")]
        public async Task<IActionResult> ExcluirProduto(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null)
            {
                return NotFound($"Produto com ID {id} não encontrado para exclusão.");
            }

            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();

            // Retorna Ok com mensagem de sucesso
            return Ok(new { mensagem = $"Produto ID {id} excluído com sucesso." });
        }

        // GET api/produtos/validar?produtoId=5&quantidade=2
        [HttpGet("validar")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidarEstoque(int produtoId, int quantidade)
        {
            var produto = await _context.Produtos.FindAsync(produtoId);

            if (produto is null)
                return NotFound($"Produto ID {produtoId} não encontrado.");

            if (produto.QuantidadeEmEstoque < quantidade)
                return BadRequest($"Estoque insuficiente. Disponível: {produto.QuantidadeEmEstoque}. Requerido: {quantidade}.");

            return Ok(new { mensagem = "Estoque validado com sucesso.", status = true });
        }
    }

}