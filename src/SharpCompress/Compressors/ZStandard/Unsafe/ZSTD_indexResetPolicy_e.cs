namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Controls, for this matchState reset, whether indexing can continue where it
 * left off (ZSTDirp_continue), or whether it needs to be restarted from zero
 * (ZSTDirp_reset).
 */
public enum ZSTD_indexResetPolicy_e
{
    ZSTDirp_continue,
    ZSTDirp_reset,
}
