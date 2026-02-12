using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.Reduce;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides Reduce4 decompression using SharpCompress's internal implementation.
/// Note: Reduce compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Reduce4CompressionProvider : CompressionProviderBase
{
    public override CompressionType CompressionType => CompressionType.Reduce4;
    public override bool SupportsCompression => false;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "Reduce compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "Reduce compression is not supported by SharpCompress's internal implementation."
        );
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        throw new InvalidOperationException(
            "Reduce decompression requires compressed and uncompressed sizes. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload."
        );
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        if (context.InputSize < 0 || context.OutputSize < 0)
        {
            throw new ArgumentException(
                "Reduce decompression requires InputSize and OutputSize in CompressionContext.",
                nameof(context)
            );
        }

        return ReduceStream.Create(source, context.InputSize, context.OutputSize, 4);
    }

    public override ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default
    ) =>
        throw new InvalidOperationException(
            "Reduce decompression requires compressed and uncompressed sizes. "
                + "Use CreateDecompressStreamAsync(Stream, CompressionContext, CancellationToken) overload."
        );

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (context.InputSize < 0 || context.OutputSize < 0)
        {
            throw new ArgumentException(
                "Reduce decompression requires InputSize and OutputSize in CompressionContext.",
                nameof(context)
            );
        }

        return await ReduceStream
            .CreateAsync(source, context.InputSize, context.OutputSize, 4, cancellationToken)
            .ConfigureAwait(false);
    }
}
