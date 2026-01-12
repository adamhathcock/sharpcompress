using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharpCompress;

public static class AsyncEnumerableEx
{
    public static async IAsyncEnumerable<T> Empty<T>()
        where T : notnull
    {
        await Task.CompletedTask;
        yield break;
    }
}

public static class EnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        await Task.CompletedTask;
        foreach (var item in source)
        {
            yield return item;
        }
    }
}

public static class AsyncEnumerableExtensions
{
    public static async ValueTask<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    public static async IAsyncEnumerable<TResult> CastAsync<TResult>(
        this IAsyncEnumerable<object?> source
    )
        where TResult : class
    {
        await foreach (var item in source)
        {
            yield return (item as TResult).NotNull();
        }
    }

    public static async ValueTask<TAccumulate> AggregateAsync<TAccumulate, T>(
        this IAsyncEnumerable<T> source,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> func
    )
    {
        TAccumulate result = seed;
        await foreach (var element in source)
        {
            result = func(result, element);
        }
        return result;
    }

    public static async ValueTask<bool> AllAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate
    )
    {
        await foreach (var item in source)
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    public static IAsyncEnumerable<T> Where<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate
    )
    {
        return WhereIterator(source, predicate);
    }

    private static async IAsyncEnumerable<T> WhereIterator<T>(
        IAsyncEnumerable<T> source,
        Func<T, bool> predicate
    )
    {
        await foreach (var item in source)
        {
            if (predicate(item))
            {
                yield return item;
            }
        }
    }

    public static async IAsyncEnumerable<T> WhereAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate
    )
    {
        await foreach (var item in source)
        {
            if (predicate(item))
            {
                yield return item;
            }
        }
    }

    public static async ValueTask<T> SingleAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate
    )
    {
        var enumerator = source.WhereAsync(predicate).GetAsyncEnumerator();
        if (!await enumerator.MoveNextAsync())
        {
            throw new InvalidOperationException("The source sequence is empty.");
        }
        var value =   enumerator.Current;
        if (await enumerator.MoveNextAsync())
        {
            throw new InvalidOperationException("The source sequence contains more than one element.");
        }
        return value;
    }

    public static async ValueTask<T> FirstAsync<T>(this IAsyncEnumerable<T> source)
    {
        await foreach (var item in source)
        {
            return item;
        }
        throw new InvalidOperationException("The source sequence is empty.");
    }

    public static async ValueTask<T?> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> source)
    {
        await foreach (var item in source)
        {
            return item;
        }

        return default;
    }
}
