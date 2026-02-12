using System;
using System.IO;
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
public sealed class ExplodeCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Explode;
    public bool SupportsCompression => false;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "Explode compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "Explode compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateDecompressStream(Stream source)
    {
        throw new InvalidOperationException(
            "Explode decompression requires compressed size, uncompressed size, and flags. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload with FormatOptions."
        );
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        if (context.InputSize < 0 || context.OutputSize < 0)
        {
            throw new ArgumentException(
                "Explode decompression requires InputSize and OutputSize in CompressionContext.",
                nameof(context)
            );
        }

        if (context.FormatOptions is not HeaderFlags flags)
        {
            throw new ArgumentException(
                "Explode decompression requires HeaderFlags in CompressionContext.FormatOptions.",
                nameof(context)
            );
        }

        return ExplodeStream.Create(source, context.InputSize, context.OutputSize, flags);
    }
}
