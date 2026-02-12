using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Providers;

public abstract class ContextRequiredDecompressionProviderBase : DecompressionOnlyProviderBase
{
    protected abstract string DecompressionContextRequirementDescription { get; }

    protected virtual string DecompressionContextRequirementSuffix => string.Empty;

    public sealed override Stream CreateDecompressStream(Stream source) =>
        throw new InvalidOperationException(
            $"{DecompressionContextRequirementDescription}. "
                + $"Use CreateDecompressStream(Stream, CompressionContext) overload{DecompressionContextRequirementSuffix}."
        );

    public sealed override ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default
    ) =>
        throw new InvalidOperationException(
            $"{DecompressionContextRequirementDescription}. "
                + "Use CreateDecompressStreamAsync(Stream, CompressionContext, CancellationToken) "
                + $"overload{DecompressionContextRequirementSuffix}."
        );
}
