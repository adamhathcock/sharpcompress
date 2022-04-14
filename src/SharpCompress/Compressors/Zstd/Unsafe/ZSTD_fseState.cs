using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct ZSTD_fseState
    {
        public nuint state;

        public ZSTD_seqSymbol* table;
    }
}
