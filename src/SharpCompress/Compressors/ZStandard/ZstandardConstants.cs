namespace SharpCompress.Compressors.ZStandard;

internal class ZstandardConstants
{
    /// <summary>
    /// Magic number found at start of ZStandard frame: 0xFD 0x2F 0xB5 0x28
    /// </summary>
    public const uint MAGIC = 0xFD2FB528;

    /// <summary>
    /// Maximum uncompressed size of a single ZStandard block: ZSTD_BLOCKSIZE_MAX = 128 KB.
    /// </summary>
    public const int BlockSizeMax = 1 << 17; // 131072 bytes

    /// <summary>
    /// Recommended input (compressed) buffer size for streaming decompression:
    /// ZSTD_DStreamInSize = ZSTD_BLOCKSIZE_MAX + ZSTD_blockHeaderSize (3 bytes).
    /// The ring buffer must be at least this large to hold the compressed bytes read
    /// during format detection before the first rewind.
    /// </summary>
    public const int DStreamInSize = BlockSizeMax + 3;
}
