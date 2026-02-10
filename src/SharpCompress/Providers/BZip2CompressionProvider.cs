using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace SharpCompress.Providers;

/// <summary>
/// Provides BZip2 compression using SharpCompress's internal implementation.
/// </summary>
public sealed class BZip2CompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.BZip2;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        // BZip2 doesn't use compressionLevel parameter in this implementation
        return BZip2Stream.Create(destination, CompressionMode.Compress, false);
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for BZip2 compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return BZip2Stream.Create(source, CompressionMode.Decompress, false);
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for BZip2 decompression
        return CreateDecompressStream(source);
    }
}
