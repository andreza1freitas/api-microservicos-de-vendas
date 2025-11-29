using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vendas.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Vendas.API.Data
{
    public class VendasDbContext : DbContext
    {
        public VendasDbContext(DbContextOptions<VendasDbContext> options)
            : base(options)
        {
        }

        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<ItemPedido> ItensPedido { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ItemPedido>()
                .HasOne(ip => ip.Pedido)
                .WithMany(p => p.Items)
                .HasForeignKey(ip => ip.PedidoId);

            // Configuração de precisão do decimal
            modelBuilder.Entity<Pedido>().Property(p => p.ValorTotal).HasPrecision(18, 2);
            modelBuilder.Entity<ItemPedido>().Property(ip => ip.PrecoUnitario).HasPrecision(18, 2);
        }
    }
}