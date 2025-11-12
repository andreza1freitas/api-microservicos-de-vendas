using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vendas.API.Data;
using Vendas.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuração do Json Serializer
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Resolve o erro de ciclo de objeto (JsonException)
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Registro do RabbitMQPublisher como serviço singleton
builder.Services.AddSingleton<IMessageBusPublisher, RabbitMQPublisher>();

// Configuração do HttpClient para comunicação com Estoque.API
builder.Services.AddHttpClient("EstoqueApiClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5154/");
});

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