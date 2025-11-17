using Microsoft.AspNetCore.Mvc;
using Auth.API.Models;
using Auth.API.Services;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;

    public AuthController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginModel model)
    {
        // Simulação da autenticação (Verificação de credenciais)
        if (model.Username != "admin" || model.Password != "123456")
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        // Geração do Token
        var token = _tokenService.GenerateToken(model.Username);
        
        // Retorna o token
        return Ok(new { token }); 
    }
}