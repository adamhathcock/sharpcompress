namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ldmParams_t
{
    /* ZSTD_ps_enable to enable LDM. ZSTD_ps_auto by default */
    public ZSTD_paramSwitch_e enableLdm;

    /* Log size of hashTable */
    public uint hashLog;

    /* Log bucket size for collision resolution, at most 8 */
    public uint bucketSizeLog;

    /* Minimum match length */
    public uint minMatchLength;

    /* Log number of entries to skip */
    public uint hashRateLog;

    /* Window log for the LDM */
    public uint windowLog;
}
