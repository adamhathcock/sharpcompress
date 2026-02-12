using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors.PPMd;

namespace SharpCompress.Providers.Default;

/// <summary>
/// Provides PPMd compression and decompression using SharpCompress's internal implementation.
/// This is a complex provider that requires initialization data for compression.
/// </summary>
public sealed class PpmdCompressingProvider : CompressionProviderBase, ICompressionProviderHooks
{
    public override CompressionType CompressionType => CompressionType.PPMd;
    public override bool SupportsCompression => true;
    public override bool SupportsDecompression => true;

    public override Stream CreateCompressStream(Stream destination, int compressionLevel)
    {
        // Ppmd doesn't use compressionLevel, uses PpmdProperties instead
        var props = new PpmdProperties();
        return PpmdStream.Create(props, destination, true);
    }

    public override Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    )
    {
        // Context not used for Ppmd compression, but we could use FormatOptions for custom properties
        if (context.FormatOptions is PpmdProperties customProps)
        {
            return PpmdStream.Create(customProps, destination, true);
        }

        return CreateCompressStream(destination, compressionLevel);
    }

    public override Stream CreateDecompressStream(Stream source)
    {
        throw new InvalidOperationException(
            "PPMd decompression requires properties. "
                + "Use CreateDecompressStream(Stream, CompressionContext) overload with Properties."
        );
    }

    public override Stream CreateDecompressStream(Stream source, CompressionContext context)
    {
        if (context.Properties is null || context.Properties.Length < 2)
        {
            throw new ArgumentException(
                "PPMd decompression requires Properties (at least 2 bytes) in CompressionContext.",
                nameof(context)
            );
        }

        var props = new PpmdProperties(context.Properties);
        return PpmdStream.Create(props, source, false);
    }

    public byte[]? GetPreCompressionData(CompressionContext context)
    {
        // Ppmd writes its properties before the compressed data
        if (context.FormatOptions is PpmdProperties customProps)
        {
            return customProps.Properties;
        }

        var defaultProps = new PpmdProperties();
        return defaultProps.Properties;
    }

    public byte[]? GetCompressionProperties(Stream stream, CompressionContext context)
    {
        // Properties are already written in GetPreCompressionData
        return null;
    }

    public byte[]? GetPostCompressionData(Stream stream, CompressionContext context)
    {
        // No post-compression data needed for Ppmd
        return null;
    }
}
