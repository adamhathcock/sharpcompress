using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Readers;
using AceMainHeader = SharpCompress.Common.Ace.Headers.AceMainHeader;

namespace SharpCompress.Archives;

internal sealed partial class ArchiveService
{
    private static ArchiveInformation InspectOpenedArchive(
        IArchive archive,
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount
    )
    {
        var entries = archive.Entries.ToArray();
        var volumes = archive.Volumes.ToArray();
        var (zipInformation, deferredSizeEntryCount) = GetZipInformation(
            archive.Type == ArchiveType.Zip,
            entries
        );
        var (isSolid, solidStreamCount) = GetSolidInformation(archive, entries);
        var isComplete = archive.IsComplete;
        return CreateArchiveInformation(
            archive.Type,
            entries,
            volumes,
            detection,
            physicalSize,
            physicalPartCount,
            zipInformation,
            deferredSizeEntryCount,
            isSolid,
            solidStreamCount,
            isComplete,
            isComplete
                ? GetCompressedPayloadSize(archive.Type, archive as SevenZipArchive, entries)
                : null
        );
    }

    private static ArchiveInformation CreateArchiveInformation(
        ArchiveType type,
        IArchiveEntry[] entries,
        IVolume[] volumes,
        ArchiveDetection detection,
        long? physicalSize,
        int physicalPartCount,
        ZipArchiveInformation? zipInformation,
        long deferredSizeEntryCount,
        bool isSolid,
        long solidStreamCount,
        bool isComplete,
        long? compressedPayloadSize
    )
    {
        var hasEncryptedHeaders = volumes
            .OfType<RarVolume>()
            .Any(volume => volume.IsHeaderEncrypted);
        var hasEncryptedEntries = entries.Any(entry => entry.IsEncrypted);
        var limitations = isComplete
            ? ArchiveInformationLimitations.None
            : ArchiveInformationLimitations.MissingVolumes;
        if (type == ArchiveType.GZip)
        {
            limitations |= ArchiveInformationLimitations.UnavailableMetadata;
        }

        return new ArchiveInformation(
            detection,
            GetStatus(limitations),
            limitations,
            GetFormatVersion(volumes),
            entries.LongLength,
            deferredSizeEntryCount,
            physicalSize,
            isComplete ? compressedPayloadSize : null,
            isComplete && type != ArchiveType.GZip
                ? entries.Aggregate(0L, (total, entry) => total + entry.Size)
                : null,
            isSolid,
            solidStreamCount,
            GetEncryptionScope(hasEncryptedHeaders, hasEncryptedEntries),
            hasEncryptedHeaders || hasEncryptedEntries,
            GetIsMultiVolume(type, volumes),
            physicalPartCount,
            volumes.Length,
            isComplete,
            GetArchiveComment(volumes),
            zipInformation
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

        return CreateReaderInformation(inspection, physicalSize, physicalPartCount, aceHeader);
    }

    private static ArchiveInformation CreateReaderInformation(
        ReaderInspection inspection,
        long? physicalSize,
        int physicalPartCount,
        AceMainHeader? aceHeader
    )
    {
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

    private static (bool IsSolid, long SolidStreamCount) GetSolidInformation(
        IArchive archive,
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
            var solidStreamCount = CountRarSolidStreams(entries);
            return (archive.IsSolid, solidStreamCount);
        }

        return (false, 0);
    }

    private static long CountSevenZipSolidStreams(IEnumerable<IArchiveEntry> entries) =>
        entries
            .OfType<SevenZipArchiveEntry>()
            .Where(entry => !entry.IsDirectory && entry.FilePart.Folder is not null)
            .GroupBy(entry => entry.FilePart.Folder)
            .LongCount(group => group.Skip(1).Any());

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
        ArchiveType type,
        SevenZipArchive? sevenZipArchive,
        IReadOnlyCollection<IArchiveEntry> entries
    ) =>
        type switch
        {
            ArchiveType.GZip => null,
            ArchiveType.SevenZip when sevenZipArchive is not null => sevenZipArchive.TotalSize,
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

    private static FileInfo[] GetArchiveFileParts(FileInfo firstPart, IFactory factory)
    {
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
        private long deferredSizeEntryCount;
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

            var hasDeferredSizes = HasDeferredSizes(entry);
            if (hasDeferredSizes)
            {
                deferredSizeEntryCount++;
            }

            if (
                detection.ContainerType == ArchiveType.Lzw
                || hasDeferredSizes
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
                    ? new ZipArchiveInformation(deferredSizeEntryCount > 0)
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

        private static bool HasDeferredSizes(IEntry entry) =>
            entry is ZipEntry zipEntry
            && zipEntry
                .Parts.OfType<ZipFilePart>()
                .Any(part =>
                    FlagUtility.HasFlag(
                        part.Header.Flags,
                        SharpCompress.Common.Zip.Headers.HeaderFlags.UsePostDataDescriptor
                    )
                    && part.Header.CompressedSize == 0
                    && part.Header.UncompressedSize == 0
                );
    }
}
