using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendas.API.Migrations
{
    /// <inheritdoc />
    public partial class AtualizarParaGuidPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- PASSO 1: Preparação da Tabela ItensPedido (DROP/ADD COLUMN) ---

            // 1a. Remove a restrição de Chave Estrangeira (FK) de ItensPedido.PedidoId
            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoId",
                table: "ItensPedido");

            // 1b. Exclui todos os registros para garantir que não haja dados para o EF Core tentar converter.
            migrationBuilder.Sql("DELETE FROM [ItensPedido];");

            // 1c. Remove o índice existente que estava causando o primeiro erro no lote de comandos.
            migrationBuilder.DropIndex(
                name: "IX_ItensPedido_PedidoId",
                table: "ItensPedido");

            // 1d. Remove a coluna antiga (int)
            migrationBuilder.DropColumn(
                name: "PedidoId",
                table: "ItensPedido");

            // 1e. Adiciona a nova coluna (Guid)
            migrationBuilder.AddColumn<Guid>(
                name: "PedidoId",
                table: "ItensPedido",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")); // Adiciona um valor padrão temporário


            // --- PASSO 2: Alteração da Tabela Pedidos ---

            // 2a. Remove a Chave Primária (PK) de Pedidos
            migrationBuilder.DropPrimaryKey(
                name: "PK_Pedidos",
                table: "Pedidos");

            // 2b. Remove a coluna Id antiga (int IDENTITY)
            migrationBuilder.DropColumn(
                name: "Id",
                table: "Pedidos");

            // 2c. Adiciona a nova coluna Id (Guid, com valor padrão NEWID())
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Pedidos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            // 2d. Recria a Chave Primária na nova coluna Id (Guid)
            migrationBuilder.AddPrimaryKey(
                name: "PK_Pedidos",
                table: "Pedidos",
                column: "Id");

            // --- PASSO 3: Recriar o Índice e a Chave Estrangeira (FK) ---

            // 3a. Recria o Índice no novo PedidoId (Guid)
            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_PedidoId",
                table: "ItensPedido",
                column: "PedidoId");
            
            // 3b. Adiciona a restrição de Chave Estrangeira (FK), usando os novos tipos Guid
            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoId",
                table: "ItensPedido",
                column: "PedidoId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // *** O método Down foi mantido como estava na correção anterior para permitir o rollback. ***
            
            // --- REVERSÃO PASSO 1: Remover FK e converter ItensPedido.PedidoId de volta para int ---

            // 1a. Remove a restrição de Chave Estrangeira (FK)
            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoId",
                table: "ItensPedido");

            // 1b. Remove o índice
            migrationBuilder.DropIndex(
                name: "IX_ItensPedido_PedidoId",
                table: "ItensPedido");

            // 1c. Exclui os registros para permitir a alteração de volta
            migrationBuilder.Sql("DELETE FROM [ItensPedido];");
            
            // 1d. Remove a coluna Guid
            migrationBuilder.DropColumn(
                name: "PedidoId",
                table: "ItensPedido");
            
            migrationBuilder.AddColumn<int>(
                name: "PedidoId",
                table: "ItensPedido",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropPrimaryKey(
                name: "PK_Pedidos",
                table: "Pedidos");
            
            migrationBuilder.DropColumn(
                name: "Id",
                table: "Pedidos");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Pedidos",
                type: "int",
                nullable: false,
                defaultValue: 0) 
                .Annotation("SqlServer:Identity", "1, 1"); 

            migrationBuilder.AddPrimaryKey(
                name: "PK_Pedidos",
                table: "Pedidos",
                column: "Id");
            
            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_PedidoId",
                table: "ItensPedido",
                column: "PedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoId",
                table: "ItensPedido",
                column: "PedidoId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}