namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* Controls whether seqStore has a single "long" litLength or matchLength. See SeqStore_t. */
public enum ZSTD_longLengthType_e
{
    /* no longLengthType */
    ZSTD_llt_none = 0,

    /* represents a long literal */
    ZSTD_llt_literalLength = 1,

    /* represents a long match */
    ZSTD_llt_matchLength = 2,
}
