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
    /// Async counterpart of the synchronous factory detection path.
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchiveAsync"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
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

    /// <summary>
    /// Identifies the archive at the given file path without enumerating its entries.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    public static ArchiveDetection? DetectArchive(string filePath) =>
        DetectArchive(filePath, ReaderOptions.ForFilePath);

    /// <summary>
    /// Identifies the archive at the given file path without enumerating its entries.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    public static ArchiveDetection? DetectArchive(string filePath, ReaderOptions? readerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return DetectArchive(stream, readerOptions ?? ReaderOptions.ForFilePath);
    }

    /// <summary>
    /// Identifies the archive in the given stream without enumerating its entries.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    public static ArchiveDetection? DetectArchive(Stream stream) =>
        DetectArchive(stream, ReaderOptions.ForExternalStream);

    /// <summary>
    /// Identifies the archive in the given stream without enumerating its entries.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    public static ArchiveDetection? DetectArchive(Stream stream, ReaderOptions? readerOptions)
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        return TryDetectArchive(stream, readerOptions ?? ReaderOptions.ForExternalStream);
    }

    /// <summary>
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchive"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
    /// <remarks>
    /// This is the shared, seekable-stream detection core used by
    /// <see cref="FindFactory{T}(Stream)"/>, <see cref="IsArchive(Stream, out ArchiveType?)"/>,
    /// and <see cref="DetectArchive(Stream)"/>.
    /// <para>
    /// <see cref="ReaderFactory.OpenReader(Stream, ReaderOptions)"/> uses a separate code path
    /// based on <see cref="IO.SharpCompressStream"/> rewindable buffering, which supports
    /// non-seekable streams and is therefore not unified with this helper.
    /// </para>
    /// </remarks>
    private static IFactory? TryFindFactory(Stream stream) =>
        TryFindFactory(stream, ReaderOptions.ForExternalStream);

    private static IFactory? TryFindFactory(Stream stream, ReaderOptions readerOptions)
    {
        var startPosition = stream.Position;

        try
        {
            foreach (var factory in Factory.Factories)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                var isArchive = factory.IsArchive(stream, readerOptions);

                if (isArchive)
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    if (IsCompressedTar(stream, factory, readerOptions))
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

    private static bool IsCompressedTar(
        Stream stream,
        IFactory factory,
        ReaderOptions readerOptions
    ) =>
        GetCompressedTarType(factory) is { } compressionType
        && IsCompressedTar(stream, readerOptions, compressionType);

    private static bool IsCompressedTar(
        Stream stream,
        ReaderOptions readerOptions,
        CompressionType compressionType
    )
    {
        using var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        var testStream =
            compressionType == CompressionType.GZip
                ? readerOptions.Providers.CreateDecompressStream(
                    compressionType,
                    nonDisposingStream,
                    CompressionContext
                        .FromStream(nonDisposingStream)
                        .WithReaderOptions(readerOptions)
                )
                : readerOptions.Providers.CreateDecompressStream(
                    compressionType,
                    nonDisposingStream
                );

        try
        {
            return TarArchive.IsTarFile(testStream);
        }
        finally
        {
            DisposeProbeStream(testStream);
        }
    }

    private static async ValueTask<bool> IsCompressedTarAsync(
        Stream stream,
        IFactory factory,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    ) =>
        GetCompressedTarType(factory) is { } compressionType
        && await IsCompressedTarAsync(stream, readerOptions, compressionType, cancellationToken)
            .ConfigureAwait(false);

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

    private static void DisposeProbeStream(Stream stream)
    {
        try
        {
#pragma warning disable VSTHRD103 // Probe streams may validate unread trailers during disposal.
            stream.Dispose();
#pragma warning restore VSTHRD103
        }
        catch
        {
            // Probes intentionally read only enough data to identify tar content.
        }
    }

    private static CompressionType? GetCompressedTarType(IFactory factory) =>
        factory switch
        {
            GZipFactory => CompressionType.GZip,
            LzwFactory => CompressionType.Lzw,
            _ => null,
        };

    private static ArchiveDetection? TryDetectArchive(Stream stream, ReaderOptions readerOptions)
    {
        var startPosition = stream.Position;

        try
        {
            foreach (var factory in Factory.Factories)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                if (!factory.IsArchive(stream, readerOptions))
                {
                    continue;
                }

                if (GetCompressedTarType(factory) is { } compressionType)
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    if (IsCompressedTar(stream, readerOptions, compressionType))
                    {
                        return CreateCompressedTarDetection(compressionType);
                    }
                }

                return CreateDetection(factory);
            }

            return
                TryDetectCompressedTar(stream, readerOptions, startPosition)
                    is { } compressedTarType
                ? CreateCompressedTarDetection(compressedTarType)
                : null;
        }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }
    }

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

    private static CompressionType? TryDetectCompressedTar(
        Stream stream,
        ReaderOptions readerOptions,
        long startPosition
    )
    {
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            if (wrapper.CompressionType == CompressionType.None)
            {
                continue;
            }

            stream.Seek(startPosition, SeekOrigin.Begin);
            if (!wrapper.IsMatch(stream))
            {
                continue;
            }

            stream.Seek(startPosition, SeekOrigin.Begin);
            if (IsCompressedTar(stream, readerOptions, wrapper.CompressionType))
            {
                return wrapper.CompressionType;
            }
        }

        return null;
    }

    private static async ValueTask<CompressionType?> TryDetectCompressedTarAsync(
        Stream stream,
        ReaderOptions readerOptions,
        long startPosition,
        CancellationToken cancellationToken
    )
    {
        foreach (var wrapper in TarWrapper.Wrappers)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    private static ArchiveDetection CreateDetection(IFactory factory)
    {
        var supportedApis = ArchiveAccessMode.None;
        if (factory is IArchiveFactory)
        {
            supportedApis |= ArchiveAccessMode.Archive;
        }
        if (factory is IReaderFactory)
        {
            supportedApis |= ArchiveAccessMode.Reader;
        }

        return new ArchiveDetection(factory.KnownArchiveType, factory.Name, null, supportedApis);
    }

    private static ArchiveDetection CreateCompressedTarDetection(CompressionType compressionType) =>
        new(ArchiveType.Tar, "Tar", compressionType, ArchiveAccessMode.Reader);
}
