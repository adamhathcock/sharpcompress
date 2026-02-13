using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides LZip compression using SharpCompress's internal implementation.
/// </summary>
public sealed class LZipCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.LZip;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        return new LZipStream(destination, CompressionMode.Compress);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new LZipStream(source, CompressionMode.Decompress);
    }
}
