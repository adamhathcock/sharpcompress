using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SharpCompress.Test;

public class LazyReadOnlyCollectionTests
{
    // Helper class to track how many times items are enumerated from the source
    private class TrackingEnumerable<T> : IEnumerable<T>
    {
        private readonly List<T> _items;
        public int EnumerationCount { get; private set; }
        public int ItemsRequestedCount { get; private set; }

        public TrackingEnumerable(params T[] items)
        {
            _items = new List<T>(items);
        }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return new TrackingEnumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        private class TrackingEnumerator : IEnumerator<T>
        {
            private readonly TrackingEnumerable<T> _parent;
            private int _index = -1;

            public TrackingEnumerator(TrackingEnumerable<T> parent)
            {
                _parent = parent;
            }

            public T Current => _parent._items[_index];

            object? System.Collections.IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _index++;
                if (_index < _parent._items.Count)
                {
                    _parent.ItemsRequestedCount++;
                    return true;
                }
                return false;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose() { }
        }
    }

    [Fact]
    public void BasicEnumeration_IteratesThroughAllItems()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3, 4, 5);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act
        var results = new List<int>();
        foreach (var item in collection)
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(5, source.ItemsRequestedCount);
    }

    [Fact]
    public void MultipleEnumerations_UsesCachedBackingList()
    {
        // Arrange
        var source = new TrackingEnumerable<string>("a", "b", "c");
        var collection = new LazyReadOnlyCollection<string>(source);

        // Act - First enumeration
        var firstResults = new List<string>();
        foreach (var item in collection)
        {
            firstResults.Add(item);
        }

        var itemsRequestedAfterFirst = source.ItemsRequestedCount;

        // Act - Second enumeration
        var secondResults = new List<string>();
        foreach (var item in collection)
        {
            secondResults.Add(item);
        }

        // Assert
        Assert.Equal(firstResults, secondResults);
        Assert.Equal(new[] { "a", "b", "c" }, secondResults);

        // Source should only be enumerated once
        Assert.Equal(1, source.EnumerationCount);

        // Items should only be requested from source during first enumeration
        Assert.Equal(itemsRequestedAfterFirst, source.ItemsRequestedCount);
    }

    [Fact]
    public void EnsureFullyLoaded_LoadsAllItemsIntoBackingList()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(10, 20, 30, 40);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act
        collection.EnsureFullyLoaded();
        var loaded = collection.GetLoaded().ToList();

        // Assert
        Assert.Equal(4, loaded.Count);
        Assert.Equal(new[] { 10, 20, 30, 40 }, loaded);
        Assert.Equal(4, source.ItemsRequestedCount);
    }

    [Fact]
    public void GetLoaded_ReturnsOnlyLoadedItemsBeforeFullEnumeration()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3, 4, 5);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act - Partially enumerate (only first 2 items)
        using var enumerator = collection.GetEnumerator();
        enumerator.MoveNext(); // Load item 1
        enumerator.MoveNext(); // Load item 2

        var loadedItems = collection.GetLoaded().ToList();

        // Continue enumeration
        enumerator.MoveNext(); // Load item 3
        var loadedItemsAfter = collection.GetLoaded().ToList();

        // Assert
        Assert.Equal(2, loadedItems.Count);
        Assert.Equal(new[] { 1, 2 }, loadedItems);

        Assert.Equal(3, loadedItemsAfter.Count);
        Assert.Equal(new[] { 1, 2, 3 }, loadedItemsAfter);
    }

    [Fact]
    public void Count_TriggersFullLoad()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3, 4, 5);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act
        var count = collection.Count;

        // Assert
        Assert.Equal(5, count);
        Assert.Equal(5, source.ItemsRequestedCount);
        Assert.Equal(5, collection.GetLoaded().Count());
    }

    [Fact]
    public void Contains_TriggersFullLoadAndSearches()
    {
        // Arrange
        var source = new TrackingEnumerable<string>("apple", "banana", "cherry");
        var collection = new LazyReadOnlyCollection<string>(source);

        // Act
        var containsBanana = collection.Contains("banana");
        var containsOrange = collection.Contains("orange");

        // Assert
        Assert.True(containsBanana);
        Assert.False(containsOrange);
        Assert.Equal(3, source.ItemsRequestedCount);
        Assert.Equal(3, collection.GetLoaded().Count());
    }

    [Fact]
    public void CopyTo_TriggersFullLoadAndCopiesArray()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(10, 20, 30);
        var collection = new LazyReadOnlyCollection<int>(source);
        var array = new int[5];

        // Act
        collection.CopyTo(array, 1);

        // Assert
        Assert.Equal(0, array[0]);
        Assert.Equal(10, array[1]);
        Assert.Equal(20, array[2]);
        Assert.Equal(30, array[3]);
        Assert.Equal(0, array[4]);
        Assert.Equal(3, source.ItemsRequestedCount);
    }

    [Fact]
    public void EmptySourceEnumerable_ReturnsNoItems()
    {
        // Arrange
        var source = new TrackingEnumerable<int>();
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act
        var results = new List<int>();
        foreach (var item in collection)
        {
            results.Add(item);
        }

        // Assert
        Assert.Empty(results);
        Assert.Equal(0, collection.Count);
        Assert.Equal(1, source.EnumerationCount);
    }

    [Fact]
    public void SingleItemSourceEnumerable_ReturnsSingleItem()
    {
        // Arrange
        var source = new TrackingEnumerable<string>("only");
        var collection = new LazyReadOnlyCollection<string>(source);

        // Act
        var results = new List<string>();
        foreach (var item in collection)
        {
            results.Add(item);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal("only", results[0]);
        Assert.Equal(1, collection.Count);
    }

    [Fact]
    public void PartialEnumeration_ThenGetLoaded_ReturnsOnlyEnumeratedItems()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(10, 20, 30, 40, 50);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act - Enumerate only first 3 items
        using (var enumerator = collection.GetEnumerator())
        {
            var hasMore = enumerator.MoveNext();
            Assert.True(hasMore);
            hasMore = enumerator.MoveNext();
            Assert.True(hasMore);
            hasMore = enumerator.MoveNext();
            Assert.True(hasMore);
        }

        var loadedItems = collection.GetLoaded().ToList();

        // Assert
        Assert.Equal(3, loadedItems.Count);
        Assert.Equal(new[] { 10, 20, 30 }, loadedItems);
        Assert.Equal(3, source.ItemsRequestedCount);
    }

    [Fact]
    public void Reset_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act & Assert
        using var enumerator = collection.GetEnumerator();
        enumerator.MoveNext();
        Assert.Throws<NotSupportedException>(() => enumerator.Reset());
    }

    [Fact]
    public void Dispose_OnEnumerator_CompletesSuccessfully()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act
        using (var enumerator = collection.GetEnumerator())
        {
            enumerator.MoveNext();
            var firstItem = enumerator.Current;
            Assert.Equal(1, firstItem);

            // Dispose is called automatically by using
        }

        // Assert - should be able to enumerate again after disposal
        var results = new List<int>();
        foreach (var item in collection)
        {
            results.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3 }, results);
    }

    [Fact]
    public void IsReadOnly_ReturnsTrue()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act & Assert
        Assert.True(collection.IsReadOnly);
    }

    [Fact]
    public void Add_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => collection.Add(4));
    }

    [Fact]
    public void Clear_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    [Fact]
    public void Remove_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => collection.Remove(1));
    }

    [Fact]
    public void NonGenericEnumerator_WorksCorrectly()
    {
        // Arrange
        var source = new TrackingEnumerable<int>(1, 2, 3);
        var collection = new LazyReadOnlyCollection<int>(source);

        // Act - Use non-generic IEnumerator
        var results = new List<int>();
        var enumerable = (System.Collections.IEnumerable)collection;
        foreach (var item in enumerable)
        {
            results.Add((int)item);
        }

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, results);
    }
}
