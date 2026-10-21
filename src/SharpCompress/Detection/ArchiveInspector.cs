using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Readers;
using SharpCompress.Services;

namespace SharpCompress.Detection;

/// <summary>Inspect archive metadata.</summary>
public static partial class ArchiveInspector
{
    private static IArchiveInspector Inspector => LibraryDefaults.Inspector;

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
}
