using System.IO;
using SharpCompress.Common;
using ZStd = SharpCompress.Compressors.ZStandard;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides ZStandard compression using SharpCompress's internal implementation.
/// </summary>
public sealed class ZStandardCompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.ZStandard;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        return new ZStd.CompressionStream(destination, compressionLevel);
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for ZStandard compression
        return CreateCompressStream(destination, compressionLevel);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        return new ZStd.DecompressionStream(source);
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        // Context not used for ZStandard decompression
        return CreateDecompressStream(source);
    }
}
