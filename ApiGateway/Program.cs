using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar o Ocelot para ler o arquivo ocelot.json
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Configuração do JWT para validação dos tokens no Gateway
// O Ocelot usará esta configuração para validar tokens antes de rotear
var jwtKey = builder.Configuration["AuthenticationProviderKeys:Bearer:Key"] ?? throw new InvalidOperationException("Chave JWT não configurada em ocelot.json.");
var issuer = builder.Configuration["AuthenticationProviderKeys:Bearer:ValidIssuer"] ?? throw new InvalidOperationException("Issuer JWT não configurado em ocelot.json.");
var audience = builder.Configuration["AuthenticationProviderKeys:Bearer:ValidAudiences:0"] ?? throw new InvalidOperationException("Audience JWT não configurado em ocelot.json.");


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
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

builder.Services.AddOcelot();

// Configurações do Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.MapControllers(); 

// Adiciona o middleware do Ocelot no final do pipeline. 
// O .Wait() é necessário pois o método é assíncrono (Task)
app.UseOcelot().Wait();

app.Run();