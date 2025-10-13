namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_paramSwitch_e
{
    /* Let the library automatically determine whether the feature shall be enabled */
    ZSTD_ps_auto = 0,

    /* Force-enable the feature */
    ZSTD_ps_enable = 1,

    /* Do not use the feature */
    ZSTD_ps_disable = 2,
}
