using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace Estoque.API.Tests
{
    /// <summary>
    /// Extensões para facilitar o mocking de DbSet para operações assíncronas do Entity Framework Core.
    /// </summary>
    public static class DbSetMockingExtensions
    {
        public static Mock<DbSet<T>> ConfigureAsyncDbSet<T>(this Mock<DbSet<T>> mockSet, List<T> data) where T : class
        {
            var queryable = data.AsQueryable();

            // Configuração para operações IQueryable síncronas
            mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

            // Configuração para operações IQueryable assíncronas (AsyncEnumerable)
            mockSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

            // Configuração para o provedor de consulta assíncrona
            mockSet.As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

            // Configuração para operações de Mutação (Add, Remove)
            mockSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(item => data.Add(item));
            mockSet.Setup(m => m.Remove(It.IsAny<T>())).Callback<T>(item => data.Remove(item));

            return mockSet;
        }
    }

    // Classes de suporte para simular IAsyncEnumerable e IAsyncQueryProvider
    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
        
        // Cuidado com a anulação: se _inner for nulo (apenas por segurança)
        public T Current => _inner.Current; 
        
        public ValueTask DisposeAsync() => new ValueTask(Task.CompletedTask);
        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());
    }

    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(System.Linq.Expressions.Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression) => new TestAsyncEnumerable<TElement>(expression);

        public object Execute(System.Linq.Expressions.Expression expression) => _inner.Execute(expression)!;

        public TResult Execute<TResult>(System.Linq.Expressions.Expression expression) => _inner.Execute<TResult>(expression)!;

        public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
        {
            // O tipo de retorno esperado (ex: 'Produto' se TResult for 'Task<Produto>')
            var expectedResultType = typeof(TResult).GetGenericArguments().First(); 
            
            // Encontra e invoca o método 'Execute' síncrono no provedor real (o IQueryable.Provider mockado)
            var executionResult = typeof(IQueryProvider)
                .GetMethods()
                .Single(method => method.Name == "Execute" && method.IsGenericMethod)
                .MakeGenericMethod(expectedResultType)
                .Invoke(_inner, new object[] { expression });
            
            // O ExecuteAsync do EF Core retorna um Task<T>. 
            // Para simular isso, usamos Reflection para chamar Task.FromResult<T>(executionResult).
            
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))! // ! Garante que GetMethod não retorna null
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!; // ! Garante que Invoke não retorna null antes do cast
        }

        internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
        {
            public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                // Usa o enumerador síncrono para simular o comportamento assíncrono
                return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
            }
        }
    }
}