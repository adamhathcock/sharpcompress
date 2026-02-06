using System.IO;
using SharpCompress.Common;
using ZStd = SharpCompress.Compressors.ZStandard;

namespace SharpCompress.Compressors.Providers;

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

    public Stream CreateDecompressStream(Stream source)
    {
        return new ZStd.DecompressionStream(source);
    }
}
