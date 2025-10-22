namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZDICT_legacy_params_t
{
    /* 0 means default; larger => select more => larger dictionary */
    public uint selectivityLevel;
    public ZDICT_params_t zParams;
}
