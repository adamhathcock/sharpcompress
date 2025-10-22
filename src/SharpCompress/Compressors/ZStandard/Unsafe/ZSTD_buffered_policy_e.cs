namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Indicates whether this compression proceeds directly from user-provided
 * source buffer to user-provided destination buffer (ZSTDb_not_buffered), or
 * whether the context needs to buffer the input/output (ZSTDb_buffered).
 */
public enum ZSTD_buffered_policy_e
{
    ZSTDb_not_buffered,
    ZSTDb_buffered,
}
