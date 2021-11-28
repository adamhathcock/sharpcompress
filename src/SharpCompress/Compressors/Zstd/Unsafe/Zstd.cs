using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static readonly ZSTD_customMem ZSTD_defaultCMem = new ZSTD_customMem
        {
            customAlloc = null,
            customFree = null,
            opaque = null,
        };
    }
}
