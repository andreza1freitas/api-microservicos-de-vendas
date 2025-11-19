using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Messaging
{
    // Classe para cada item a ser baixado no estoque.
    public class BaixaEstoqueItemMessage
    {
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
    }
}