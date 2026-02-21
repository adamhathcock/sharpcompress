using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Cli.Inspection;

public sealed class ArchiveInspector
{
    public InspectionExecutionResult InspectArchives(
        IEnumerable<string> archivePaths,
        InspectionRequest request
    )
    {
        ArgumentNullException.ThrowIfNull(archivePaths);
        ArgumentNullException.ThrowIfNull(request);

        var archives = new List<ArchiveInspectionResult>();
        var errors = new List<InspectionError>();

        foreach (var archivePath in archivePaths)
        {
            try
            {
                archives.Add(InspectArchive(archivePath, request));
            }
            catch (Exception exception)
            {
                errors.Add(new InspectionError(archivePath, exception.Message));
            }
        }

        return new InspectionExecutionResult(archives, errors);
    }

    private static ArchiveInspectionResult InspectArchive(
        string archivePath,
        InspectionRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("Archive path is required.", nameof(archivePath));
        }

        // Handle tilde expansion for home directory, then convert relative paths to absolute paths
        var expandedPath = archivePath.StartsWith('~')
            ? archivePath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.Ordinal)
            : archivePath;
        var fullPath = Path.GetFullPath(expandedPath);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"Archive file was not found: {fileInfo.FullName}");
        }

        var readerOptions = CreateReaderOptions(request);
        var archiveParts = ArchiveFactory.GetFileParts(fileInfo).ToList();

        if (request.AccessMode == AccessMode.Seekable)
        {
            return InspectWithSeekable(
                fileInfo,
                archiveParts,
                readerOptions,
                request,
                autoFallbackApplied: false,
                fallbackReason: null,
                forcedSeekable: true
            );
        }

        if (archiveParts.Count > 1)
        {
            return InspectWithSeekable(
                fileInfo,
                archiveParts,
                readerOptions,
                request,
                autoFallbackApplied: true,
                fallbackReason: "Detected multi-volume archive parts.",
                forcedSeekable: false
            );
        }

        try
        {
            return InspectWithForward(fileInfo, readerOptions, request);
        }
        catch (Exception exception) when (ShouldFallbackToSeekable(exception))
        {
            return InspectWithSeekable(
                fileInfo,
                archiveParts,
                readerOptions,
                request,
                autoFallbackApplied: true,
                fallbackReason: $"Forward reader failed and seekable mode was used ({exception.GetType().Name}).",
                forcedSeekable: false
            );
        }
    }

    private static ArchiveInspectionResult InspectWithForward(
        FileInfo fileInfo,
        ReaderOptions readerOptions,
        InspectionRequest request
    )
    {
        using var reader = ReaderFactory.OpenReader(fileInfo, readerOptions);
        var entries = new List<ArchiveEntryResult>();

        var entryCount = 0;
        var displayedEntryCount = 0;
        var outputTruncated = false;
        var totalCompressedSize = 0L;
        var totalUncompressedSize = 0L;
        var isSolid = false;
        var isEncrypted = false;

        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            entryCount++;
            totalCompressedSize += entry.CompressedSize;
            totalUncompressedSize += entry.Size;
            isSolid |= entry.IsSolid;
            isEncrypted |= entry.IsEncrypted;

            if (!ShouldIncludeEntry(entry, request))
            {
                continue;
            }

            if (request.Limit.HasValue && displayedEntryCount >= request.Limit.Value)
            {
                outputTruncated = true;
                continue;
            }

            entries.Add(MapEntry(entry));
            displayedEntryCount++;
        }

        return new ArchiveInspectionResult(
            ArchivePath: fileInfo.FullName,
            ArchiveType: reader.ArchiveType.ToString(),
            RequestedAccessMode: request.AccessMode,
            UsedAccessMode: AccessMode.Forward,
            AutoFallbackApplied: false,
            FallbackReason: null,
            IsComplete: null,
            IsSolid: isSolid,
            IsEncrypted: isEncrypted,
            VolumeCount: 1,
            EntryCount: entryCount,
            DisplayedEntryCount: displayedEntryCount,
            OutputTruncated: outputTruncated,
            TotalCompressedSize: totalCompressedSize,
            TotalUncompressedSize: totalUncompressedSize,
            Entries: entries
        );
    }

    private static ArchiveInspectionResult InspectWithSeekable(
        FileInfo fileInfo,
        List<FileInfo> archiveParts,
        ReaderOptions readerOptions,
        InspectionRequest request,
        bool autoFallbackApplied,
        string? fallbackReason,
        bool forcedSeekable
    )
    {
        try
        {
            using var archive =
                archiveParts.Count == 1
                    ? ArchiveFactory.OpenArchive(fileInfo, readerOptions)
                    : ArchiveFactory.OpenArchive(archiveParts, readerOptions);

            var entries = new List<ArchiveEntryResult>();
            var entryCount = 0;
            var displayedEntryCount = 0;
            var outputTruncated = false;
            var totalCompressedSize = 0L;
            var totalUncompressedSize = 0L;

            foreach (var entry in archive.Entries)
            {
                entryCount++;
                totalCompressedSize += entry.CompressedSize;
                totalUncompressedSize += entry.Size;

                if (!ShouldIncludeEntry(entry, request))
                {
                    continue;
                }

                if (request.Limit.HasValue && displayedEntryCount >= request.Limit.Value)
                {
                    outputTruncated = true;
                    continue;
                }

                entries.Add(MapEntry(entry));
                displayedEntryCount++;
            }

            return new ArchiveInspectionResult(
                ArchivePath: fileInfo.FullName,
                ArchiveType: archive.Type.ToString(),
                RequestedAccessMode: request.AccessMode,
                UsedAccessMode: AccessMode.Seekable,
                AutoFallbackApplied: autoFallbackApplied,
                FallbackReason: fallbackReason,
                IsComplete: archive.IsComplete,
                IsSolid: archive.IsSolid,
                IsEncrypted: archive.IsEncrypted,
                VolumeCount: archiveParts.Count,
                EntryCount: entryCount,
                DisplayedEntryCount: displayedEntryCount,
                OutputTruncated: outputTruncated,
                TotalCompressedSize: totalCompressedSize,
                TotalUncompressedSize: totalUncompressedSize,
                Entries: entries
            );
        }
        catch (Exception exception)
            when (forcedSeekable && SupportsForwardOnly(fileInfo, readerOptions))
        {
            throw new InvalidOperationException(
                "Seekable mode is not available for this archive. Re-run the command with --access forward.",
                exception
            );
        }
    }

    private static ReaderOptions CreateReaderOptions(InspectionRequest request)
    {
        var extensionHint = string.IsNullOrWhiteSpace(request.ExtensionHint)
            ? null
            : request.ExtensionHint;

        return ReaderOptions.ForOwnedFile with
        {
            Password = request.Password,
            LookForHeader = request.LookForHeader,
            ExtensionHint = extensionHint,
            RewindableBufferSize = request.RewindableBufferSize,
        };
    }

    private static bool ShouldFallbackToSeekable(Exception exception) =>
        exception is InvalidFormatException or ArchiveOperationException;

    private static bool SupportsForwardOnly(FileInfo fileInfo, ReaderOptions readerOptions)
    {
        try
        {
            using var reader = ReaderFactory.OpenReader(fileInfo, readerOptions);
            return reader is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldIncludeEntry(IEntry entry, InspectionRequest request) =>
        request.IncludeDirectories || !entry.IsDirectory;

    private static ArchiveEntryResult MapEntry(IEntry entry) =>
        new(
            Key: entry.Key ?? string.Empty,
            CompressionType: entry.CompressionType,
            CompressedSize: entry.CompressedSize,
            Size: entry.Size,
            IsDirectory: entry.IsDirectory,
            IsEncrypted: entry.IsEncrypted,
            IsSplitAfter: entry.IsSplitAfter,
            IsSolid: entry.IsSolid,
            VolumeIndexFirst: entry.VolumeIndexFirst,
            VolumeIndexLast: entry.VolumeIndexLast,
            LastModifiedTime: entry.LastModifiedTime,
            CreatedTime: entry.CreatedTime,
            LastAccessedTime: entry.LastAccessedTime,
            ArchivedTime: entry.ArchivedTime,
            LinkTarget: entry.LinkTarget
        );
}
