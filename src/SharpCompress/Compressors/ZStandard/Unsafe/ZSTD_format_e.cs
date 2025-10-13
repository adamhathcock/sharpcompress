namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_format_e
{
    /* zstd frame format, specified in zstd_compression_format.md (default) */
    ZSTD_f_zstd1 = 0,

    /* Variant of zstd frame format, without initial 4-bytes magic number.
     * Useful to save 4 bytes per generated frame.
     * Decoder cannot recognise automatically this format, requiring this instruction. */
    ZSTD_f_zstd1_magicless = 1,
}
