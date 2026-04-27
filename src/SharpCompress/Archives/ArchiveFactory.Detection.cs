using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    /// <summary>
    /// Returns information about the archive at the given file path asynchronously,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await GetArchiveInformationAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Returns information about the archive at the given file path asynchronously,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await GetArchiveInformationAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns information about the archive in the given stream asynchronously,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await GetArchiveInformationAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Returns information about the archive in the given stream asynchronously,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = await TryFindFactoryAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                cancellationToken
            )
            .ConfigureAwait(false);
        return factory is null
            ? null
            : new ArchiveInformation(factory.KnownArchiveType, factory is IArchiveFactory);
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
        var factory = await TryFindFactoryAsync(stream, cancellationToken).ConfigureAwait(false);
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
    /// Async counterpart of <see cref="ArchiveFactory.TryFindFactory"/>.
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchiveAsync"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
    private static async ValueTask<IFactory?> TryFindFactoryAsync(
        Stream stream,
        CancellationToken cancellationToken
    ) =>
        await TryFindFactoryAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    private static async ValueTask<IFactory?> TryFindFactoryAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
    {
        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            var isArchive = await factory
                .IsArchiveAsync(stream, readerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (isArchive)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                return factory;
            }
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        return null;
    }

    /// <summary>
    /// Returns information about the archive at the given file path,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    public static ArchiveInformation? GetArchiveInformation(string filePath) =>
        GetArchiveInformation(filePath, ReaderOptions.ForFilePath);

    /// <summary>
    /// Returns information about the archive at the given file path,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    public static ArchiveInformation? GetArchiveInformation(
        string filePath,
        ReaderOptions? readerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return GetArchiveInformation(stream, readerOptions ?? ReaderOptions.ForFilePath);
    }

    /// <summary>
    /// Returns information about the archive in the given stream,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    public static ArchiveInformation? GetArchiveInformation(Stream stream) =>
        GetArchiveInformation(stream, ReaderOptions.ForExternalStream);

    /// <summary>
    /// Returns information about the archive in the given stream,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    public static ArchiveInformation? GetArchiveInformation(
        Stream stream,
        ReaderOptions? readerOptions
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = TryFindFactory(stream, readerOptions ?? ReaderOptions.ForExternalStream);
        return factory is null
            ? null
            : new ArchiveInformation(factory.KnownArchiveType, factory is IArchiveFactory);
    }

    /// <summary>
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchive"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
    /// <remarks>
    /// This is the shared, seekable-stream detection core used by
    /// <see cref="FindFactory{T}(Stream)"/>, <see cref="IsArchive(Stream, out ArchiveType?)"/>,
    /// and <see cref="GetArchiveInformation(Stream)"/>.
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

        foreach (var factory in Factory.Factories)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            var isArchive = factory.IsArchive(stream, readerOptions);

            if (isArchive)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                return factory;
            }
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        return null;
    }
}
