using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Xz;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides XZ compression decompression using SharpCompress's internal implementation.
/// Note: Compression is not supported by this provider.
/// </summary>
public sealed class XzCompressionProvider : DecompressionOnlyProviderBase
{
    public override CompressionType CompressionType => CompressionType.Xz;
    protected override string CompressionNotSupportedMessage =>
        "XZ compression is not supported by SharpCompress's internal implementation.";

    public override Stream CreateDecompressStream(Stream source)
    {
        return new XZStream(source);
    }
}
