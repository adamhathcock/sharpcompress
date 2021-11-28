using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct ZSTD_blockState_t
    {
        public ZSTD_compressedBlockState_t* prevCBlock;

        public ZSTD_compressedBlockState_t* nextCBlock;

        public ZSTD_matchState_t matchState;
    }
}
