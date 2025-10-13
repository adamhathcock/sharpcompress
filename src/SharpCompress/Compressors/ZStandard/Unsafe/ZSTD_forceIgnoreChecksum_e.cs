namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_forceIgnoreChecksum_e
{
    /* Note: this enum controls ZSTD_d_forceIgnoreChecksum */
    ZSTD_d_validateChecksum = 0,
    ZSTD_d_ignoreChecksum = 1,
}
