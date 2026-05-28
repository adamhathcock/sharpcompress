using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Explode;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Explode decompression using SharpCompress's internal implementation.
/// Note: Explode compression is not supported; this provider is decompression-only.
/// </summary>
/// <remarks>
/// Explode requires compressed size, uncompressed size, and flags which must be provided via CompressionContext.
/// </remarks>
public sealed class ExplodeCompressionProvider : ContextRequiredDecompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Explode;
    protected override string CompressionNotSupportedMessage =>
        "Explode compression is not supported by SharpCompress's internal implementation.";

    protected override string DecompressionContextRequirementDescription =>
        "Explode decompression requires compressed size, uncompressed size, and flags";

    protected override string DecompressionContextRequirementSuffix => " with FormatOptions";

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        ValidateRequiredSizes(context, "Explode");
        var flags = RequireFormatOption<HeaderFlags>(context, "Explode", "HeaderFlags");

        return ExplodeStream.Create(source, context.InputSize, context.OutputSize, flags);
    }

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRequiredSizes(context, "Explode");
        var flags = RequireFormatOption<HeaderFlags>(context, "Explode", "HeaderFlags");

        return await ExplodeStream
            .CreateAsync(source, context.InputSize, context.OutputSize, flags, cancellationToken)
            .ConfigureAwait(false);
    }
}
