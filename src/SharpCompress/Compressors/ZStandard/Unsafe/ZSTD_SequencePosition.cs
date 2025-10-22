namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_SequencePosition
{
    /* Index in array of ZSTD_Sequence */
    public uint idx;

    /* Position within sequence at idx */
    public uint posInSequence;

    /* Number of bytes given by sequences provided so far */
    public nuint posInSrc;
}
