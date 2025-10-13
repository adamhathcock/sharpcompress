namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Used to describe whether the workspace is statically allocated (and will not
 * necessarily ever be freed), or if it's dynamically allocated and we can
 * expect a well-formed caller to free this.
 */
public enum ZSTD_cwksp_static_alloc_e
{
    ZSTD_cwksp_dynamic_alloc,
    ZSTD_cwksp_static_alloc,
}
