namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_refMultipleDDicts_e
{
    /* Note: this enum controls ZSTD_d_refMultipleDDicts */
    ZSTD_rmd_refSingleDDict = 0,
    ZSTD_rmd_refMultipleDDicts = 1,
}
