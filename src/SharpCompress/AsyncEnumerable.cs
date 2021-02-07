using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress
{
    public static class AsyncEnumerable
    {
        public static IAsyncEnumerable<T> Empty<T>() => EmptyAsyncEnumerable<T>.Instance;
        

        private class EmptyAsyncEnumerable<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
        {
            public static readonly EmptyAsyncEnumerable<T> Instance = 
                new();
            public T Current => default!;
            public ValueTask DisposeAsync() => default;
            public ValueTask<bool> MoveNextAsync() => new(false);
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
            {
                return this;
            }
        }
    }
}