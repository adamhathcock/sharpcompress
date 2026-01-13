using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SharpCompress.Test;

public class LazyAsyncReadOnlyCollectionTests
{
    // Helper class to track how many times items are enumerated from the source
    private class TrackingAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly List<T> _items;
        public int EnumerationCount { get; private set; }
        public int ItemsRequestedCount { get; private set; }

        public TrackingAsyncEnumerable(params T[] items)
        {
            _items = new List<T>(items);
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            EnumerationCount++;
            return new TrackingEnumerator(this, cancellationToken);
        }

        private class TrackingEnumerator : IAsyncEnumerator<T>
        {
            private readonly TrackingAsyncEnumerable<T> _parent;
            private readonly CancellationToken _cancellationToken;
            private int _index = -1;

            public TrackingEnumerator(
                TrackingAsyncEnumerable<T> parent,
                CancellationToken cancellationToken
            )
            {
                _parent = parent;
                _cancellationToken = cancellationToken;
            }

            public T Current => _parent._items[_index];

            public async ValueTask<bool> MoveNextAsync()
            {
                _cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield(); // Simulate async behavior
                _index++;
                if (_index < _parent._items.Count)
                {
                    _parent.ItemsRequestedCount++;
                    return true;
                }
                return false;
            }

            public ValueTask DisposeAsync() => default;
        }
    }

    [Fact]
    public async Task BasicEnumeration_IteratesThroughAllItems()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>(1, 2, 3, 4, 5);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act
        var results = new List<int>();
        await foreach (var item in collection)
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
    public async Task MultipleEnumerations_UsesCachedBackingList()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<string>("a", "b", "c");
        var collection = new LazyAsyncReadOnlyCollection<string>(source);

        // Act - First enumeration
        var firstResults = new List<string>();
        await foreach (var item in collection)
        {
            firstResults.Add(item);
        }

        var itemsRequestedAfterFirst = source.ItemsRequestedCount;

        // Act - Second enumeration
        var secondResults = new List<string>();
        await foreach (var item in collection)
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
    public async Task EnsureFullyLoaded_LoadsAllItemsIntoBackingList()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>(10, 20, 30, 40);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act
        await collection.EnsureFullyLoaded();
        var loaded = collection.GetLoaded().ToList();

        // Assert
        Assert.Equal(4, loaded.Count);
        Assert.Equal(new[] { 10, 20, 30, 40 }, loaded);
        Assert.Equal(4, source.ItemsRequestedCount);
    }

    [Fact]
    public async Task GetLoaded_ReturnsOnlyLoadedItemsBeforeFullEnumeration()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>(1, 2, 3, 4, 5);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act - Partially enumerate (only first 2 items)
        var enumerator = collection.GetAsyncEnumerator();
        await enumerator.MoveNextAsync(); // Load item 1
        await enumerator.MoveNextAsync(); // Load item 2

        var loadedItems = collection.GetLoaded().ToList();

        // Continue enumeration
        await enumerator.MoveNextAsync(); // Load item 3
        var loadedItemsAfter = collection.GetLoaded().ToList();

        await enumerator.DisposeAsync();

        // Assert
        Assert.Equal(2, loadedItems.Count);
        Assert.Equal(new[] { 1, 2 }, loadedItems);

        Assert.Equal(3, loadedItemsAfter.Count);
        Assert.Equal(new[] { 1, 2, 3 }, loadedItemsAfter);
    }

    [Fact]
    public async Task CancellationToken_PassedToGetAsyncEnumerator_HonorsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var source = new TrackingAsyncEnumerable<int>(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act & Assert
        var results = new List<int>();
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in collection.WithCancellation(cts.Token))
            {
                results.Add(item);
                if (item == 3)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 1, 2, 3 }, results);
    }

    [Fact]
    public async Task CancellationDuringMoveNextAsync_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var source = CreateDelayedAsyncEnumerable(
            new[] { 1, 2, 3, 4, 5 },
            TimeSpan.FromMilliseconds(50)
        );
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            var enumerator = collection.GetAsyncEnumerator(cts.Token);
            await enumerator.MoveNextAsync();
            await enumerator.MoveNextAsync();

            cts.Cancel();

            await enumerator.MoveNextAsync(); // Should throw
        });
    }

    [Fact]
    public async Task EmptySourceEnumerable_ReturnsNoItems()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>();
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act
        var results = new List<int>();
        await foreach (var item in collection)
        {
            results.Add(item);
        }

        // Assert
        Assert.Empty(results);
        Assert.Equal(1, source.EnumerationCount);
    }

    [Fact]
    public async Task SingleItemSourceEnumerable_ReturnsSingleItem()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<string>("only");
        var collection = new LazyAsyncReadOnlyCollection<string>(source);

        // Act
        var results = new List<string>();
        await foreach (var item in collection)
        {
            results.Add(item);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal("only", results[0]);
    }

    [Fact]
    public async Task PartialEnumeration_ThenGetLoaded_ReturnsOnlyEnumeratedItems()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>(10, 20, 30, 40, 50);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act - Enumerate only first 3 items
        await using (var enumerator = collection.GetAsyncEnumerator())
        {
            var hasMore = await enumerator.MoveNextAsync();
            Assert.True(hasMore);
            hasMore = await enumerator.MoveNextAsync();
            Assert.True(hasMore);
            hasMore = await enumerator.MoveNextAsync();
            Assert.True(hasMore);
        }

        var loadedItems = collection.GetLoaded().ToList();

        // Assert
        Assert.Equal(3, loadedItems.Count);
        Assert.Equal(new[] { 10, 20, 30 }, loadedItems);
        Assert.Equal(3, source.ItemsRequestedCount);
    }

    [Fact]
    public async Task ConcurrentEnumerations_ShareBackingList()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>(1, 2, 3, 4, 5);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act - Fully load the collection first, then enumerate from two threads
        await collection.EnsureFullyLoaded();

        var task1 = Task.Run(async () =>
        {
            var results = new List<int>();
            await foreach (var item in collection)
            {
                results.Add(item);
                await Task.Delay(5);
            }
            return results;
        });

        var task2 = Task.Run(async () =>
        {
            var results = new List<int>();
            await foreach (var item in collection)
            {
                results.Add(item);
                await Task.Delay(5);
            }
            return results;
        });

        var results1 = await task1;
        var results2 = await task2;

        // Assert - Both enumerations should see all items from the shared backing list
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results1);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results2);
        Assert.Equal(5, source.ItemsRequestedCount);
    }

    [Fact]
    public async Task DisposeAsync_OnLazyLoader_CompletesSuccessfully()
    {
        // Arrange
        var source = new TrackingAsyncEnumerable<int>(1, 2, 3);
        var collection = new LazyAsyncReadOnlyCollection<int>(source);

        // Act
        await using (var enumerator = collection.GetAsyncEnumerator())
        {
            await enumerator.MoveNextAsync();
            var firstItem = enumerator.Current;
            Assert.Equal(1, firstItem);

            // Dispose is called automatically by await using
        }

        // Assert - should be able to enumerate again after disposal
        var results = new List<int>();
        await foreach (var item in collection)
        {
            results.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3 }, results);
    }

    // Helper method to create an async enumerable with delays
    private static async IAsyncEnumerable<T> CreateDelayedAsyncEnumerable<T>(
        IEnumerable<T> items,
        TimeSpan delay
    )
    {
        foreach (var item in items)
        {
            await Task.Delay(delay);
            yield return item;
        }
    }
}
