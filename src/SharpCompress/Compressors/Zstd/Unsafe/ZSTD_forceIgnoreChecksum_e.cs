using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_forceIgnoreChecksum_e
    {
        ZSTD_d_validateChecksum = 0,
        ZSTD_d_ignoreChecksum = 1,
    }
}
