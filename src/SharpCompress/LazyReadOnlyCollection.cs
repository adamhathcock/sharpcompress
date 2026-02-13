using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpCompress;

internal sealed class LazyReadOnlyCollection<T> : ICollection<T>
{
    private readonly List<T> _backing = new();
    private readonly IEnumerator<T> _source;
    private bool _fullyLoaded;

    public LazyReadOnlyCollection(IEnumerable<T> source) => _source = source.GetEnumerator();

    private class LazyLoader : IEnumerator<T>
    {
        private readonly LazyReadOnlyCollection<T> _lazyReadOnlyCollection;
        private bool _disposed;
        private int _index = -1;

        internal LazyLoader(LazyReadOnlyCollection<T> lazyReadOnlyCollection) =>
            _lazyReadOnlyCollection = lazyReadOnlyCollection;

        #region IEnumerator<T> Members

        public T Current => _lazyReadOnlyCollection._backing[_index];

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion

        #region IEnumerator Members

        object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            if (_index + 1 < _lazyReadOnlyCollection._backing.Count)
            {
                _index++;
                return true;
            }
            if (!_lazyReadOnlyCollection._fullyLoaded && _lazyReadOnlyCollection._source.MoveNext())
            {
                _lazyReadOnlyCollection._backing.Add(_lazyReadOnlyCollection._source.Current);
                _index++;
                return true;
            }
            _lazyReadOnlyCollection._fullyLoaded = true;
            return false;
        }

        public void Reset() => throw new NotSupportedException();

        #endregion
    }

    internal void EnsureFullyLoaded()
    {
        if (!_fullyLoaded)
        {
            this.ForEach(x => { });
            _fullyLoaded = true;
        }
    }

    internal IEnumerable<T> GetLoaded() => _backing;

    #region ICollection<T> Members

    public void Add(T item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(T item)
    {
        EnsureFullyLoaded();
        return _backing.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        EnsureFullyLoaded();
        _backing.CopyTo(array, arrayIndex);
    }

    public int Count
    {
        get
        {
            EnsureFullyLoaded();
            return _backing.Count;
        }
    }

    public bool IsReadOnly => true;

    public bool Remove(T item) => throw new NotSupportedException();

    #endregion

    #region IEnumerable<T> Members

    //TODO check for concurrent access
    public IEnumerator<T> GetEnumerator() => new LazyLoader(this);

    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
