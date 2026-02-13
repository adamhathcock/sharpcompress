using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Reduce;

namespace SharpCompress.Providers.Default;

public abstract class ReduceCompressionProviderBase : ContextRequiredDecompressionProviderBase
{
    protected abstract int Factor { get; }

    protected override string DecompressionContextRequirementDescription =>
        "Reduce decompression requires compressed and uncompressed sizes";

    protected override string CompressionNotSupportedMessage =>
        "Reduce compression is not supported by SharpCompress's internal implementation.";

    public sealed override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        ValidateRequiredSizes(context, "Reduce");
        return ReduceStream.Create(source, context.InputSize, context.OutputSize, Factor);
    }

    public sealed override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRequiredSizes(context, "Reduce");
        return await ReduceStream
            .CreateAsync(source, context.InputSize, context.OutputSize, Factor, cancellationToken)
            .ConfigureAwait(false);
    }
}
