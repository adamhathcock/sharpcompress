using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_sequencePosition
    {
        /* Index in array of ZSTD_Sequence */
        public uint idx;

        /* Position within sequence at idx */
        public uint posInSequence;

        /* Number of bytes given by sequences provided so far */
        public nuint posInSrc;
    }
}
