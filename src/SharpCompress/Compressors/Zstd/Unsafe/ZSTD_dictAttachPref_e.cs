using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_dictAttachPref_e
    {
        ZSTD_dictDefaultAttach = 0,
        ZSTD_dictForceAttach = 1,
        ZSTD_dictForceCopy = 2,
        ZSTD_dictForceLoad = 3,
    }
}
