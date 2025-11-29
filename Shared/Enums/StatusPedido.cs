namespace Vendas.API.Enums
{
    public enum StatusPedido
    {
        AguardandoPagamento = 0,
        Processando = 1,
        Enviado = 2,
        Entregue = 3,
        Cancelado = 4,
        FalhaEstoque = 5
    }
}