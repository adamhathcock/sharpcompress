namespace ZstdSharp.Unsafe
{
    /*-*******************************************************
     *  Decompression types
     *********************************************************/
    public struct ZSTD_seqSymbol_header
    {
        public uint fastMode;
        public uint tableLog;
    }
}