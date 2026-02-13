using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Shrink;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Shrink decompression using SharpCompress's internal implementation.
/// Note: Shrink compression is not supported; this provider is decompression-only.
/// </summary>
/// <remarks>
/// Shrink requires compressed and uncompressed sizes which must be provided via CompressionContext.
/// </remarks>
public sealed class ShrinkCompressionProvider : ContextRequiredDecompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Shrink;
    protected override string CompressionNotSupportedMessage =>
        "Shrink compression is not supported by SharpCompress's internal implementation.";

    protected override string DecompressionContextRequirementDescription =>
        "Shrink decompression requires compressed and uncompressed sizes";

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        ValidateRequiredSizes(context, "Shrink");

        return new ShrinkStream(
            source,
            CompressionMode.Decompress,
            context.InputSize,
            context.OutputSize
        );
    }

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ValidateRequiredSizes(context, "Shrink");

        return await ShrinkStream
            .CreateAsync(
                source,
                CompressionMode.Decompress,
                context.InputSize,
                context.OutputSize,
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
