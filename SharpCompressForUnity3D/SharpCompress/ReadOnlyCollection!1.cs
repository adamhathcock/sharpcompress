namespace SharpCompress
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal class ReadOnlyCollection<T> : ICollection<T>, IEnumerable<T>, IEnumerable
    {
        private ICollection<T> collection;

        public ReadOnlyCollection(ICollection<T> collection)
        {
            this.collection = collection;
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
            return this.collection.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.collection.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.collection.GetEnumerator();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotSupportedException();
        }

        public int Count
        {
            get
            {
                return this.collection.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }
    }
}

