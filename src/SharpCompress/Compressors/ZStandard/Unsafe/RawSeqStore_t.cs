namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct RawSeqStore_t
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

    public RawSeqStore_t(rawSeq* seq, nuint pos, nuint posInSequence, nuint size, nuint capacity)
    {
        this.seq = seq;
        this.pos = pos;
        this.posInSequence = posInSequence;
        this.size = size;
        this.capacity = capacity;
    }
}
