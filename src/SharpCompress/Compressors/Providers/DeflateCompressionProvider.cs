using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Compressors.Providers;

/// <summary>
/// Provides Deflate compression using SharpCompress's internal implementation.
/// </summary>
public sealed class DeflateCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Deflate;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var level = (CompressionLevel)compressionLevel;
        return new DeflateStream(destination, CompressionMode.Compress, level);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new DeflateStream(source, CompressionMode.Decompress);
    }
}
