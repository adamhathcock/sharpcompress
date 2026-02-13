using SharpCompress.Common;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Reduce2 decompression using SharpCompress's internal implementation.
/// Note: Reduce compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Reduce2CompressionProvider : ReduceCompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Reduce2;
    protected override int Factor => 2;
}
