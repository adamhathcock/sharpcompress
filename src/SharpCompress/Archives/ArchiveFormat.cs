using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>
/// Detects archive formats and reports format capabilities without opening an archive.
/// </summary>
public static class ArchiveFormat
{
    public static bool IsArchive(string filePath, out ArchiveType? type)
    {
        return IsArchive(filePath, ReaderOptions.ForFilePath, out type);
    }

    public static bool IsArchive(
        string filePath,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return IsArchive(stream, readerOptions ?? ReaderOptions.ForFilePath, out type);
    }

    public static bool IsArchive(Stream stream, out ArchiveType? type)
    {
        return IsArchive(stream, ReaderOptions.ForExternalStream, out type);
    }

    public static bool IsArchive(Stream stream, ReaderOptions? readerOptions, out ArchiveType? type)
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var match = FactoryDetection.Detect(
            stream,
            readerOptions ?? ReaderOptions.ForExternalStream,
            FactoryDetectionTarget.Identify
        );
        type = match.Factory?.KnownArchiveType;
        return match.Factory is not null;
    }

    public static async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await IsArchiveAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    public static async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await IsArchiveAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public static async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await IsArchiveAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    public static async ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var match = await FactoryDetection
            .DetectAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                FactoryDetectionTarget.Identify,
                cancellationToken
            )
            .ConfigureAwait(false);
        return (match.Factory is not null, match.Factory?.KnownArchiveType);
    }

    public static ArchiveFormatInfo? GetInfo(string filePath) =>
        GetInfo(filePath, ReaderOptions.ForFilePath);

    public static ArchiveFormatInfo? GetInfo(string filePath, ReaderOptions? readerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return GetInfo(stream, readerOptions ?? ReaderOptions.ForFilePath);
    }

    public static ArchiveFormatInfo? GetInfo(Stream stream) =>
        GetInfo(stream, ReaderOptions.ForExternalStream);

    public static ArchiveFormatInfo? GetInfo(Stream stream, ReaderOptions? readerOptions)
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var match = FactoryDetection.Detect(
            stream,
            readerOptions ?? ReaderOptions.ForExternalStream,
            FactoryDetectionTarget.Archive
        );
        return match.Factory is null
            ? null
            : new ArchiveFormatInfo(
                match.Factory.KnownArchiveType,
                match.CompressionType,
                match.Result == FactoryDetectionResult.Match && match.Factory is IArchiveFactory
            );
    }

    public static async ValueTask<ArchiveFormatInfo?> GetInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await GetInfoAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    public static async ValueTask<ArchiveFormatInfo?> GetInfoAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await GetInfoAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public static async ValueTask<ArchiveFormatInfo?> GetInfoAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await GetInfoAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    public static async ValueTask<ArchiveFormatInfo?> GetInfoAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var match = await FactoryDetection
            .DetectAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                FactoryDetectionTarget.Archive,
                cancellationToken
            )
            .ConfigureAwait(false);
        return match.Factory is null
            ? null
            : new ArchiveFormatInfo(
                match.Factory.KnownArchiveType,
                match.CompressionType,
                match.Result == FactoryDetectionResult.Match && match.Factory is IArchiveFactory
            );
    }
}
