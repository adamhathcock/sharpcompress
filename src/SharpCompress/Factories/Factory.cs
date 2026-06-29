using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Factories;

/// <inheritdoc/>
public abstract class Factory : IFactory
{
    static Factory()
    {
        RegisterFactory(new ZipFactory());
        RegisterFactory(new RarFactory());
        RegisterFactory(new TarFactory()); //put tar before most
        RegisterFactory(new GZipFactory());
        RegisterFactory(new LzwFactory());
        RegisterFactory(new ArcFactory());
        RegisterFactory(new ArjFactory());
        RegisterFactory(new AceFactory());
        RegisterFactory(new SevenZipFactory());
    }

    private static readonly HashSet<Factory> _factories = new();

    /// <summary>
    /// Gets the collection of registered <see cref="IFactory"/>.
    /// </summary>
    public static IEnumerable<IFactory> Factories => _factories;

    /// <summary>
    /// Registers an archive factory.
    /// </summary>
    /// <param name="factory">The factory to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> must not be null.</exception>
    public static void RegisterFactory(Factory factory)
    {
        factory.NotNull(nameof(factory));

        _factories.Add(factory);
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual ArchiveType? KnownArchiveType => null;

    internal virtual CompressionType? KnownCompressionType => null;

    /// <inheritdoc/>
    public abstract IEnumerable<string> GetSupportedExtensions();

    /// <inheritdoc/>
    public abstract bool IsArchive(Stream stream, ReaderOptions readerOptions);
    public abstract ValueTask<bool> IsArchiveAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken = default
    );

    internal virtual int? MinimumReaderDetectionBufferSize => null;

    internal virtual FactoryDetectionResult Detect(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target
    )
    {
        if (!IsArchive(stream, readerOptions))
        {
            return FactoryDetectionResult.NoMatch;
        }

        return SupportsTarget(target)
            ? FactoryDetectionResult.Match
            : FactoryDetectionResult.Unsupported;
    }

    internal virtual FactoryDetectionMatch DetectMatch(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target
    )
    {
        var result = Detect(stream, readerOptions, target);
        return CreateDetectionMatch(result);
    }

    internal virtual async ValueTask<FactoryDetectionResult> DetectAsync(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target,
        CancellationToken cancellationToken = default
    )
    {
        if (!await IsArchiveAsync(stream, readerOptions, cancellationToken).ConfigureAwait(false))
        {
            return FactoryDetectionResult.NoMatch;
        }

        return SupportsTarget(target)
            ? FactoryDetectionResult.Match
            : FactoryDetectionResult.Unsupported;
    }

    internal virtual async ValueTask<FactoryDetectionMatch> DetectMatchAsync(
        Stream stream,
        ReaderOptions readerOptions,
        FactoryDetectionTarget target,
        CancellationToken cancellationToken = default
    )
    {
        var result = await DetectAsync(stream, readerOptions, target, cancellationToken)
            .ConfigureAwait(false);
        return CreateDetectionMatch(result);
    }

    private FactoryDetectionMatch CreateDetectionMatch(FactoryDetectionResult result) =>
        result == FactoryDetectionResult.NoMatch
            ? new FactoryDetectionMatch(result, null, null)
            : new FactoryDetectionMatch(result, this, KnownCompressionType);

    private bool SupportsTarget(FactoryDetectionTarget target) =>
        target switch
        {
            FactoryDetectionTarget.Identify => true,
            FactoryDetectionTarget.Archive => this is IArchiveFactory,
            FactoryDetectionTarget.Reader => this is IReaderFactory,
            _ => false,
        };

    /// <inheritdoc/>
    public virtual FileInfo? GetFilePart(int index, FileInfo part1) => null;

    /// <summary>
    /// Tries to open an <see cref="IReader"/> from a <see cref="SharpCompressStream"/>.
    /// </summary>
    /// <remarks>
    /// This method provides extra insight to support loading compressed TAR files.
    /// </remarks>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    internal virtual bool TryOpenReader(
        SharpCompressStream stream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        if (this is IReaderFactory readerFactory)
        {
            stream.Rewind();
            if (
                Detect(stream, options, FactoryDetectionTarget.Reader)
                == FactoryDetectionResult.Match
            )
            {
                stream.Rewind(true);
                reader = readerFactory.OpenReader(stream, options);
                return true;
            }
        }
        stream.Rewind();
        return false;
    }

    internal virtual async ValueTask<IAsyncReader?> TryOpenReaderAsync(
        SharpCompressStream stream,
        ReaderOptions options,
        CancellationToken cancellationToken = default
    )
    {
        if (this is IReaderFactory readerFactory)
        {
            stream.Rewind();
            if (
                await DetectAsync(stream, options, FactoryDetectionTarget.Reader, cancellationToken)
                    .ConfigureAwait(false) == FactoryDetectionResult.Match
            )
            {
                stream.Rewind(true);
                return await readerFactory
                    .OpenAsyncReader(stream, options, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        stream.Rewind();
        return null;
    }
}

internal enum FactoryDetectionTarget
{
    Identify,
    Archive,
    Reader,
}

internal enum FactoryDetectionResult
{
    NoMatch,
    Match,
    Unsupported,
}

internal readonly record struct FactoryDetectionMatch(
    FactoryDetectionResult Result,
    Factory? Factory,
    CompressionType? CompressionType
);
