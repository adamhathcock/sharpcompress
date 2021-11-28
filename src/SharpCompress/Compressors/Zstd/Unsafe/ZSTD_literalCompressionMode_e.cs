using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_literalCompressionMode_e
    {
        ZSTD_lcm_auto = 0,
        ZSTD_lcm_huffman = 1,
        ZSTD_lcm_uncompressed = 2,
    }
}
