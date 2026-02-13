using SharpCompress.Common;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Reduce1 decompression using SharpCompress's internal implementation.
/// Note: Reduce compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Reduce1CompressionProvider : ReduceCompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Reduce1;
    protected override int Factor => 1;
}
