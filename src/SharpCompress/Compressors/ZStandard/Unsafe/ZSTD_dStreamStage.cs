namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dStreamStage
{
    zdss_init = 0,
    zdss_loadHeader,
    zdss_read,
    zdss_load,
    zdss_flush,
}
