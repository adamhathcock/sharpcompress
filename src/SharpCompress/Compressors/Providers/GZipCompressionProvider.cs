using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Compressors.Providers;

/// <summary>
/// Provides GZip compression using SharpCompress's internal implementation.
/// </summary>
public sealed class GZipCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.GZip;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var level = (CompressionLevel)compressionLevel;
        return new GZipStream(destination, CompressionMode.Compress, level);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new GZipStream(source, CompressionMode.Decompress);
    }
}
