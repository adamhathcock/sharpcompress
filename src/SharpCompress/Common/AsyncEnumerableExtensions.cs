using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;

namespace SharpCompress.Common;

/// <summary>
/// Extension methods for async enumerables.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Casts an IAsyncEnumerable to another type.
    /// </summary>
    public static async IAsyncEnumerable<TResult> CastAsync<TResult>(
        this IAsyncEnumerable<object> source
    )
    {
        await foreach (var item in source)
        {
            yield return (TResult)item;
        }
    }

    /// <summary>
    /// Casts an IAsyncEnumerable of TEntry to IAsyncEnumerable of IExtractableArchiveEntry.
    /// </summary>
    public static async IAsyncEnumerable<IExtractableArchiveEntry> CastToExtractableEntry<TEntry>(
        this IAsyncEnumerable<TEntry> source
    )
        where TEntry : IArchiveEntry
    {
        await foreach (var item in source)
        {
            yield return (IExtractableArchiveEntry)(object)item;
        }
    }
}
