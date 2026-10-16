using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Deflate compression using SharpCompress's internal implementation.
/// </summary>
public sealed class DeflateCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Deflate;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var level = (CompressionLevel)compressionLevel;
        // Batch output to reduce downstream writes, particularly for stored or
        // incompressible data. Keep decoder buffers unchanged for read-ahead behavior.
        return new DeflateStream(destination, CompressionMode.Compress, level)
        {
            BufferSize = 64 * 1024,
        };
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new DeflateStream(source, CompressionMode.Decompress);
    }
}
