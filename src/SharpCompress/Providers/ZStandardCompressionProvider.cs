using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using ZStd = SharpCompress.Compressors.ZStandard;

namespace SharpCompress.Providers;

/// <summary>
/// Provides ZStandard compression using SharpCompress's internal implementation.
/// </summary>
public sealed class ZStandardCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.ZStandard;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        return new ZStd.CompressionStream(destination, compressionLevel);
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for ZStandard compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        return new ZStd.DecompressionStream(source);
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for ZStandard decompression
        return CreateDecompressStream(source);
    }
}
