namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictContentType_e
{
    /* dictionary is "full" when starting with ZSTD_MAGIC_DICTIONARY, otherwise it is "rawContent" */
    ZSTD_dct_auto = 0,

    /* ensures dictionary is always loaded as rawContent, even if it starts with ZSTD_MAGIC_DICTIONARY */
    ZSTD_dct_rawContent = 1,

    /* refuses to load a dictionary if it does not respect Zstandard's specification, starting with ZSTD_MAGIC_DICTIONARY */
    ZSTD_dct_fullDict = 2,
}
