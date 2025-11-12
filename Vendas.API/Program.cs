using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vendas.API.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuração do DbContext com SQL Server
builder.Services.AddDbContext<VendasDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VendasDbConnection")));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();