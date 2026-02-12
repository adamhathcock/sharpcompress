using SharpCompress.Common;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Reduce4 decompression using SharpCompress's internal implementation.
/// Note: Reduce compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Reduce4CompressionProvider : ReduceCompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Reduce4;
    protected override int Factor => 4;
}
