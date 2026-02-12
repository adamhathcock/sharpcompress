using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides XZ compression decompression using SharpCompress's internal implementation.
/// Note: Compression is not supported by this provider.
/// </summary>
public sealed class XzCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Xz;
    public override bool SupportsCompression => false;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "XZ compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "XZ compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new XZStream(source);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for XZ decompression
        return CreateDecompressStream(source);
    }
}
