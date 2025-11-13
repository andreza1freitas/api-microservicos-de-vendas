using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Estoque.API.Models;

namespace Estoque.API.Services
{
    public interface IEstoqueService
    {
        Task ProcessarBaixaEstoqueAsync(PedidoCriadoMessage message);
    }
}