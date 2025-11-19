using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Estoque.API.Tests
{
    public static class EstoqueConstants
    {
        public const string PROCESSAMENTO_EXCHANGE = "estoque-exchange";
        public const string BAIXA_ESTOQUE_CONFIRMED_ROUTING_KEY = "baixa-estoque-confirmed";
        public const string BAIXA_ESTOQUE_FAILED_ROUTING_KEY = "baixa-estoque-failed";
        public const string BAIXA_ESTOQUE_DLQ_ROUTING_KEY = "baixa-estoque-dlq";
    }
}