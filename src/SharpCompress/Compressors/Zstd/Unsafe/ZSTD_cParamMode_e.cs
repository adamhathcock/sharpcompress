using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_cParamMode_e
    {
        ZSTD_cpm_noAttachDict = 0,
        ZSTD_cpm_attachDict = 1,
        ZSTD_cpm_createCDict = 2,
        ZSTD_cpm_unknown = 3,
    }
}
