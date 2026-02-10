using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Reduce;

namespace SharpCompress.Providers;

/// <summary>
/// Provides Reduce2 decompression using SharpCompress's internal implementation.
/// Note: Reduce compression is not supported; this provider is decompression-only.
/// </summary>
public sealed class Reduce2CompressionProvider : ICompressionProvider
{
    public CompressionType CompressionType => CompressionType.Reduce2;
    public bool SupportsCompression => false;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new NotSupportedException(
            "Reduce compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        throw new NotSupportedException(
            "Reduce compression is not supported by SharpCompress's internal implementation."
        );
    }

    public Stream CreateDecompressStream(Stream source)
    {
        throw new InvalidOperationException(
            "Reduce decompression requires compressed and uncompressed sizes. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload."
        );
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        if (context.InputSize < 0 || context.OutputSize < 0)
        {
            throw new ArgumentException(
                "Reduce decompression requires InputSize and OutputSize in CompressionContext.",
                nameof(context)
            );
        }

        return ReduceStream.Create(source, context.InputSize, context.OutputSize, 2);
    }
}
