using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress;

public static class AsyncEnumerableEx
{
    public static async IAsyncEnumerable<T> Empty<T>()
        where T : notnull
    {
        await Task.Yield();
        yield break;
    }
}

public static class EnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        await Task.Yield();
        foreach (var item in source)
        {
            yield return item;
        }
    }
}

public static class AsyncEnumerableExtensions
{
#if !NET10_0_OR_GREATER
    extension<T>(IAsyncEnumerable<T> source)
    {
        public async IAsyncEnumerable<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            await foreach (var element in source)
            {
                yield return selector(element);
            }
        }

        public async ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        {
            await using var e = source.GetAsyncEnumerator(cancellationToken);

            var count = 0;
            while (await e.MoveNextAsync().ConfigureAwait(false))
            {
                checked
                {
                    count++;
                }
            }

            return count;
        }

        public async IAsyncEnumerable<T> Take(int count)
        {
            await foreach (var element in source)
            {
                yield return element;

                if (--count == 0)
                {
                    break;
                }
            }
        }

        public async ValueTask<List<T>> ToListAsync()
        {
            var list = new List<T>();
            await foreach (var item in source)
            {
                list.Add(item);
            }
            return list;
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

        public async IAsyncEnumerable<T> Where(Func<T, bool> predicate)
        {
            await foreach (var item in source)
            {
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        public async ValueTask<T> SingleAsync(Func<T, bool>? predicate = null)
        {
            IAsyncEnumerator<T> enumerator;
            if (predicate is null)
            {
                enumerator = source.GetAsyncEnumerator();
            }
            else
            {
                enumerator = source.Where(predicate).GetAsyncEnumerator();
            }

            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                throw new ArchiveOperationException("The source sequence is empty.");
            }
            var value = enumerator.Current;
            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                throw new ArchiveOperationException(
                    "The source sequence contains more than one element."
                );
            }
            return value;
        }

        public async ValueTask<T> FirstAsync()
        {
            await foreach (var item in source)
            {
                return item;
            }
            throw new ArchiveOperationException("The source sequence is empty.");
        }

        public async ValueTask<T?> FirstOrDefaultAsync(
            CancellationToken cancellationToken = default
        )
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                return item;
            }

            return default;
        }
    }
#endif

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
        var result = seed;
        await foreach (var element in source)
        {
            result = func(result, element);
        }
        return result;
    }
}
