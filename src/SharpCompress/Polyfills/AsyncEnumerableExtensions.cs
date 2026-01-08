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

public static class AsyncEnumerableExtensions
{
    extension<T>(IAsyncEnumerable<T> source)
        where T : notnull
    {
        public async IAsyncEnumerable<TResult> Cast<TResult>()
            where TResult : class
        {
            await foreach (var item in source)
            {
                yield return (item as TResult).NotNull();
            }
        }
        public async ValueTask<bool> All(Func<T, bool> predicate)
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

        public async ValueTask<T?> FirstOrDefaultAsync()
        {
            await foreach (var item in source)
            {
                return item; // Returns the very first item found
            }

            return default; // Returns null/default if the stream is empty
        }

        public  async ValueTask<TAccumulate> Aggregate<TAccumulate>(TAccumulate seed, Func<TAccumulate, T, TAccumulate> func)
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
