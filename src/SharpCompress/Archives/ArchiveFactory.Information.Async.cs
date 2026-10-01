using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Ace.Headers;
using SharpCompress.Common.Zip;
using SharpCompress.Detection;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    /// <summary>
    /// Collects metadata for the archive at the given file path.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the file is not a supported archive.</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await InspectArchiveAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Collects metadata for the archive at the given file path.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive inspection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the file is not a supported archive.</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        var options = readerOptions ?? ReaderOptions.ForFilePath;
        using Stream stream = File.OpenRead(filePath);
        var recognition = await TryRecognizeArchiveAsync(stream, options, true, cancellationToken)
            .ConfigureAwait(false);
        if (recognition is null)
        {
            return null;
        }
        if (
            recognition.Detection.OuterCompressionType is null
            && recognition.Factory is IArchiveFactory factory
        )
        {
            var fileInfos = GetArchiveFileParts(new FileInfo(filePath), factory);
            if (fileInfos.Length > 1)
            {
                return await InspectRecognizedFilesAsync(
                        fileInfos,
                        options,
                        recognition,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        return await InspectRecognizedStreamAsync(stream, options, recognition, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Collects metadata for the archive in the given stream.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the stream is not a supported archive.</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await InspectArchiveAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Collects metadata for the archive in the given stream.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive inspection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the stream is not a supported archive.</returns>
    /// <remarks>The supplied stream remains open and is restored to its original position.</remarks>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();
#if !SYNC_ONLY
        cancellationToken.ThrowIfCancellationRequested();
#endif

        var options = readerOptions ?? ReaderOptions.ForExternalStream;
        using var archiveStream = new ArchiveOffsetStream(stream);
        var recognition = await TryRecognizeArchiveAsync(
                archiveStream,
                options with
                {
                    LeaveStreamOpen = true,
                },
                true,
                cancellationToken
            )
            .ConfigureAwait(false);
        return recognition is null
            ? null
            : await InspectRecognizedStreamAsync(stream, options, recognition, cancellationToken)
                .ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<ArchiveInformation> InspectRecognizedStreamAsync(
        Stream stream,
        ReaderOptions options,
        ArchiveRecognition recognition,
        CancellationToken cancellationToken
    )
    {
        var startPosition = stream.Position;
        var physicalSize = GetPhysicalSize(stream, startPosition);
        var detection = recognition.Detection;

        try
        {
            using var archiveStream = new ArchiveOffsetStream(stream);
            var inspectionOptions = options with { LeaveStreamOpen = true };
            if (
                detection.OuterCompressionType is null
                && recognition.Factory is IArchiveFactory factory
            )
            {
#if SYNC_ONLY
                using var archive = factory.OpenArchive(archiveStream, inspectionOptions);
                return InspectOpenedArchive(archive, detection, physicalSize, 1);
#else
                await using var archive = await factory
                    .OpenAsyncArchive(archiveStream, inspectionOptions, cancellationToken)
                    .ConfigureAwait(false);
                return await InspectOpenedArchiveAsync(
                        archive,
                        detection,
                        physicalSize,
                        1,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
#endif
            }

#if SYNC_ONLY
            var aceHeader = ReadAceHeader(archiveStream, detection, inspectionOptions);
            using var reader =
                detection.OuterCompressionType is { } compressionType
                    ? new TarReader(archiveStream, inspectionOptions, compressionType)
                : recognition.Factory is IReaderFactory readerFactory
                    ? readerFactory.OpenReader(archiveStream, inspectionOptions)
                : ReaderFactory.OpenReader(archiveStream, inspectionOptions);
            return InspectOpenedReader(reader, detection, physicalSize, 1, aceHeader);
#else
            var aceHeader = await ReadAceHeaderAsync(
                    archiveStream,
                    detection,
                    inspectionOptions,
                    cancellationToken
                )
                .ConfigureAwait(false);
            await using var reader =
                detection.OuterCompressionType is { } compressionType
                    ? new TarReader(archiveStream, inspectionOptions, compressionType)
                : recognition.Factory is IReaderFactory readerFactory
                    ? await readerFactory
                        .OpenAsyncReader(archiveStream, inspectionOptions, cancellationToken)
                        .ConfigureAwait(false)
                : await ReaderFactory
                    .OpenAsyncReader(archiveStream, inspectionOptions, cancellationToken)
                    .ConfigureAwait(false);
            return await InspectOpenedReaderAsync(
                    reader,
                    detection,
                    physicalSize,
                    1,
                    aceHeader,
                    cancellationToken
                )
                .ConfigureAwait(false);
#endif
        }
        catch (CryptographicException) when (string.IsNullOrEmpty(options.Password))
        {
            return CreatePartialInformation(
                detection,
                physicalSize,
                1,
                ArchiveInformationLimitations.EncryptedHeaders
            );
        }
        catch (MultipartStreamRequiredException)
        {
            return CreatePartialInformation(
                detection,
                physicalSize,
                1,
                ArchiveInformationLimitations.MissingVolumes
            );
        }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Collects metadata for an archive opened from multiple files.
    /// </summary>
    /// <param name="fileInfos">Archive source files in archive order.</param>
    /// <param name="readerOptions">Options controlling archive inspection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the files are not a supported archive.</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        if (fileInfos.Count == 0)
        {
            throw new ArchiveOperationException("No files to inspect");
        }
        if (fileInfos.Count == 1)
        {
            return await InspectArchiveAsync(
                    fileInfos[0].FullName,
                    readerOptions,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var options = readerOptions ?? ReaderOptions.ForFilePath;
        using Stream stream = fileInfos[0].OpenRead();
        var recognition = await TryRecognizeArchiveAsync(stream, options, true, cancellationToken)
            .ConfigureAwait(false);
        if (recognition is null)
        {
            return null;
        }
        return await InspectRecognizedFilesAsync(fileInfos, options, recognition, cancellationToken)
            .ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<ArchiveInformation> InspectRecognizedFilesAsync(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions options,
        ArchiveRecognition recognition,
        CancellationToken cancellationToken
    )
    {
        var detection = recognition.Detection;
        if (
            detection.OuterCompressionType is not null
            || recognition.Factory is not IMultiArchiveFactory factory
        )
        {
            throw new NotSupportedException(
                "Inspecting multiple source files is supported only for formats with an Archive API."
            );
        }

        var physicalSize = GetPhysicalSize(fileInfos);
        try
        {
#if SYNC_ONLY
            using var archive = factory.OpenArchive(fileInfos, options);
            return InspectOpenedArchive(archive, detection, physicalSize, fileInfos.Count);
#else
            await using var archive = await factory
                .OpenAsyncArchive(fileInfos, options, cancellationToken)
                .ConfigureAwait(false);
            return await InspectOpenedArchiveAsync(
                    archive,
                    detection,
                    physicalSize,
                    fileInfos.Count,
                    cancellationToken
                )
                .ConfigureAwait(false);
#endif
        }
        catch (CryptographicException) when (string.IsNullOrEmpty(options.Password))
        {
            return CreatePartialInformation(
                detection,
                physicalSize,
                fileInfos.Count,
                ArchiveInformationLimitations.EncryptedHeaders
            );
        }
    }

    /// <summary>
    /// Collects metadata for an archive opened from multiple streams.
    /// </summary>
    /// <param name="streams">Archive source streams in archive order.</param>
    /// <param name="readerOptions">Options controlling archive inspection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the streams are not a supported archive.</returns>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        streams.NotNull(nameof(streams));
        if (streams.Count == 0)
        {
            throw new ArchiveOperationException("No streams to inspect");
        }
        if (streams.Count == 1)
        {
            return await InspectArchiveAsync(streams[0], readerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        var options = readerOptions ?? ReaderOptions.ForExternalStream;
        streams.RequireReadable().RequireSeekable();
        var startPositions = streams.Select(stream => stream.Position).ToArray();
        var physicalSize = GetPhysicalSize(streams, startPositions);
        using var firstArchiveStream = new ArchiveOffsetStream(streams[0]);
        var recognition = await TryRecognizeArchiveAsync(
                firstArchiveStream,
                options with
                {
                    LeaveStreamOpen = true,
                },
                true,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (recognition is null)
        {
            return null;
        }
        var detection = recognition.Detection;

        try
        {
            var inspectionOptions = options with { LeaveStreamOpen = true };
            if (
                detection.OuterCompressionType is not null
                || recognition.Factory is not IMultiArchiveFactory factory
            )
            {
                throw new NotSupportedException(
                    "Inspecting multiple source streams is supported only for formats with an Archive API."
                );
            }

            var archiveStreams = new List<Stream> { firstArchiveStream };
            archiveStreams.AddRange(
                streams.Skip(1).Select(stream => new ArchiveOffsetStream(stream))
            );
#if SYNC_ONLY
            using var archive = factory.OpenArchive(archiveStreams, inspectionOptions);
            return InspectOpenedArchive(archive, detection, physicalSize, streams.Count);
#else
            await using var archive = await factory
                .OpenAsyncArchive(archiveStreams, inspectionOptions, cancellationToken)
                .ConfigureAwait(false);
            return await InspectOpenedArchiveAsync(
                    archive,
                    detection,
                    physicalSize,
                    streams.Count,
                    cancellationToken
                )
                .ConfigureAwait(false);
#endif
        }
        catch (CryptographicException) when (string.IsNullOrEmpty(options.Password))
        {
            return CreatePartialInformation(
                detection,
                physicalSize,
                streams.Count,
                ArchiveInformationLimitations.EncryptedHeaders
            );
        }
        finally
        {
            for (var i = 0; i < streams.Count; i++)
            {
                streams[i].Seek(startPositions[i], SeekOrigin.Begin);
            }
        }
    }

    private static async ValueTask<ArchiveInformation> InspectOpenedArchiveAsync(
        IAsyncArchive archive,
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount,
        CancellationToken cancellationToken
    )
    {
        var entries = new List<IArchiveEntry>();
        await foreach (
            var entry in archive
                .EntriesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(entry);
        }

        var volumes = new List<IVolume>();
        await foreach (
            var volume in archive
                .VolumesAsync.WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            volumes.Add(volume);
        }

        var entryArray = entries.ToArray();
        var volumeArray = volumes.ToArray();
        var (zipInformation, deferredSizeEntryCount) = await GetZipInformationAsync(
                archive.Type == ArchiveType.Zip,
                entryArray,
                cancellationToken
            )
            .ConfigureAwait(false);
        var (isSolid, solidStreamCount) = await GetSolidInformationAsync(archive, entryArray)
            .ConfigureAwait(false);
        var isComplete = await archive.IsCompleteAsync().ConfigureAwait(false);
        return CreateArchiveInformation(
            archive.Type,
            entryArray,
            volumeArray,
            detection,
            physicalSize,
            physicalPartCount,
            zipInformation,
            deferredSizeEntryCount,
            isSolid,
            solidStreamCount,
            isComplete,
            isComplete
                ? GetCompressedPayloadSize(archive.Type, archive as SevenZipArchive, entryArray)
                : null
        );
    }

    private static async ValueTask<ArchiveInformation> InspectOpenedReaderAsync(
        IAsyncReader reader,
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount,
        AceMainHeader? aceHeader,
        CancellationToken cancellationToken
    )
    {
        var inspection = new ReaderInspection(detection);
        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            inspection.Add(reader.Entry);
        }

        return CreateReaderInformation(inspection, physicalSize, physicalPartCount, aceHeader);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<AceMainHeader?> ReadAceHeaderAsync(
        Stream stream,
        ArchiveDetection detection,
        ReaderOptions options,
        CancellationToken cancellationToken
    )
    {
        if (detection.ContainerType != ArchiveType.Ace)
        {
            return null;
        }

        try
        {
            stream.Position = 0;
            return await new AceMainHeader(options.ArchiveEncoding)
                    .ReadAsync(stream, cancellationToken)
                    .ConfigureAwait(false) as AceMainHeader;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static async ValueTask<(bool IsSolid, long SolidStreamCount)> GetSolidInformationAsync(
        IAsyncArchive archive,
        IReadOnlyCollection<IArchiveEntry> entries
    )
    {
        if (archive.Type == ArchiveType.SevenZip)
        {
            var solidStreamCount = CountSevenZipSolidStreams(entries);
            return (solidStreamCount > 0, solidStreamCount);
        }

        if (archive.Type == ArchiveType.Rar)
        {
            return (
                await archive.IsSolidAsync().ConfigureAwait(false),
                CountRarSolidStreams(entries)
            );
        }

        return (false, 0);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private static async ValueTask<(
        ZipArchiveInformation? Information,
        long DeferredSizeEntryCount
    )> GetZipInformationAsync(
        bool isZipArchive,
        IEnumerable<IArchiveEntry> entries,
        CancellationToken cancellationToken
    )
    {
        if (!isZipArchive)
        {
            return (null, 0);
        }

        long deferredSizeEntryCount = 0;
        foreach (var entry in entries.OfType<ZipArchiveEntry>())
        {
#if !SYNC_ONLY
            cancellationToken.ThrowIfCancellationRequested();
#endif
            var filePart = entry.Parts.OfType<SeekableZipFilePart>().Single();
            if (!filePart.HasDeferredSizes)
            {
                continue;
            }

            var localHeader = await filePart.GetRawLocalHeaderAsync().ConfigureAwait(false);
            if (localHeader.CompressedSize == 0 && localHeader.UncompressedSize == 0)
            {
                deferredSizeEntryCount++;
            }
        }

        return (new ZipArchiveInformation(deferredSizeEntryCount > 0), deferredSizeEntryCount);
    }
}
