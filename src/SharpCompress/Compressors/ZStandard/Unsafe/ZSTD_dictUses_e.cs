namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictUses_e
{
    /* Use the dictionary indefinitely */
    ZSTD_use_indefinitely = -1,

    /* Do not use the dictionary (if one exists free it) */
    ZSTD_dont_use = 0,

    /* Use the dictionary once and set to ZSTD_dont_use */
    ZSTD_use_once = 1,
}
