#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress;

internal sealed class LazyAsyncReadOnlyCollection<T>(IAsyncEnumerable<T> source) : IAsyncEnumerable<T>
{
    private readonly List<T> backing = new();
    private readonly IAsyncEnumerator<T> source = source.GetAsyncEnumerator();
    private bool fullyLoaded;

    private class LazyLoader(LazyAsyncReadOnlyCollection<T> lazyReadOnlyCollection, CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        private bool disposed;
        private int index = -1;

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
            }
            return default;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index + 1 < lazyReadOnlyCollection.backing.Count)
            {
                index++;
                return true;
            }
            if (!lazyReadOnlyCollection.fullyLoaded && await lazyReadOnlyCollection.source.MoveNextAsync())
            {
                lazyReadOnlyCollection.backing.Add(lazyReadOnlyCollection.source.Current);
                index++;
                return true;
            }
            lazyReadOnlyCollection.fullyLoaded = true;
            return false;
        }

        #region IEnumerator<T> Members

        public T Current => lazyReadOnlyCollection.backing[index];

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
            }
        }

        #endregion

    }

    internal async ValueTask EnsureFullyLoaded()
    {
        if (!fullyLoaded)
        {
            var loader = new LazyLoader(this, CancellationToken.None);
            while (await loader.MoveNextAsync())
            {
                // Intentionally empty
            }
            fullyLoaded = true;
        }
    }

    internal IEnumerable<T> GetLoaded() => backing;

    #region ICollection<T> Members

    public void Add(T item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool IsReadOnly => true;

    public bool Remove(T item) => throw new NotSupportedException();

    #endregion

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new LazyLoader(this, cancellationToken);
}
