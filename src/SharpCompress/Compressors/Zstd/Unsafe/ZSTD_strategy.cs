using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_strategy
    {
        ZSTD_fast = 1,
        ZSTD_dfast = 2,
        ZSTD_greedy = 3,
        ZSTD_lazy = 4,
        ZSTD_lazy2 = 5,
        ZSTD_btlazy2 = 6,
        ZSTD_btopt = 7,
        ZSTD_btultra = 8,
        ZSTD_btultra2 = 9,
    }
}
