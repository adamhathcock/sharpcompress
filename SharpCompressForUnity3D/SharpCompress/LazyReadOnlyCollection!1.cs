namespace SharpCompress
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal class LazyReadOnlyCollection<T> : ICollection<T>, IEnumerable<T>, IEnumerable
    {
        private readonly List<T> backing;
        private bool fullyLoaded;
        private readonly IEnumerator<T> source;

        public LazyReadOnlyCollection(IEnumerable<T> source)
        {
            this.backing = new List<T>();
            this.source = source.GetEnumerator();
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            this.EnsureFullyLoaded();
            return this.backing.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.EnsureFullyLoaded();
            this.backing.CopyTo(array, arrayIndex);
        }

        internal void EnsureFullyLoaded()
        {
            if (!this.fullyLoaded)
            {
                Utility.ForEach<T>(this, delegate (T x) {
                });
                this.fullyLoaded = true;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new LazyLoader(this);//(LazyReadOnlyCollection<T>) this);
        }

        internal IEnumerable<T> GetLoaded()
        {
            return this.backing;
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public int Count
        {
            get
            {
                this.EnsureFullyLoaded();
                return this.backing.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        private class LazyLoader : IEnumerator<T>, IDisposable, IEnumerator
        {
            private bool disposed;
            private int index;
            private readonly LazyReadOnlyCollection<T> lazyReadOnlyCollection;

            internal LazyLoader(LazyReadOnlyCollection<T> lazyReadOnlyCollection)
            {
                this.index = -1;
                this.lazyReadOnlyCollection = lazyReadOnlyCollection;
            }

            public void Dispose()
            {
                if (!this.disposed)
                {
                    this.disposed = true;
                }
            }

            public bool MoveNext()
            {
                if ((this.index + 1) < this.lazyReadOnlyCollection.backing.Count)
                {
                    this.index++;
                    return true;
                }
                if (!(this.lazyReadOnlyCollection.fullyLoaded || !this.lazyReadOnlyCollection.source.MoveNext()))
                {
                    this.lazyReadOnlyCollection.backing.Add(this.lazyReadOnlyCollection.source.Current);
                    this.index++;
                    return true;
                }
                this.lazyReadOnlyCollection.fullyLoaded = true;
                return false;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public T Current
            {
                get
                {
                    return this.lazyReadOnlyCollection.backing[this.index];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }
        }
    }
}

