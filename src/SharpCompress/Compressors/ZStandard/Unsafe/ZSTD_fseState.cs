namespace ZstdSharp.Unsafe
{
    public unsafe struct ZSTD_fseState
    {
        public nuint state;
        public ZSTD_seqSymbol* table;
    }
}