using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Providers;

/// <summary>
/// Base class for compression providers that provides default async implementations
/// delegating to synchronous methods. Providers can inherit from this class for
/// simpler implementations or implement ICompressionProvider directly for full control.
/// </summary>
/// <remarks>
/// <para>
/// This base class implements the async methods by calling the synchronous versions.
/// Providers that need true async implementations should override these methods.
/// </para>
/// </remarks>
public abstract class CompressionProviderBase : ICompressionProvider
{
    /// <inheritdoc />
    public abstract CompressionType CompressionType { get; }

    /// <inheritdoc />
    public abstract bool SupportsCompression { get; }

    /// <inheritdoc />
    public abstract bool SupportsDecompression { get; }

    /// <inheritdoc />
    public abstract Stream CreateCompressStream(Stream destination, int compressionLevel);

    /// <inheritdoc />
    public virtual Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    ) => CreateCompressStream(destination, compressionLevel);

    /// <inheritdoc />
    public abstract Stream CreateDecompressStream(Stream source);

    /// <inheritdoc />
    public virtual Stream CreateDecompressStream(Stream source, CompressionContext context) =>
        CreateDecompressStream(source);

    /// <summary>
    /// Asynchronously creates a compression stream.
    /// Default implementation delegates to the synchronous CreateCompressStream.
    /// </summary>
    public virtual ValueTask<Stream> CreateCompressStreamAsync(
        Stream destination,
        int compressionLevel,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<Stream>(CreateCompressStream(destination, compressionLevel));
    }

    /// <summary>
    /// Asynchronously creates a compression stream with context.
    /// Default implementation delegates to the synchronous CreateCompressStream with context.
    /// </summary>
    public virtual ValueTask<Stream> CreateCompressStreamAsync(
        Stream destination,
        int compressionLevel,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        return CreateCompressStreamAsync(destination, compressionLevel, cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a decompression stream.
    /// Default implementation delegates to the synchronous CreateDecompressStream.
    /// </summary>
    public virtual ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<Stream>(CreateDecompressStream(source));
    }

    /// <summary>
    /// Asynchronously creates a decompression stream with context.
    /// Default implementation delegates to the synchronous CreateDecompressStream with context.
    /// </summary>
    public virtual ValueTask<Stream> CreateDecompressStreamAsync(
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        return CreateDecompressStreamAsync(source, cancellationToken);
    }

    protected static void ValidateRequiredSizes(CompressionContext context, string algorithmName)
    {
        if (context.InputSize < 0 || context.OutputSize < 0)
        {
            throw new ArgumentException(
                $"{algorithmName} decompression requires InputSize and OutputSize in CompressionContext.",
                nameof(context)
            );
        }
    }

    protected static T RequireFormatOption<T>(
        CompressionContext context,
        string algorithmName,
        string optionName
    )
    {
        if (context.FormatOptions is not T options)
        {
            throw new ArgumentException(
                $"{algorithmName} decompression requires {optionName} in CompressionContext.FormatOptions.",
                nameof(context)
            );
        }

        return options;
    }
}
