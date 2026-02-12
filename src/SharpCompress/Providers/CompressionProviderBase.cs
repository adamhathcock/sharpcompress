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
    public abstract Stream CreateCompressStream(
        Stream destination,
        int compressionLevel,
        CompressionContext context
    );

    /// <inheritdoc />
    public abstract Stream CreateDecompressStream(Stream source);

    /// <inheritdoc />
    public abstract Stream CreateDecompressStream(Stream source, CompressionContext context);

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
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<Stream>(CreateCompressStream(destination, compressionLevel, context));
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
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<Stream>(CreateDecompressStream(source, context));
    }
}
