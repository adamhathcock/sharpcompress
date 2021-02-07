using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress
{
    internal sealed class LazyReadOnlyCollection<T> : IAsyncEnumerable<T>
    {
        private readonly List<T> backing = new();
        private IAsyncEnumerator<T>? enumerator;
        private readonly IAsyncEnumerable<T> enumerable;
        private bool fullyLoaded;

        public LazyReadOnlyCollection(IAsyncEnumerable<T> source)
        {
            enumerable = source;
        }

        private IAsyncEnumerator<T> GetEnumerator()
        {
            if (enumerator is null)
            {
                enumerator = enumerable.GetAsyncEnumerator();
            }
            return enumerator;
        }

        private class LazyLoader : IAsyncEnumerator<T>
        {
            private readonly LazyReadOnlyCollection<T> lazyReadOnlyCollection;
            private bool disposed;
            private int index = -1;

            internal LazyLoader(LazyReadOnlyCollection<T> lazyReadOnlyCollection)
            {
                this.lazyReadOnlyCollection = lazyReadOnlyCollection;
            }

            public T Current => lazyReadOnlyCollection.backing[index];
            
            public ValueTask DisposeAsync()
            {
                if (!disposed)
                {
                    disposed = true;
                }
                return new ValueTask(Task.CompletedTask);
            }


            public async ValueTask<bool> MoveNextAsync()
            {
                if (index + 1 < lazyReadOnlyCollection.backing.Count)
                {
                    index++;
                    return true;
                }
                if (!lazyReadOnlyCollection.fullyLoaded && await lazyReadOnlyCollection.GetEnumerator().MoveNextAsync())
                {
                    lazyReadOnlyCollection.backing.Add(lazyReadOnlyCollection.GetEnumerator().Current);
                    index++;
                    return true;
                }
                lazyReadOnlyCollection.fullyLoaded = true;
                return false;
            }
        }

        internal async ValueTask EnsureFullyLoaded()
        {
            if (!fullyLoaded)
            {
                await foreach (var x in this)
                {
                }
                fullyLoaded = true;
            }
        }

        internal IEnumerable<T> GetLoaded()
        {
            return backing;
        }

        #region IEnumerable<T> Members

        //TODO check for concurrent access
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return new LazyLoader(this);
        }

        #endregion
    }
}
