using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Providers;

/// <summary>
/// Provides LZMA compression and decompression using SharpCompress's internal implementation.
/// This is a complex provider that requires initialization data for compression.
/// </summary>
public sealed class LzmaCompressingProvider : ICompressionProviderHooks
{
    public CompressionType CompressionType => CompressionType.LZMA;
    public bool SupportsCompression => true;
    public bool SupportsDecompression => true;

    public Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new InvalidOperationException(
            "LZMA compression requires context with CanSeek information. "
                + "Use CreateCompressStream(Stream, int, CompressionContext) overload."
        );
    }

    public Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // LZMA stream creation returns the encoder stream
        // Note: Pre-compression data and properties are handled via ICompressionProviderHooks methods
        var props = new LzmaEncoderProperties(!context.CanSeek);
        return LzmaStream.Create(props, false, destination);
    }

    public Stream CreateDecompressStream(Stream source)
    {
        throw new InvalidOperationException(
            "LZMA decompression requires properties. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload with Properties."
        );
    }

    public Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        if (context.Properties is null || context.Properties.Length < 5)
        {
            throw new ArgumentException(
                "LZMA decompression requires Properties (at least 5 bytes) in CompressionContext.",
                nameof(context)
            );
        }

        return LzmaStream.Create(context.Properties, source, context.InputSize, context.OutputSize);
    }

    public byte[]? GetPreCompressionData(CompressionContext context)
    {
        // Zip format writes these magic bytes before the LZMA stream
        return new byte[] { 9, 20, 5, 0 };
    }

    public byte[]? GetCompressionProperties(Stream stream, CompressionContext context)
    {
        // The LZMA stream exposes its properties after creation
        if (stream is LzmaStream lzmaStream)
        {
            return lzmaStream.Properties;
        }
        return null;
    }

    public byte[]? GetPostCompressionData(Stream stream, CompressionContext context)
    {
        // No post-compression data needed for LZMA in Zip
        return null;
    }
}
