using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_dictContentType_e
    {
        ZSTD_dct_auto = 0,
        ZSTD_dct_rawContent = 1,
        ZSTD_dct_fullDict = 2,
    }
}
