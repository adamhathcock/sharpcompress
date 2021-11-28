using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct ZSTD_customMem
    {
        public void* customAlloc;

        public void* customFree;

        public void* opaque;
    }
}
