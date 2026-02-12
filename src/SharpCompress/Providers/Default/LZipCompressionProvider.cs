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

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for LZip compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new LZipStream(source, CompressionMode.Decompress);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for LZip decompression
        return CreateDecompressStream(source);
    }
}
