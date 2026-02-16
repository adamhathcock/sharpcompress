using System.Globalization;

namespace SharpCompress.Common;

public static class Constants
{
    /// <summary>
    /// The default buffer size for stream operations, matching .NET's Stream.CopyTo default of 81920 bytes.
    /// This can be modified globally at runtime.
    /// </summary>
    public static int BufferSize { get; set; } = 81920;

    /// <summary>
    /// The default size for rewindable buffers in SharpCompressStream.
    /// Used for format detection on non-seekable streams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When opening archives from non-seekable streams (network streams, pipes,
    /// compressed streams), SharpCompress uses a ring buffer to enable format
    /// auto-detection. This buffer allows the library to try multiple decoders
    /// by rewinding and re-reading the same data.
    /// </para>
    /// <para>
    /// <b>Default:</b> 81920 bytes (81KB) - sufficient for typical format detection.
    /// </para>
    /// <para>
    /// <b>Typical usage:</b> 500-1000 bytes for most archives
    /// </para>
    /// <para>
    /// <b>Can be overridden per-stream via ReaderOptions.RewindableBufferSize.</b>
    /// </para>
    /// <para>
    /// <b>Increase if:</b>
    /// <list type="bullet">
    /// <item>Handling self-extracting archives (may need 512KB+)</item>
    /// <item>Format detection fails with buffer overflow errors</item>
    /// <item>Using custom formats with large headers</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static int RewindableBufferSize { get; set; } = 81920;

    public static CultureInfo DefaultCultureInfo { get; set; } = CultureInfo.InvariantCulture;
}
