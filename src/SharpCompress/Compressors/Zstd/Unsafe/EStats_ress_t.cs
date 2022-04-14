using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct EStats_ress_t
    {
        /* dictionary */
        public ZSTD_CDict_s* dict;

        /* working context */
        public ZSTD_CCtx_s* zc;

        /* must be ZSTD_BLOCKSIZE_MAX allocated */
        public void* workPlace;
    }
}
