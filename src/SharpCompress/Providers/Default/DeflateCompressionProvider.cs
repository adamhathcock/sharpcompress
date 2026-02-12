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
        return new DeflateStream(destination, CompressionMode.Compress, level);
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for simple Deflate compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new DeflateStream(source, CompressionMode.Decompress);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for simple Deflate decompression
        return CreateDecompressStream(source);
    }
}
