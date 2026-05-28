using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate64;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Deflate64 decompression using SharpCompress's internal implementation.
/// Note: Deflate64 compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Deflate64CompressionProvider : DecompressionOnlyProviderBase
{
    public override CompressionType CompressionType => CompressionType.Deflate64;
    protected override string CompressionNotSupportedMessage =>
        "Deflate64 compression is not supported by SharpCompress's internal implementation.";

    public override Stream CreateDecompressStream(Stream source)
    {
        return new Deflate64Stream(source, CompressionMode.Decompress);
    }
}
