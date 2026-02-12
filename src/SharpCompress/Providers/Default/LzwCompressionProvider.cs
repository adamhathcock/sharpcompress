using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.Lzw;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides LZW compression decompression using SharpCompress's internal implementation.
/// Note: Compression is not supported by this provider.
/// </summary>
public sealed class LzwCompressionProvider : DecompressionOnlyProviderBase
{
    public override CompressionType CompressionType => CompressionType.Lzw;
    protected override string CompressionNotSupportedMessage =>
        "LZW compression is not supported by SharpCompress's internal implementation.";

    public override Stream CreateDecompressStream(Stream source)
    {
        return new LzwStream(source);
    }
}
