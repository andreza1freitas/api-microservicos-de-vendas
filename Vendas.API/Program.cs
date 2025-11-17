using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Vendas.API.Data;
using Vendas.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuração do Json Serializer
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Resolve o erro de ciclo de objeto (JsonException)
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Define a segurança do Swagger (O campo 'Authorize' será adicionado no topo da UI)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT no formato: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    // Define que as rotas com [Authorize] usarão a definição 'Bearer' acima
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Configuração do DbContext com Banco de Dados SQL Server
builder.Services.AddDbContext<VendasDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VendasDbConnection")));

// Configuração do HttpClient para comunicação com Estoque.API
builder.Services.AddHttpClient("EstoqueApiClient", client =>
{
    // A BaseAddress deve ser a raiz do serviço de destino
    var baseUrl = builder.Configuration["HttpClientSettings:EstoqueApiBaseUrl"] 
        ?? throw new InvalidOperationException("HttpClientSettings:EstoqueApiBaseUrl não configurada.");
    client.BaseAddress = new Uri(baseUrl); 
});

// Registro do serviço publicador de mensagens RabbitMQ
builder.Services.AddSingleton<IMessageBusPublisher, RabbitMQPublisher>();

// CONFIGURAÇÃO JWT START (Autenticação)
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurada.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer não configurado.");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience não configurado.");


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Inicializa o banco de dados
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<VendasDbContext>();
        db.Database.EnsureCreated();

    }
}

// Adiciona o middleware de autenticação e autorização JWT
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();