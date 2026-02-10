using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Providers;

/// <summary>
/// Provides LZip compression using SharpCompress's internal implementation.
/// </summary>
public sealed class LZipCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.LZip;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        return new LZipStream(destination, CompressionMode.Compress);
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for LZip compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new LZipStream(source, CompressionMode.Decompress);
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for LZip decompression
        return CreateDecompressStream(source);
    }
}
