using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Services;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    private static IArchiveService Service => LibraryDefaults.Composition.Archives;
    private static IArchiveDetector Detector => LibraryDefaults.Composition.Detector;
    private static IArchiveInspector Inspector => LibraryDefaults.Composition.Inspector;

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
    ) =>
        LibraryDefaults.Composition.Extractor.WriteToDirectory(
            sourceArchive,
            destinationDirectory,
            options
        );

    public static T FindFactory<T>(string filePath)
        where T : IFactory => Detector.FindFactory<T>(filePath);

    public static T FindFactory<T>(FileInfo finfo)
        where T : IFactory => Detector.FindFactory<T>(finfo);

    public static T FindFactory<T>(Stream stream)
        where T : IFactory => Detector.FindFactory<T>(stream);

    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => Detector.FindFactoryAsync<T>(filePath, cancellationToken);

    internal static ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => Detector.FindFactoryAsync<T>(fileInfo, cancellationToken);

    internal static ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => Detector.FindFactoryAsync<T>(stream, cancellationToken);

    public static bool IsArchive(string filePath, out ArchiveType? type) =>
        Detector.IsArchive(filePath, out type);

    public static bool IsArchive(
        string filePath,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    ) => Detector.IsArchive(filePath, readerOptions, out type);

    public static bool IsArchive(Stream stream, out ArchiveType? type) =>
        Detector.IsArchive(stream, out type);

    public static bool IsArchive(
        Stream stream,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    ) => Detector.IsArchive(stream, readerOptions, out type);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => Detector.IsArchiveAsync(filePath, cancellationToken);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Detector.IsArchiveAsync(filePath, readerOptions, cancellationToken);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Detector.IsArchiveAsync(stream, cancellationToken);

    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Detector.IsArchiveAsync(stream, readerOptions, cancellationToken);

    /// <summary>Identify the archive without enumerating its entries.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => Detector.DetectArchiveAsync(filePath, cancellationToken);

    /// <summary>Identify the archive without enumerating its entries.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Detector.DetectArchiveAsync(filePath, readerOptions, cancellationToken);

    /// <summary>Identify the archive without enumerating its entries. Restore the stream position.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Detector.DetectArchiveAsync(stream, cancellationToken);

    /// <summary>Identify the archive without enumerating its entries. Restore the stream position.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Detector.DetectArchiveAsync(stream, readerOptions, cancellationToken);

    /// <summary>Inspect archive metadata.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => Inspector.InspectArchiveAsync(filePath, cancellationToken);

    /// <summary>Inspect archive metadata.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Inspector.InspectArchiveAsync(filePath, readerOptions, cancellationToken);

    /// <summary>Inspect archive metadata, leaving the stream open at its original position.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => Inspector.InspectArchiveAsync(stream, cancellationToken);

    /// <summary>Inspect archive metadata, leaving the stream open at its original position.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => Inspector.InspectArchiveAsync(stream, readerOptions, cancellationToken);

    /// <summary>Inspect multipart archive metadata.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => Inspector.InspectArchiveAsync(fileInfos, readerOptions, cancellationToken);

    /// <summary>Inspect multipart archive metadata, leaving the streams open at their original positions.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    ) => Inspector.InspectArchiveAsync(streams, readerOptions, cancellationToken);

    public static IEnumerable<string> GetFileParts(string part1) => Service.GetFileParts(part1);

    public static IEnumerable<FileInfo> GetFileParts(FileInfo part1) => Service.GetFileParts(part1);
}
