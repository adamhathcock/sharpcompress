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
public sealed class Deflate64CompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Deflate64;
    public override bool SupportsCompression => false;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "Deflate64 compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "Deflate64 compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new Deflate64Stream(source, CompressionMode.Decompress);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for Deflate64 decompression
        return CreateDecompressStream(source);
    }
}
