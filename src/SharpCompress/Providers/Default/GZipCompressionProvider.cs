using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides GZip compression using SharpCompress's internal implementation.
/// </summary>
public sealed class GZipCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.GZip;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        var level = (CompressionLevel)compressionLevel;
        return new GZipStream(destination, CompressionMode.Compress, level);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new GZipStream(source, CompressionMode.Decompress);
    }
}
