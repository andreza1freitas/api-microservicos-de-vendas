using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Vendas.API.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirTipoStatusParaInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O valor do Enum 'Processando' é, por padrão, 0 (se for o primeiro item). 
            // Se você definiu explicitamente como 1, altere o valor '0' abaixo.

            // --- PASSO 1: Garantir que a coluna aceite NULL temporariamente ---
            // Isso pode ser necessário se houver restrições, mas vamos focar na limpeza de dados.
            // Para simplificar, focamos na limpeza direta.

            // --- PASSO 2: Limpar/Corrigir Dados (MANDATÓRIO) ---
            // Atualiza todas as linhas que contêm o texto 'Processando' para o valor inteiro 0 (ou o valor do seu Enum 'Processando').
            // Se houver outros status (Ex: 'Cancelado', 'Entregue'), você deve adicionar um comando de UPDATE para cada um.
            migrationBuilder.Sql("UPDATE Pedidos SET Status = '0' WHERE Status = 'Processando'");
            
            // Se houverem registros com outros valores (ex: 'Pendente', 'Concluido'), adicione:
            // migrationBuilder.Sql("UPDATE Pedidos SET Status = '1' WHERE Status = 'Pendente'"); 
            // migrationBuilder.Sql("UPDATE Pedidos SET Status = '2' WHERE Status = 'Concluido'"); 
            // IMPORTANTE: Se houver valores NULL (vazios), eles também causarão erro, a menos que você os trate:
            // migrationBuilder.Sql("UPDATE Pedidos SET Status = '0' WHERE Status IS NULL");


            // --- PASSO 3: Alterar o Tipo da Coluna ---
            // Agora que a coluna só tem valores que o SQL consegue converter (strings que representam números '0', '1', etc.), a conversão para INT funcionará.
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Pedidos",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O método Down() reverte o tipo, mas precisa tratar os dados se a reversão do tipo falhar.
            // Aqui, apenas revertemos o tipo, assumindo que você não terá textos após a reversão imediata.
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}