using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Vendas.API.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HomeVendasController : Controller
    {
        [HttpGet("/")]
        public ContentResult Index()
        {
            string serviceName = "Microserviço de Gestão de Vendas (Vendas.API)";
            
            string swaggerUrl = Url.Content("~/swagger");

            string htmlContent = $@"
                <!DOCTYPE html>
                <html lang='pt-BR'>
                <head>
                    <meta charset='utf-8' />
                    <title>{serviceName}</title>
                    <style>
                        body {{ font-family: 'Arial', sans-serif; background-color: #f4f7f9; text-align: center; padding-top: 200px; }}
                        .container {{ background: #ffffff; padding: 80px; border-radius: 12px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); display: inline-block; }}
                        h1 {{ color: #007bff; }}
                        .btn {{ 
                            display: inline-block; 
                            margin-top: 20px; 
                            padding: 10px 20px; 
                            background-color: #7cb342; 
                            color: white; 
                            text-decoration: none; 
                            border-radius: 8px; 
                            font-weight: bold;
                            transition: background-color 0.3s;
                        }}
                        .btn:hover {{ background-color: #218838; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>🟢 {serviceName} está Online!</h1>
                        <p>Acesse a documentação interativa para testar os endpoints.</p>
                        <a href='{swaggerUrl}' class='btn'>Acessar Swagger UI</a>
                    </div>
                </body>
                </html>";

            return Content(htmlContent, "text/html");
        }
    }
}