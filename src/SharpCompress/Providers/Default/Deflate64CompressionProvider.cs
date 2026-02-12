using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate64;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Deflate64 decompression using SharpCompress's internal implementation.
/// Note: Deflate64 compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Deflate64CompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Deflate64;
    public bool SupportsCompression => false;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "Deflate64 compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "Deflate64 compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new Deflate64Stream(source, CompressionMode.Decompress);
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for Deflate64 decompression
        return CreateDecompressStream(source);
    }
}
