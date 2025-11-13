using Estoque.API.Data;
using Estoque.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registro do EstoqueService
builder.Services.AddScoped<IEstoqueService, EstoqueService>();

// Registro do RabbitMQConsumer como serviço hospedado
builder.Services.AddHostedService<RabbitMQConsumer>();

builder.Services.AddDbContext<EstoqueDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EstoqueDbConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.MapControllers();

app.Run();

