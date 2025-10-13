namespace ZstdSharp.Unsafe
{
    public unsafe struct SeqCollector
    {
        public int collectSequences;
        public ZSTD_Sequence* seqStart;
        public nuint seqIndex;
        public nuint maxSequences;
    }
}