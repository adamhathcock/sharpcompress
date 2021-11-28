using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_dictUses_e
    {
        ZSTD_use_indefinitely = -1,
        ZSTD_dont_use = 0,
        ZSTD_use_once = 1,
    }
}
