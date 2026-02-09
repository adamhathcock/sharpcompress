using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Lzw;

namespace SharpCompress.Compressors.Providers;

/// <summary>
/// Provides LZW compression decompression using SharpCompress's internal implementation.
/// Note: Compression is not supported by this provider.
/// </summary>
public sealed class LzwCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Lzw;
    public bool SupportsCompression => false;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "LZW compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "LZW compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new LzwStream(source);
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for LZW decompression
        return CreateDecompressStream(source);
    }
}
