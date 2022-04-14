using System;

namespace ZstdSharp.Unsafe
{
    /* Hashset for storing references to multiple ZSTD_DDict within ZSTD_DCtx */
    public unsafe partial struct ZSTD_DDictHashSet
    {
        public ZSTD_DDict_s** ddictPtrTable;

        public nuint ddictPtrTableSize;

        public nuint ddictPtrCount;
    }
}
