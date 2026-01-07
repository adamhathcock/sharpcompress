using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpCompress;

public static class AsyncEnumerableExtensions
{
    extension<T>(IAsyncEnumerable<T> source)
    {
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
    }
}
