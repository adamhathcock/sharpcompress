using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct rawSeqStore_t
    {
        /* The start of the sequences */
        public rawSeq* seq;

        /* The index in seq where reading stopped. pos <= size. */
        public nuint pos;

        /* The position within the sequence at seq[pos] where reading
                                   stopped. posInSequence <= seq[pos].litLength + seq[pos].matchLength */
        public nuint posInSequence;

        /* The number of sequences. <= capacity. */
        public nuint size;

        /* The capacity starting from `seq` pointer */
        public nuint capacity;
    }
}
