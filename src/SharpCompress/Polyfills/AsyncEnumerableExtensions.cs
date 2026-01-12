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
    extension<T>(IAsyncEnumerable<T> source)
        where T : notnull
    {
        public async ValueTask<List<T>> ToListAsync()
        {
            var list = new List<T>();
            await foreach (var item in source)
            {
                list.Add(item);
            }
            return list;
        }

        public async IAsyncEnumerable<TResult> CastAsync<TResult>()
            where TResult : class
        {
            await foreach (var item in source)
            {
                yield return (item as TResult).NotNull();
            }
        }

        public async ValueTask<bool> AllAsync(Func<T, bool> predicate)
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

        public async IAsyncEnumerable<T> WhereAsync(Func<T, bool> predicate)
        {
            await foreach (var item in source)
            {
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        public async ValueTask<T> FirstAsync()
        {
            await foreach (var item in source)
            {
                return item; // Returns the very first item found
            }
            throw new InvalidOperationException("The source sequence is empty."); // Throws if the stream is empty
        }

        public async ValueTask<T?> FirstOrDefaultAsync()
        {
            await foreach (var item in source)
            {
                return item; // Returns the very first item found
            }

            return default; // Returns null/default if the stream is empty
        }

        public async ValueTask<TAccumulate> Aggregate<TAccumulate>(
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
    }
}
