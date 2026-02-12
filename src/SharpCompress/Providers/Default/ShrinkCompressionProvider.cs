using System;
using System.IO;
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
public sealed class ShrinkCompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Shrink;
    public bool SupportsCompression => false;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "Shrink compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "Shrink compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateDecompressStream(Stream source)
    {
        throw new InvalidOperationException(
            "Shrink decompression requires compressed and uncompressed sizes. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload."
        );
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        if (context.InputSize < 0 || context.OutputSize < 0)
        {
            throw new ArgumentException(
                "Shrink decompression requires InputSize and OutputSize in CompressionContext.",
                nameof(context)
            );
        }

        return new ShrinkStream(
            source,
            CompressionMode.Decompress,
            context.InputSize,
            context.OutputSize
        );
    }
}
