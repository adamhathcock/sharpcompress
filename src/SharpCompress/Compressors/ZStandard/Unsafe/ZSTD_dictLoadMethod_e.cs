namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictLoadMethod_e
{
    /**< Copy dictionary content internally */
    ZSTD_dlm_byCopy = 0,

    /**< Reference dictionary content -- the dictionary buffer must outlive its users. */
    ZSTD_dlm_byRef = 1,
}
