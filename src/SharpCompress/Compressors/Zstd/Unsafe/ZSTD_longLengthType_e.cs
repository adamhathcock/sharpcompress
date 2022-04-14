using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_longLengthType_e
    {
        ZSTD_llt_none = 0,
        ZSTD_llt_literalLength = 1,
        ZSTD_llt_matchLength = 2,
    }
}
