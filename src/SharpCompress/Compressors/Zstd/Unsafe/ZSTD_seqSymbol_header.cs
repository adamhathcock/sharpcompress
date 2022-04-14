using System;

namespace ZstdSharp.Unsafe
{
    /*-*******************************************************
     *  Decompression types
     *********************************************************/
    public partial struct ZSTD_seqSymbol_header
    {
        public uint fastMode;

        public uint tableLog;
    }
}
