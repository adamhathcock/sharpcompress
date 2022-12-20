using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpCompress;

public class ReadOnlyCollection<T> : ICollection<T>
{
    private readonly ICollection<T> collection;

    public ReadOnlyCollection(ICollection<T> collection) => this.collection = collection;

    public void Add(T item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(T item) => collection.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);

    public int Count => collection.Count;

    public bool IsReadOnly => true;

    public bool Remove(T item) => throw new NotSupportedException();

    public IEnumerator<T> GetEnumerator() => collection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
}
