using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Estoque.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Data
{
    public class EstoqueDbContext : DbContext
    {
        public EstoqueDbContext(DbContextOptions<EstoqueDbContext> options)
            : base(options)
        {
        }
        public DbSet<Produto> Produtos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuração para o campo Nome ser único
            modelBuilder.Entity<Produto>().HasIndex(p => p.Nome).IsUnique();

            // Configuração para o campo Preco com precisão de 18 dígitos e 2 casas decimais
            modelBuilder.Entity<Produto>()
                .Property(p => p.Preco)
                .HasPrecision(18, 2);
        }

    }
}