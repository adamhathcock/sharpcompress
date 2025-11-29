namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictLoadMethod_e
{
    /// <summary>Copy dictionary content internally</summary>
    ZSTD_dlm_byCopy = 0,

    /// <summary>Reference dictionary content -- the dictionary buffer must outlive its users</summary>
    ZSTD_dlm_byRef = 1,
}
