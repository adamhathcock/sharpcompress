namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictContentType_e
{
    /// <summary>dictionary is "full" when starting with ZSTD_MAGIC_DICTIONARY, otherwise it is "rawContent"</summary>
    ZSTD_dct_auto = 0,

    /// <summary>ensures dictionary is always loaded as rawContent, even if it starts with ZSTD_MAGIC_DICTIONARY</summary>
    ZSTD_dct_rawContent = 1,

    /// <summary>refuses to load a dictionary if it does not respect Zstandard's specification</summary>
    ZSTD_dct_fullDict = 2,
}
