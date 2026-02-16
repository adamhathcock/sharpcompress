using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides LZMA compression and decompression using SharpCompress's internal implementation.
/// This is a complex provider that requires initialization data for compression.
/// </summary>
public sealed class LzmaCompressingProvider : CompressionProviderBase, ICompressionProviderHooks
{
    public override CompressionType CompressionType => CompressionType.LZMA;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        throw new ArchiveOperationException(
            "LZMA compression requires context with CanSeek information. "
                + "Use CreateCompressStream(Stream, int, CompressionContext) overload."
        );
    }

    public override Stream CreateCompressStream(
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

    public override Stream CreateDecompressStream(Stream source)
    {
        throw new ArchiveOperationException(
            "LZMA decompression requires properties. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload with Properties."
        );
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
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

    public override ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default
    ) =>
        throw new ArchiveOperationException(
            "LZMA decompression requires properties. "
                + "Use CreateDecompressStreamAsync(Stream, CompressionContext, CancellationToken) overload with Properties."
        );

    public override async ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (context.Properties is null || context.Properties.Length < 5)
        {
            throw new ArgumentException(
                "LZMA decompression requires Properties (at least 5 bytes) in CompressionContext.",
                nameof(context)
            );
        }

        return await LzmaStream
            .CreateAsync(
                context.Properties,
                source,
                context.InputSize,
                context.OutputSize,
                leaveOpen: false
            )
            .ConfigureAwait(false);
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
