using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Factories;
using SharpCompress.Readers;
using SharpCompress.Services;
using ArchiveDetector = SharpCompress.Detection.ArchiveDetector;

namespace SharpCompress.Archives;

public static class ArchiveFactory
{
    private static IArchiveService Service => LibraryDefaults.Archives;

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

    [Obsolete("Use ArchiveExtractor.WriteToDirectory instead.")]
    public static void WriteToDirectory(
        string sourceArchive,
        string destinationDirectory,
        ExtractionOptions? options = null
    ) => ArchiveExtractor.WriteToDirectory(sourceArchive, destinationDirectory, options);

    [Obsolete("Use ArchiveDetector.FindFactory<T> instead.")]
    public static T FindFactory<T>(string filePath)
        where T : IFactory => ArchiveDetector.FindFactory<T>(filePath);

    [Obsolete("Use ArchiveDetector.FindFactory<T> instead.")]
    public static T FindFactory<T>(FileInfo finfo)
        where T : IFactory => ArchiveDetector.FindFactory<T>(finfo);

    [Obsolete("Use ArchiveDetector.FindFactory<T> instead.")]
    public static T FindFactory<T>(Stream stream)
        where T : IFactory => ArchiveDetector.FindFactory<T>(stream);

    [Obsolete("Use ArchiveDetector.FindFactoryAsync<T> instead.")]
    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => ArchiveDetector.FindFactoryAsync<T>(filePath, cancellationToken);

    [Obsolete("Use ArchiveDetector.FindFactoryAsync<T> instead.")]
    internal static ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => ArchiveDetector.FindFactoryAsync<T>(fileInfo, cancellationToken);

    [Obsolete("Use ArchiveDetector.FindFactoryAsync<T> instead.")]
    internal static ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory => ArchiveDetector.FindFactoryAsync<T>(stream, cancellationToken);

    [Obsolete("Use ArchiveDetector.IsArchive instead.")]
    public static bool IsArchive(string filePath, out ArchiveType? type) =>
        ArchiveDetector.IsArchive(filePath, out type);

    [Obsolete("Use ArchiveDetector.IsArchive instead.")]
    public static bool IsArchive(
        string filePath,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    ) => ArchiveDetector.IsArchive(filePath, readerOptions, out type);

    [Obsolete("Use ArchiveDetector.IsArchive instead.")]
    public static bool IsArchive(Stream stream, out ArchiveType? type) =>
        ArchiveDetector.IsArchive(stream, out type);

    [Obsolete("Use ArchiveDetector.IsArchive instead.")]
    public static bool IsArchive(
        Stream stream,
        ReaderOptions? readerOptions,
        out ArchiveType? type
    ) => ArchiveDetector.IsArchive(stream, readerOptions, out type);

    [Obsolete("Use ArchiveDetector.IsArchiveAsync instead.")]
    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) => ArchiveDetector.IsArchiveAsync(filePath, cancellationToken);

    [Obsolete("Use ArchiveDetector.IsArchiveAsync instead.")]
    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => ArchiveDetector.IsArchiveAsync(filePath, readerOptions, cancellationToken);

    [Obsolete("Use ArchiveDetector.IsArchiveAsync instead.")]
    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) => ArchiveDetector.IsArchiveAsync(stream, cancellationToken);

    [Obsolete("Use ArchiveDetector.IsArchiveAsync instead.")]
    public static ValueTask<(bool IsArchive, ArchiveType? Type)> IsArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    ) => ArchiveDetector.IsArchiveAsync(stream, readerOptions, cancellationToken);

    public static IEnumerable<string> GetFileParts(string part1) => Service.GetFileParts(part1);

    public static IEnumerable<FileInfo> GetFileParts(FileInfo part1) => Service.GetFileParts(part1);
}
