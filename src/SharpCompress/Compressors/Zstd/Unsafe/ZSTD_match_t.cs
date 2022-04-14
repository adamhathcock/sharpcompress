using System;

namespace ZstdSharp.Unsafe
{
    /*********************************
    *  Compression internals structs *
    *********************************/
    public partial struct ZSTD_match_t
    {
        /* Offset code (offset + ZSTD_REP_MOVE) for the match */
        public uint off;

        /* Raw length of match */
        public uint len;
    }
}
