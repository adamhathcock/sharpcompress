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

        var match = await Factory
            .DetectFactoryAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                FactoryDetectionTarget.Archive,
                cancellationToken
            )
            .ConfigureAwait(false);
        return match.Factory is null
            ? null
            : new ArchiveInformation(
                match.Factory.KnownArchiveType,
                match.Result == FactoryDetectionResult.Match && match.Factory is IArchiveFactory
            );
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

        var target = GetDetectionTarget<T>();
        var match = await Factory
            .DetectFactoryAsync(stream, ReaderOptions.ForExternalStream, target, cancellationToken)
            .ConfigureAwait(false);
        if (match.Result == FactoryDetectionResult.Match && match.Factory is T typedFactory)
        {
            return typedFactory;
        }

        var extensions = string.Join(", ", Factory.Factories.OfType<T>().Select(item => item.Name));

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
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

        var match = Factory.DetectFactory(
            stream,
            readerOptions ?? ReaderOptions.ForExternalStream,
            FactoryDetectionTarget.Archive
        );
        return match.Factory is null
            ? null
            : new ArchiveInformation(
                match.Factory.KnownArchiveType,
                match.Result == FactoryDetectionResult.Match && match.Factory is IArchiveFactory
            );
    }
}
