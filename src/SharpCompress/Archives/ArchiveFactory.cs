using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>
/// Convenience entry points using the immutable default client.
/// Use <see cref="SharpCompressClient"/> for client-local configuration and injectable services.
/// </summary>
public static class ArchiveFactory
{
    private static ArchiveService Service => ClientDefaults.Client.Services.Archives;

    public static IArchive OpenArchive(Stream stream, ReaderOptions? readerOptions = null) =>
        Service.OpenArchive(stream, readerOptions);

    public static IArchive OpenArchive(string filePath, ReaderOptions? options = null) =>
        Service.OpenArchive(filePath, options);

    public static IArchive OpenArchive(FileInfo fileInfo, ReaderOptions? options = null) =>
        Service.OpenArchive(fileInfo, options);

    public static IArchive OpenArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? options = null
    ) => Service.OpenArchive(fileInfos, options);

    public static IArchive OpenArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null
    ) => Service.OpenArchive(streams, options);

    public static IWritableArchive<TOptions> CreateArchive<TOptions>()
        where TOptions : IWriterOptions => Service.CreateArchive<TOptions>();

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        Stream stream,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => Service.OpenAsyncArchive(stream, readerOptions, cancellationToken);

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Service.OpenAsyncArchive(filePath, options, cancellationToken);

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        FileInfo fileInfo,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Service.OpenAsyncArchive(fileInfo, options, cancellationToken);

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Service.OpenAsyncArchive(fileInfos, options, cancellationToken);

    public static ValueTask<IAsyncArchive> OpenAsyncArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Service.OpenAsyncArchive(streams, options, cancellationToken);

    public static void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    )
    {
        using var archive = Service.OpenArchive(sourceArchive);
        ClientDefaults.Client.Extractor.ExtractToDirectory(archive, destinationDirectory, options);
    }

    public static T FindFactory<T>(string filePath)
        where T : IFactory => Service.FindFactory<T>(filePath);

    public static T FindFactory<T>(FileInfo finfo)
        where T : IFactory => Service.FindFactory<T>(finfo);

    public static T FindFactory<T>(Stream stream)
        where T : IFactory => Service.FindFactory<T>(stream);

    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => Service.FindFactoryAsync<T>(filePath, cancellationToken);

    internal static ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => Service.FindFactoryAsync<T>(fileInfo, cancellationToken);

    internal static ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => Service.FindFactoryAsync<T>(stream, cancellationToken);

    public static bool IsArchive(string filePath, out ArchiveType? type) =>
        Service.IsArchive(filePath, out type);

    public static bool IsArchive(
        string filePath,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    ) => Service.IsArchive(filePath, readerOptions, out type);

    public static bool IsArchive(Stream stream, out ArchiveType? type) =>
        Service.IsArchive(stream, out type);

    public static bool IsArchive(
        Stream stream,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    ) => Service.IsArchive(stream, readerOptions, out type);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => Service.IsArchiveAsync(filePath, cancellationToken);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Service.IsArchiveAsync(filePath, readerOptions, cancellationToken);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Service.IsArchiveAsync(stream, cancellationToken);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Service.IsArchiveAsync(stream, readerOptions, cancellationToken);

    public static ArchiveDetection? DetectArchive(string filePath) =>
        Service.DetectArchive(filePath);

    public static ArchiveDetection? DetectArchive(string filePath, ReaderOptions? readerOptions) =>
        Service.DetectArchive(filePath, readerOptions);

    public static ArchiveDetection? DetectArchive(Stream stream) => Service.DetectArchive(stream);

    public static ArchiveDetection? DetectArchive(Stream stream, ReaderOptions? readerOptions) =>
        Service.DetectArchive(stream, readerOptions);

    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => Service.DetectArchiveAsync(filePath, cancellationToken);

    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Service.DetectArchiveAsync(filePath, readerOptions, cancellationToken);

    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Service.DetectArchiveAsync(stream, cancellationToken);

    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Service.DetectArchiveAsync(stream, readerOptions, cancellationToken);

    public static ArchiveInformation? InspectArchive(string filePath) =>
        Service.InspectArchive(filePath);

    public static ArchiveInformation? InspectArchive(
        string filePath,
        ReaderOptions? readerOptions
    ) => Service.InspectArchive(filePath, readerOptions);

    public static ArchiveInformation? InspectArchive(Stream stream) =>
        Service.InspectArchive(stream);

    public static ArchiveInformation? InspectArchive(Stream stream, ReaderOptions? readerOptions) =>
        Service.InspectArchive(stream, readerOptions);

    public static ArchiveInformation? InspectArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    ) => Service.InspectArchive(fileInfos, readerOptions);

    public static ArchiveInformation? InspectArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    ) => Service.InspectArchive(streams, readerOptions);

    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => Service.InspectArchiveAsync(filePath, cancellationToken);

    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Service.InspectArchiveAsync(filePath, readerOptions, cancellationToken);

    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Service.InspectArchiveAsync(stream, cancellationToken);

    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Service.InspectArchiveAsync(stream, readerOptions, cancellationToken);

    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => Service.InspectArchiveAsync(fileInfos, readerOptions, cancellationToken);

    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => Service.InspectArchiveAsync(streams, readerOptions, cancellationToken);

    public static IEnumerable<string> GetFileParts(string part1) => Service.GetFileParts(part1);

    public static IEnumerable<FileInfo> GetFileParts(FileInfo part1) => Service.GetFileParts(part1);
}
