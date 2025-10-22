namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* Type returned by ZSTD_buildSequencesStatistics containing finalized symbol encoding types
 * and size of the sequences statistics
 */
public struct ZSTD_symbolEncodingTypeStats_t
{
    public uint LLtype;
    public uint Offtype;
    public uint MLtype;
    public nuint size;

    /* Accounts for bug in 1.3.4. More detail in ZSTD_entropyCompressSeqStore_internal() */
    public nuint lastCountSize;
    public int longOffsets;
}
