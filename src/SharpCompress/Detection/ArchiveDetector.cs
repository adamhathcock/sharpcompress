using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Services;

namespace SharpCompress.Detection;

/// <summary>Identify archive formats and their supported APIs.</summary>
public static partial class ArchiveDetector
{
    private static IArchiveDetector Detector => LibraryDefaults.Detector;

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
}
