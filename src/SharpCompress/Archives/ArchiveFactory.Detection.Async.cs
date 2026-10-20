using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Providers;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    /// <summary>
    /// Identifies the archive at the given file path without enumerating its entries.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await DetectArchiveAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Identifies the archive at the given file path without enumerating its entries.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await DetectArchiveAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Identifies the archive in the given stream without enumerating its entries.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await DetectArchiveAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Identifies the archive in the given stream without enumerating its entries.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        return await TryDetectArchiveAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return FindFactoryAsync<T>(new FileInfo(filePath), cancellationToken);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        fileInfo.NotNull(nameof(fileInfo));
        using Stream stream = fileInfo.OpenRead();
        return await FindFactoryAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
        where T : IFactory
    {
        fileInfo.NotNull(nameof(fileInfo));
        using Stream stream = fileInfo.OpenRead();
        return await FindFactoryAsync<T>(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        // Use the shared async detection loop over all factories. If the matched factory
        // implements T we return it; otherwise (or if nothing matched) we fall through
        // to the same "unsupported format" exception that the original code produced,
        // listing the T-typed factories as the hint for the caller.
        return await FindFactoryAsync<T>(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = await TryFindFactoryAsync(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
        if (factory is T typedFactory)
        {
            return typedFactory;
        }

        var extensions = string.Join(", ", Factory.Factories.OfType<T>().Select(item => item.Name));

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }

    /// <summary>
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchiveAsync"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<IFactory?> TryFindFactoryAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
    {
        var startPosition = stream.Position;

        try
        {
            foreach (var factory in Factory.Factories)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                var isArchive = await factory
                    .IsArchiveAsync(stream, readerOptions, cancellationToken)
                    .ConfigureAwait(false);

                if (isArchive)
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    if (
                        await IsCompressedTarAsync(
                                stream,
                                factory,
                                readerOptions,
                                cancellationToken
                            )
                            .ConfigureAwait(false)
                    )
                    {
                        continue;
                    }

                    return factory;
                }
            }

            return null;
        }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<bool> IsCompressedTarAsync(
        Stream stream,
        IFactory factory,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    ) =>
        GetCompressedTarType(factory) is { } compressionType
        && await IsCompressedTarAsync(stream, readerOptions, compressionType, cancellationToken)
            .ConfigureAwait(false);

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<bool> IsCompressedTarAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CompressionType compressionType,
        CancellationToken cancellationToken
    )
    {
        using var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        var testStream =
            compressionType == CompressionType.GZip
                ? await readerOptions
                    .Providers.CreateDecompressStreamAsync(
                        compressionType,
                        nonDisposingStream,
                        CompressionContext
                            .FromStream(nonDisposingStream)
                            .WithReaderOptions(readerOptions),
                        cancellationToken
                    )
                    .ConfigureAwait(false)
                : await readerOptions
                    .Providers.CreateDecompressStreamAsync(
                        compressionType,
                        nonDisposingStream,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

        try
        {
            return await TarArchive
                .IsTarFileAsync(testStream, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            DisposeProbeStream(testStream);
        }
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<ArchiveDetection?> TryDetectArchiveAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
    {
        var startPosition = stream.Position;

        try
        {
            foreach (var factory in Factory.Factories)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                if (
                    !await factory
                        .IsArchiveAsync(stream, readerOptions, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    continue;
                }

                if (GetCompressedTarType(factory) is { } compressionType)
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    if (
                        await IsCompressedTarAsync(
                                stream,
                                readerOptions,
                                compressionType,
                                cancellationToken
                            )
                            .ConfigureAwait(false)
                    )
                    {
                        return CreateCompressedTarDetection(compressionType);
                    }
                }

                return CreateDetection(factory);
            }

            var compressedTarType = await TryDetectCompressedTarAsync(
                    stream,
                    readerOptions,
                    startPosition,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return compressedTarType is { } value ? CreateCompressedTarDetection(value) : null;
        }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<CompressionType?> TryDetectCompressedTarAsync(
        Stream stream,
        ReaderOptions readerOptions,
        long startPosition,
        CancellationToken cancellationToken
    )
    {
        foreach (var wrapper in TarWrapper.Wrappers)
        {
#if !SYNC_ONLY
            cancellationToken.ThrowIfCancellationRequested();
#endif
            if (wrapper.CompressionType == CompressionType.None)
            {
                continue;
            }

            stream.Seek(startPosition, SeekOrigin.Begin);
            if (!await wrapper.IsMatchAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            stream.Seek(startPosition, SeekOrigin.Begin);
            if (
                await IsCompressedTarAsync(
                        stream,
                        readerOptions,
                        wrapper.CompressionType,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                return wrapper.CompressionType;
            }
        }

        return null;
    }
}
