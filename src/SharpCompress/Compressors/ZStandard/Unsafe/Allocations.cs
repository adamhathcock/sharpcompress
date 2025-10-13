using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /* custom memory allocation functions */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_customMalloc(nuint size, ZSTD_customMem customMem)
    {
        if (customMem.customAlloc != null)
            return ((delegate* managed<void*, nuint, void*>)customMem.customAlloc)(
                customMem.opaque,
                size
            );
        return malloc(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_customCalloc(nuint size, ZSTD_customMem customMem)
    {
        if (customMem.customAlloc != null)
        {
            /* calloc implemented as malloc+memset;
             * not as efficient as calloc, but next best guess for custom malloc */
            void* ptr = ((delegate* managed<void*, nuint, void*>)customMem.customAlloc)(
                customMem.opaque,
                size
            );
            memset(ptr, 0, (uint)size);
            return ptr;
        }

        return calloc(1, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_customFree(void* ptr, ZSTD_customMem customMem)
    {
        if (ptr != null)
        {
            if (customMem.customFree != null)
                ((delegate* managed<void*, void*, void>)customMem.customFree)(
                    customMem.opaque,
                    ptr
                );
            else
                free(ptr);
        }
    }
}
