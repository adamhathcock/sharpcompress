namespace SharpCompress.Compressors.BZip2;

/// <summary>
/// Options for configuring <see cref="BZip2Stream" /> behavior.
/// </summary>
public sealed class BZip2StreamOptions
{
    private int blockSize100k = 9;

    /// <summary>
    /// Decompression only. When true, consecutive BZip2 streams are decompressed as one stream.
    /// </summary>
    public bool DecompressConcatenated { get; set; }

    /// <summary>
    /// Leaves the underlying stream open when the BZip2 stream is disposed.
    /// </summary>
    public bool LeaveStreamOpen { get; set; }

    /// <summary>
    /// Decompression only. When true, an end-of-stream reached at a bzip2 block boundary is treated as a
    /// normal end of stream rather than throwing. EOF in the middle of a block is still reported as an error.
    /// </summary>
    public bool TolerateTruncatedStream { get; set; }

    /// <summary>
    /// Compression only. The BZip2 block size in 100k units. Values are clamped to the BZip2 range 1-9.
    /// </summary>
    public int BlockSize100k
    {
        get => blockSize100k;
        set =>
            blockSize100k =
                value < 1 ? 1
                : value > 9 ? 9
                : value;
    }
}
