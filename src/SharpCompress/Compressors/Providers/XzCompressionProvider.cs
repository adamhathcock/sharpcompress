using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;

namespace SharpCompress.Compressors.Providers;

/// <summary>
/// Provides XZ compression decompression using SharpCompress's internal implementation.
/// Note: Compression is not supported by this provider.
/// </summary>
public sealed class XzCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Xz;
    public bool SupportsCompression => false;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "XZ compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new XZStream(source);
    }
}
