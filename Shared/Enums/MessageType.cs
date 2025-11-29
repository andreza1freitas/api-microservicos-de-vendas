namespace Shared.Enums
{
    public enum MessageType
    {
        BaixaEstoque = 0,
        BaixaEstoqueConfirmed = 1,
        BaixaEstoqueFailed = 2,
        BaixaEstoqueDlq = 3,
        EstornoEstoque = 4,
        EstornoEstoqueConfirmed = 5
    }
}