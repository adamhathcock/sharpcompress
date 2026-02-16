using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Providers.Default;

namespace SharpCompress.Providers;

/// <summary>
/// A registry of compression providers, keyed by CompressionType.
/// Used to resolve which implementation to use for a given compression type.
/// </summary>
/// <remarks>
/// <para>
/// This class is immutable. Use the <c>With</c> method to create modified copies
/// that add or replace providers:
/// </para>
/// <code>
/// var customRegistry = CompressionProviderRegistry.Default
///     .With(new MyCustomGZipProvider());
/// var options = new WriterOptions(CompressionType.GZip)
/// {
///     Providers = customRegistry
/// };
/// </code>
/// </remarks>
public sealed class CompressionProviderRegistry
{
    /// <summary>
    /// The default registry using SharpCompress internal implementations.
    /// </summary>
    public static CompressionProviderRegistry Default { get; } = CreateDefault();

    /// <summary>
    /// The empty registry for tests
    /// </summary>
    public static CompressionProviderRegistry Empty { get; } = CreateEmpty();

    private readonly Dictionary<CompressionType, ICompressionProvider> _providers;

    private CompressionProviderRegistry(
        Dictionary<CompressionType, ICompressionProvider> providers
    ) => _providers = providers;

    /// <summary>
    /// Gets the provider for a given compression type, or null if none is registered.
    /// </summary>
    /// <param name="type">The compression type to look up.</param>
    /// <returns>The provider for the type, or null if not found.</returns>
    public ICompressionProvider? GetProvider(CompressionType type)
    {
        _providers.TryGetValue(type, out var provider);
        return provider;
    }

    /// <summary>
    /// Creates a compression stream for the specified type.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="destination">The destination stream.</param>
    /// <param name="level">The compression level.</param>
    /// <returns>A compression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support compression.</exception>
    public Stream CreateCompressStream(CompressionType type, Stream destination, int level)
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateCompressStream(destination, level);
    }

    /// <summary>
    /// Creates a decompression stream for the specified type.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="source">The source stream.</param>
    /// <returns>A decompression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support decompression.</exception>
    public Stream CreateDecompressStream(CompressionType type, Stream source)
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateDecompressStream(source);
    }

    /// <summary>
    /// Creates a compression stream for the specified type with context.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="destination">The destination stream.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="context">Context information for the compression.</param>
    /// <returns>A compression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support compression.</exception>
    public Stream CreateCompressStream(
        CompressionType type,
        Stream destination,
        int level,
        CompressionContext context
    )
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateCompressStream(destination, level, context);
    }

    /// <summary>
    /// Creates a decompression stream for the specified type with context.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="source">The source stream.</param>
    /// <param name="context">Context information for the decompression.</param>
    /// <returns>A decompression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support decompression.</exception>
    public Stream CreateDecompressStream(
        CompressionType type,
        Stream source,
        CompressionContext context
    )
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateDecompressStream(source, context);
    }

    /// <summary>
    /// Asynchronously creates a compression stream for the specified type.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="destination">The destination stream.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the compression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support compression.</exception>
    public ValueTask<Stream> CreateCompressStreamAsync(
        CompressionType type,
        Stream destination,
        int level,
        CancellationToken cancellationToken = default
    )
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateCompressStreamAsync(destination, level, cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a decompression stream for the specified type.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="source">The source stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the decompression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support decompression.</exception>
    public ValueTask<Stream> CreateDecompressStreamAsync(
        CompressionType type,
        Stream source,
        CancellationToken cancellationToken = default
    )
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateDecompressStreamAsync(source, cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a compression stream for the specified type with context.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="destination">The destination stream.</param>
    /// <param name="level">The compression level.</param>
    /// <param name="context">Context information for the compression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the compression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support compression.</exception>
    public ValueTask<Stream> CreateCompressStreamAsync(
        CompressionType type,
        Stream destination,
        int level,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateCompressStreamAsync(destination, level, context, cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a decompression stream for the specified type with context.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <param name="source">The source stream.</param>
    /// <param name="context">Context information for the decompression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the decompression stream.</returns>
    /// <exception cref="ArchiveOperationException">If no provider is registered for the type.</exception>
    /// <exception cref="NotSupportedException">If the provider does not support decompression.</exception>
    public ValueTask<Stream> CreateDecompressStreamAsync(
        CompressionType type,
        Stream source,
        CompressionContext context,
        CancellationToken cancellationToken = default
    )
    {
        var provider = GetProvider(type);
        if (provider is null)
        {
            throw new ArchiveOperationException(
                $"No compression provider registered for type: {type}"
            );
        }
        return provider.CreateDecompressStreamAsync(source, context, cancellationToken);
    }

    /// <summary>
    /// Gets the provider as an ICompressionProviderHooks if it supports complex initialization.
    /// </summary>
    /// <param name="type">The compression type.</param>
    /// <returns>The compressing provider, or null if the provider doesn't support complex initialization.</returns>
    public ICompressionProviderHooks? GetCompressingProvider(CompressionType type)
    {
        var provider = GetProvider(type);
        return provider as ICompressionProviderHooks;
    }

    /// <summary>
    /// Creates a new registry with the specified provider added or replaced.
    /// </summary>
    /// <param name="provider">The provider to add or replace.</param>
    /// <returns>A new registry instance with the provider included.</returns>
    /// <exception cref="ArgumentNullException">If provider is null.</exception>
    public CompressionProviderRegistry With(ICompressionProvider provider)
    {
        ThrowHelper.ThrowIfNull(provider);

        var newProviders = new Dictionary<CompressionType, ICompressionProvider>(_providers)
        {
            [provider.CompressionType] = provider,
        };

        return new CompressionProviderRegistry(newProviders);
    }

    private static CompressionProviderRegistry CreateDefault()
    {
        var providers = new Dictionary<CompressionType, ICompressionProvider>
        {
            [CompressionType.Deflate] = new DeflateCompressionProvider(),
            [CompressionType.GZip] = new GZipCompressionProvider(),
            [CompressionType.BZip2] = new BZip2CompressionProvider(),
            [CompressionType.ZStandard] = new ZStandardCompressionProvider(),
            [CompressionType.LZip] = new LZipCompressionProvider(),
            [CompressionType.Xz] = new XzCompressionProvider(),
            [CompressionType.Lzw] = new LzwCompressionProvider(),
            [CompressionType.Deflate64] = new Deflate64CompressionProvider(),
            [CompressionType.Shrink] = new ShrinkCompressionProvider(),
            [CompressionType.Reduce1] = new Reduce1CompressionProvider(),
            [CompressionType.Reduce2] = new Reduce2CompressionProvider(),
            [CompressionType.Reduce3] = new Reduce3CompressionProvider(),
            [CompressionType.Reduce4] = new Reduce4CompressionProvider(),
            [CompressionType.Explode] = new ExplodeCompressionProvider(),
            [CompressionType.LZMA] = new LzmaCompressingProvider(),
            [CompressionType.PPMd] = new PpmdCompressingProvider(),
        };

        return new CompressionProviderRegistry(providers);
    }

    private static CompressionProviderRegistry CreateEmpty()
    {
        var providers = new Dictionary<CompressionType, ICompressionProvider>();
        return new CompressionProviderRegistry(providers);
    }
}
