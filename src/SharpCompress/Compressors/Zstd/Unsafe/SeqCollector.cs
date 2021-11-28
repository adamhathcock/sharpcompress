using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct SeqCollector
    {
        public int collectSequences;

        public ZSTD_Sequence* seqStart;

        public nuint seqIndex;

        public nuint maxSequences;
    }
}
