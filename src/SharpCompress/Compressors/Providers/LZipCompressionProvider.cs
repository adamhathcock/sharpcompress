using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Compressors.Providers;

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

    public Stream CreateDecompressStream(Stream source)
    {
        return new LZipStream(source, CompressionMode.Decompress);
    }
}
