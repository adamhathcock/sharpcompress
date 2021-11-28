using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_useRowMatchFinderMode_e
    {
        ZSTD_urm_auto = 0,
        ZSTD_urm_disableRowMatchFinder = 1,
        ZSTD_urm_enableRowMatchFinder = 2,
    }
}
