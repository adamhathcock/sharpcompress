using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Lzw;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides LZW compression decompression using SharpCompress's internal implementation.
/// Note: Compression is not supported by this provider.
/// </summary>
public sealed class LzwCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Lzw;
    public override bool SupportsCompression => false;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "LZW compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "LZW compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new LzwStream(source);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for LZW decompression
        return CreateDecompressStream(source);
    }
}
