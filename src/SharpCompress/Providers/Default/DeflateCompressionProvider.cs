using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Deflate compression using SharpCompress's internal implementation.
/// </summary>
public sealed class DeflateCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Deflate;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var level = (CompressionLevel)compressionLevel;
        return new DeflateStream(destination, CompressionMode.Compress, level);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new DeflateStream(source, CompressionMode.Decompress);
    }
}
