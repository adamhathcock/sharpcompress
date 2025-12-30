namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_sequenceFormat_e
{
    /* ZSTD_Sequence[] has no block delimiters, just sequences */
    ZSTD_sf_noBlockDelimiters = 0,

    /* ZSTD_Sequence[] contains explicit block delimiters */
    ZSTD_sf_explicitBlockDelimiters = 1,
}
