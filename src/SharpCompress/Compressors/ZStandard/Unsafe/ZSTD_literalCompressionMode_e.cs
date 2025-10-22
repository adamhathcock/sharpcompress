namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_literalCompressionMode_e
{
    /**< Automatically determine the compression mode based on the compression level.
     *   Negative compression levels will be uncompressed, and positive compression
     *   levels will be compressed. */
    ZSTD_lcm_auto = 0,

    /**< Always attempt Huffman compression. Uncompressed literals will still be
     *   emitted if Huffman compression is not profitable. */
    ZSTD_lcm_huffman = 1,

    /**< Always emit uncompressed literals. */
    ZSTD_lcm_uncompressed = 2,
}
