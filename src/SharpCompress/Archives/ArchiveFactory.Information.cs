using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using AceMainHeader = SharpCompress.Common.Ace.Headers.AceMainHeader;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    /// <summary>
    /// Collects metadata for the archive at the given file path.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the file is not a supported archive.</returns>
    public static ArchiveInformation? InspectArchive(string filePath) =>
        InspectArchive(filePath, ReaderOptions.ForFilePath);

    /// <summary>
    /// Collects metadata for the archive at the given file path.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive inspection.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the file is not a supported archive.</returns>
    public static ArchiveInformation? InspectArchive(string filePath, ReaderOptions? readerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        var options = readerOptions ?? ReaderOptions.ForFilePath;
        var detection = DetectArchive(filePath, options);
        if (detection is null)
        {
            return null;
        }
        if ((detection.SupportedApis & ArchiveAccessMode.Archive) != 0)
        {
            var fileInfos = GetArchiveFileParts(new FileInfo(filePath), options);
            if (fileInfos.Length > 1)
            {
                return InspectArchive(fileInfos, options);
            }
        }

        using Stream stream = File.OpenRead(filePath);
        return InspectArchive(stream, options);
    }

    /// <summary>
    /// Collects metadata for the archive in the given stream.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the stream is not a supported archive.</returns>
    public static ArchiveInformation? InspectArchive(Stream stream) =>
        InspectArchive(stream, ReaderOptions.ForExternalStream);

    /// <summary>
    /// Collects metadata for the archive in the given stream.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive inspection.</param>
    /// <returns>Archive metadata, or <see langword="null"/> when the stream is not a supported archive.</returns>
    /// <remarks>The supplied stream remains open and is restored to its original position.</remarks>
    public static ArchiveInformation? InspectArchive(Stream stream, ReaderOptions? readerOptions)
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var options = readerOptions ?? ReaderOptions.ForExternalStream;
        var startPosition = stream.Position;
        var physicalSize = GetPhysicalSize(stream, startPosition);

        try
        {
            using var archiveStream = new ArchiveOffsetStream(stream);
            var inspectionOptions = options with { LeaveStreamOpen = true };
            var detection = TryDetectArchive(archiveStream, inspectionOptions);
            if (detection is null)
            {
                return null;
            }

            if ((detection.SupportedApis & ArchiveAccessMode.Archive) != 0)
            {
                using var archive = OpenArchive(archiveStream, inspectionOptions);
                return InspectOpenedArchive(archive, detection, physicalSize, 1);
            }

            var aceHeader = ReadAceHeader(archiveStream, detection, inspectionOptions);
            using var reader = ReaderFactory.OpenReader(archiveStream, inspectionOptions);
            return InspectOpenedReader(reader, detection, physicalSize, 1, aceHeader);
        }
        catch (CryptographicException) when (string.IsNullOrEmpty(options.Password))
        {
            var detection = RedetectArchive(stream, startPosition, options);
            return detection is null
                ? null
                : CreatePartialInformation(
                    detection,
                    physicalSize,
                    1,
                    ArchiveInformationLimitations.EncryptedHeaders
                );
        }
        catch (MultipartStreamRequiredException)
        {
            var detection = RedetectArchive(stream, startPosition, options);
            return detection is null
                ? null
                : CreatePartialInformation(
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
    /// <returns>Archive metadata, or <see langword="null"/> when the files are not a supported archive.</returns>
    public static ArchiveInformation? InspectArchive(
        IReadOnlyList<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.NotNull(nameof(fileInfos));
        if (fileInfos.Count == 0)
        {
            throw new ArchiveOperationException("No files to inspect");
        }
        if (fileInfos.Count == 1)
        {
            return InspectArchive(fileInfos[0].FullName, readerOptions);
        }

        var options = readerOptions ?? ReaderOptions.ForFilePath;
        var detection = DetectArchive(fileInfos[0].FullName, options);
        if (detection is null)
        {
            return null;
        }
        if ((detection.SupportedApis & ArchiveAccessMode.Archive) == 0)
        {
            throw new NotSupportedException(
                "Inspecting multiple source files is supported only for formats with an Archive API."
            );
        }

        var physicalSize = GetPhysicalSize(fileInfos);
        try
        {
            using var archive = OpenArchive(fileInfos, options);
            return InspectOpenedArchive(archive, detection, physicalSize, fileInfos.Count);
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
    /// <returns>Archive metadata, or <see langword="null"/> when the streams are not a supported archive.</returns>
    public static ArchiveInformation? InspectArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? readerOptions = null
    )
    {
        streams.NotNull(nameof(streams));
        if (streams.Count == 0)
        {
            throw new ArchiveOperationException("No streams to inspect");
        }
        if (streams.Count == 1)
        {
            return InspectArchive(streams[0], readerOptions);
        }

        var options = readerOptions ?? ReaderOptions.ForExternalStream;
        var startPositions = streams.Select(stream => stream.Position).ToArray();
        var physicalSize = GetPhysicalSize(streams, startPositions);

        try
        {
            using var firstArchiveStream = new ArchiveOffsetStream(streams[0]);
            var inspectionOptions = options with { LeaveStreamOpen = true };
            var detection = TryDetectArchive(firstArchiveStream, inspectionOptions);
            if (detection is null)
            {
                return null;
            }
            if ((detection.SupportedApis & ArchiveAccessMode.Archive) == 0)
            {
                throw new NotSupportedException(
                    "Inspecting multiple source streams is supported only for formats with an Archive API."
                );
            }

            var archiveStreams = new List<Stream> { firstArchiveStream };
            archiveStreams.AddRange(
                streams.Skip(1).Select(stream => new ArchiveOffsetStream(stream))
            );
            using var archive = OpenArchive(archiveStreams, inspectionOptions);
            return InspectOpenedArchive(archive, detection, physicalSize, streams.Count);
        }
        catch (CryptographicException) when (string.IsNullOrEmpty(options.Password))
        {
            var detection = RedetectArchive(streams[0], startPositions[0], options);
            return detection is null
                ? null
                : CreatePartialInformation(
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

    private static ArchiveInformation InspectOpenedArchive(
        IArchive archive,
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount
    )
    {
        var entries = archive.Entries.ToArray();
        var volumes = archive.Volumes.ToArray();
        var zip = GetZipInformation(entries);
        var (isSolid, solidStreamCount) = GetSolidInformation(archive, entries);
        var isMultiVolume = GetIsMultiVolume(archive.Type, volumes);
        var comment = GetArchiveComment(volumes);
        var hasEncryptedHeaders = volumes
            .OfType<RarVolume>()
            .Any(volume => volume.IsHeaderEncrypted);
        var hasEncryptedEntries = entries.Any(entry => entry.IsEncrypted);
        var isComplete = archive.IsComplete;
        var limitations = isComplete
            ? ArchiveInformationLimitations.None
            : ArchiveInformationLimitations.MissingVolumes;
        if (archive.Type == ArchiveType.GZip)
        {
            limitations |= ArchiveInformationLimitations.UnavailableMetadata;
        }

        return new ArchiveInformation(
            detection,
            GetStatus(limitations),
            limitations,
            GetFormatVersion(volumes),
            entries.LongLength,
            zip?.DataDescriptorEntryCount ?? 0,
            physicalSize,
            isComplete ? GetCompressedPayloadSize(archive, entries) : null,
            isComplete && archive.Type != ArchiveType.GZip
                ? entries.Aggregate(0L, (total, entry) => total + entry.Size)
                : null,
            isSolid,
            solidStreamCount,
            GetEncryptionScope(hasEncryptedHeaders, hasEncryptedEntries),
            hasEncryptedHeaders || hasEncryptedEntries,
            isMultiVolume,
            physicalPartCount,
            volumes.Length,
            isComplete,
            comment,
            zip
        );
    }

    private static ArchiveInformation InspectOpenedReader(
        IReader reader,
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount,
        AceMainHeader? aceHeader
    )
    {
        var inspection = new ReaderInspection(detection);
        while (reader.MoveToNextEntry())
        {
            inspection.Add(reader.Entry);
        }

        var isSolid = aceHeader?.IsSolid ?? false;
        var isMultiVolume = aceHeader?.IsMultiVolume ?? false;
        var formatVersion = aceHeader is null ? null : $"ACE {aceHeader.AceVersion / 10.0:0.0}";
        return inspection.CreateInformation(
            physicalSize,
            physicalPartCount,
            isSolid,
            isSolid && inspection.EntryCount > 1 ? 1 : 0,
            isMultiVolume,
            formatVersion
        );
    }

    private static AceMainHeader? ReadAceHeader(
        Stream stream,
        ArchiveDetection detection,
        ReaderOptions options
    )
    {
        if (detection.ContainerType != ArchiveType.Ace)
        {
            return null;
        }

        try
        {
            stream.Position = 0;
            return new AceMainHeader(options.ArchiveEncoding).Read(stream) as AceMainHeader;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static ArchiveInformation CreatePartialInformation(
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount,
        ArchiveInformationLimitations limitations
    ) =>
        new(
            detection,
            ArchiveInformationStatus.Partial,
            limitations,
            null,
            null,
            null,
            physicalSize,
            null,
            null,
            null,
            null,
            limitations.HasFlag(ArchiveInformationLimitations.EncryptedHeaders)
                ? ArchiveEncryptionScope.Headers
                : null,
            limitations.HasFlag(ArchiveInformationLimitations.EncryptedHeaders) ? true : null,
            null,
            physicalPartCount,
            null,
            null,
            null,
            null
        );

    private static ArchiveInformationStatus GetStatus(ArchiveInformationLimitations limitations) =>
        limitations == ArchiveInformationLimitations.None
            ? ArchiveInformationStatus.Complete
            : ArchiveInformationStatus.Partial;

    private static ZipArchiveInformation? GetZipInformation(IEnumerable<IArchiveEntry> entries)
    {
        var dataDescriptorEntryCount = entries
            .OfType<ZipArchiveEntry>()
            .LongCount(entry =>
                entry
                    .Parts.OfType<ZipFilePart>()
                    .Any(part =>
                        FlagUtility.HasFlag(
                            part.Header.Flags,
                            SharpCompress.Common.Zip.Headers.HeaderFlags.UsePostDataDescriptor
                        )
                    )
            );
        return dataDescriptorEntryCount == 0 && !entries.OfType<ZipArchiveEntry>().Any()
            ? null
            : new ZipArchiveInformation(dataDescriptorEntryCount);
    }

    private static (bool IsSolid, long SolidStreamCount) GetSolidInformation(
        IArchive archive,
        IReadOnlyCollection<IArchiveEntry> entries
    )
    {
        if (archive.Type == ArchiveType.SevenZip)
        {
            var solidStreamCount = entries
                .OfType<SevenZipArchiveEntry>()
                .Where(entry => !entry.IsDirectory && entry.FilePart.Folder is not null)
                .GroupBy(entry => entry.FilePart.Folder)
                .LongCount(group => group.Skip(1).Any());
            return (solidStreamCount > 0, solidStreamCount);
        }

        if (archive.Type == ArchiveType.Rar)
        {
            var solidStreamCount = CountRarSolidStreams(entries);
            return (archive.IsSolid, solidStreamCount);
        }

        return (false, 0);
    }

    private static long CountRarSolidStreams(IEnumerable<IArchiveEntry> entries)
    {
        var wasSolid = false;
        long count = 0;
        foreach (var entry in entries.Where(entry => !entry.IsDirectory))
        {
            if (entry.IsSolid && !wasSolid)
            {
                count++;
            }
            wasSolid = entry.IsSolid;
        }
        return count;
    }

    private static bool GetIsMultiVolume(ArchiveType type, IVolume[] volumes) =>
        type == ArchiveType.Rar
            ? volumes.OfType<RarVolume>().Any(volume => volume.IsMultiVolume)
            : volumes.Length > 1;

    private static string? GetArchiveComment(IEnumerable<IVolume> volumes) =>
        volumes.OfType<ZipVolume>().LastOrDefault()?.Comment
        ?? volumes
            .OfType<RarVolume>()
            .Select(volume => volume.Comment)
            .FirstOrDefault(comment => comment is not null);

    private static string? GetFormatVersion(IEnumerable<IVolume> volumes)
    {
        var rarVolume = volumes.OfType<RarVolume>().FirstOrDefault();
        return rarVolume is null ? null : $"RAR {rarVolume.MinVersion}-{rarVolume.MaxVersion}";
    }

    private static ArchiveEncryptionScope GetEncryptionScope(
        bool hasEncryptedHeaders,
        bool hasEncryptedEntries
    )
    {
        var encryption = ArchiveEncryptionScope.None;
        if (hasEncryptedHeaders)
        {
            encryption |= ArchiveEncryptionScope.Headers;
        }
        if (hasEncryptedEntries)
        {
            encryption |= ArchiveEncryptionScope.EntryData;
        }
        return encryption;
    }

    private static long? GetCompressedPayloadSize(
        IArchive archive,
        IReadOnlyCollection<IArchiveEntry> entries
    ) =>
        archive.Type switch
        {
            ArchiveType.GZip => null,
            ArchiveType.SevenZip when archive is SevenZipArchive sevenZipArchive =>
                sevenZipArchive.TotalSize,
            _ => entries.Aggregate(0L, (total, entry) => total + entry.CompressedSize),
        };

    private static long? GetPhysicalSize(Stream stream, long startPosition)
    {
        try
        {
            return stream.Length - startPosition;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static ArchiveDetection? RedetectArchive(
        Stream stream,
        long startPosition,
        ReaderOptions options
    )
    {
        stream.Seek(startPosition, SeekOrigin.Begin);
        using var archiveStream = new ArchiveOffsetStream(stream);
        return TryDetectArchive(archiveStream, options);
    }

    private static FileInfo[] GetArchiveFileParts(FileInfo firstPart, ReaderOptions options)
    {
        using Stream stream = firstPart.OpenRead();
        var factory = TryFindFactory(stream, options);
        if (factory is null)
        {
            return [firstPart];
        }

        var parts = new List<FileInfo> { firstPart };
        for (var index = 1; factory.GetFilePart(index, firstPart) is { } part; index++)
        {
            parts.Add(part);
        }
        return parts.ToArray();
    }

    private static long? GetPhysicalSize(IEnumerable<FileInfo> fileInfos)
    {
        try
        {
            return fileInfos.Aggregate(0L, (total, fileInfo) => total + fileInfo.Length);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static long? GetPhysicalSize(IReadOnlyList<Stream> streams, long[] startPositions)
    {
        try
        {
            long total = 0;
            for (var i = 0; i < streams.Count; i++)
            {
                total += streams[i].Length - startPositions[i];
            }
            return total;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private sealed class ReaderInspection
    {
        private readonly ArchiveDetection detection;
        private long? compressedPayloadSize = 0;
        private long? uncompressedPayloadSize = 0;
        private long dataDescriptorEntryCount;
        private long entriesWithUnknownSizeCount;
        private bool isEncrypted;
        private ArchiveInformationLimitations limitations;

        public ReaderInspection(ArchiveDetection detection)
        {
            this.detection = detection;
            if (
                detection.ContainerType == ArchiveType.Lzw
                || (
                    detection.ContainerType == ArchiveType.Tar
                    && detection.OuterCompressionType is not null
                )
            )
            {
                compressedPayloadSize = null;
                limitations |= ArchiveInformationLimitations.UnavailableMetadata;
            }
            if (detection.ContainerType == ArchiveType.Lzw)
            {
                uncompressedPayloadSize = null;
            }
        }

        public long EntryCount { get; private set; }

        public void Add(IEntry entry)
        {
            EntryCount++;
            isEncrypted |= entry.IsEncrypted;

            var usesDataDescriptor = UsesZipDataDescriptor(entry);
            if (usesDataDescriptor)
            {
                dataDescriptorEntryCount++;
            }

            if (
                detection.ContainerType == ArchiveType.Lzw
                || usesDataDescriptor
                || !TryGetSize(entry, out var size)
            )
            {
                entriesWithUnknownSizeCount++;
                uncompressedPayloadSize = null;
                limitations |= ArchiveInformationLimitations.UnavailableMetadata;
            }
            else if (uncompressedPayloadSize is { } totalSize)
            {
                uncompressedPayloadSize = totalSize + size;
            }

            if (compressedPayloadSize is { } totalCompressedSize)
            {
                compressedPayloadSize = totalCompressedSize + entry.CompressedSize;
            }
        }

        public ArchiveInformation CreateInformation(
            long? physicalSize,
            int physicalPartCount,
            bool isSolid,
            long solidStreamCount,
            bool isMultiVolume,
            string? formatVersion
        ) =>
            new(
                detection,
                GetStatus(limitations),
                limitations,
                formatVersion,
                EntryCount,
                entriesWithUnknownSizeCount,
                physicalSize,
                compressedPayloadSize,
                uncompressedPayloadSize,
                isSolid,
                solidStreamCount,
                isEncrypted ? ArchiveEncryptionScope.EntryData : ArchiveEncryptionScope.None,
                isEncrypted,
                isMultiVolume,
                physicalPartCount,
                1,
                true,
                null,
                detection.ContainerType == ArchiveType.Zip
                    ? new ZipArchiveInformation(dataDescriptorEntryCount)
                    : null
            );

        private static bool TryGetSize(IEntry entry, out long size)
        {
            try
            {
                size = entry.Size;
                return size >= 0;
            }
            catch (NotImplementedException)
            {
                size = 0;
                return false;
            }
        }

        private static bool UsesZipDataDescriptor(IEntry entry) =>
            entry is ZipEntry zipEntry
            && zipEntry
                .Parts.OfType<ZipFilePart>()
                .Any(part =>
                    FlagUtility.HasFlag(
                        part.Header.Flags,
                        SharpCompress.Common.Zip.Headers.HeaderFlags.UsePostDataDescriptor
                    )
                );
    }
}
