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
            if (TryInspectArchive(archivePath, request, out var archive, out var error))
            {
                archives.Add(archive!);
                continue;
            }

            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            errors.Add(
                new InspectionError(
                    archivePath,
                    InspectionErrorCode.Unexpected,
                    "Unexpected inspection failure."
                )
            );
        }

        return new InspectionExecutionResult(archives, errors);
    }

    private static bool TryInspectArchive(
        string archivePath,
        InspectionRequest request,
        out ArchiveInspectionResult? archiveResult,
        out InspectionError? error
    )
    {
        archiveResult = null;
        error = null;

        try
        {
            if (!TryResolveFileInfo(archivePath, out var fileInfo, out error))
            {
                return false;
            }

            if (!TryDetectArchiveType(fileInfo, out var detectedArchiveType, out error))
            {
                return false;
            }

            var capabilities = ArchiveTypeCapabilities.Get(detectedArchiveType);
            var readerOptions = CreateReaderOptions(request);
            var archiveParts = ArchiveFactory.GetFileParts(fileInfo).ToList();

            if (
                !TryCreateExecutionPlan(
                    fileInfo,
                    request,
                    capabilities,
                    archiveParts,
                    out var plan,
                    out error
                )
            )
            {
                return false;
            }

            if (plan.UsedAccessMode == AccessMode.Seekable)
            {
                return TryInspectWithSeekable(
                    fileInfo,
                    archiveParts,
                    readerOptions,
                    request,
                    detectedArchiveType,
                    capabilities,
                    plan,
                    out archiveResult,
                    out error
                );
            }

            if (
                TryInspectWithForward(
                    fileInfo,
                    readerOptions,
                    request,
                    detectedArchiveType,
                    plan,
                    out archiveResult,
                    out var forwardFailure
                )
            )
            {
                return true;
            }

            if (
                !plan.AllowSeekableFallback
                || forwardFailure is null
                || !ShouldFallbackToSeekable(forwardFailure)
            )
            {
                error = new InspectionError(
                    fileInfo.FullName,
                    InspectionErrorCode.InspectionFailed,
                    forwardFailure?.Message ?? "Failed to inspect archive with forward mode."
                );
                return false;
            }

            var fallbackPlan = plan with
            {
                UsedAccessMode = AccessMode.Seekable,
                StreamingType = StreamingType.AutoFallbackSeekable,
                AutoFallbackApplied = true,
                FallbackReason =
                    $"Forward reader failed and seekable mode was used ({forwardFailure.GetType().Name}).",
                AllowSeekableFallback = false,
            };

            return TryInspectWithSeekable(
                fileInfo,
                archiveParts,
                readerOptions,
                request,
                detectedArchiveType,
                capabilities,
                fallbackPlan,
                out archiveResult,
                out error
            );
        }
        catch (Exception exception)
        {
            error = new InspectionError(
                archivePath,
                InspectionErrorCode.Unexpected,
                exception.Message
            );
            return false;
        }
    }

    private static bool TryResolveFileInfo(
        string archivePath,
        out FileInfo fileInfo,
        out InspectionError? error
    )
    {
        fileInfo = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(archivePath))
        {
            error = new InspectionError(
                archivePath,
                InspectionErrorCode.InvalidPath,
                "Archive path is required."
            );
            return false;
        }

        try
        {
            var expandedPath = archivePath.StartsWith('~')
                ? archivePath.Replace(
                    "~",
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    StringComparison.Ordinal
                )
                : archivePath;

            var fullPath = Path.GetFullPath(expandedPath);
            fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                error = new InspectionError(
                    fullPath,
                    InspectionErrorCode.FileNotFound,
                    $"Archive file was not found: {fullPath}"
                );
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = new InspectionError(
                archivePath,
                InspectionErrorCode.InvalidPath,
                exception.Message
            );
            return false;
        }
    }

    private static bool TryDetectArchiveType(
        FileInfo fileInfo,
        out ArchiveType detectedArchiveType,
        out InspectionError? error
    )
    {
        detectedArchiveType = default;
        error = null;

        try
        {
            if (!ArchiveFactory.IsArchive(fileInfo.FullName, out var type) || type is null)
            {
                error = new InspectionError(
                    fileInfo.FullName,
                    InspectionErrorCode.NotArchive,
                    "The provided file is not a supported archive type."
                );
                return false;
            }

            detectedArchiveType = type.Value;
            return true;
        }
        catch (Exception exception)
        {
            error = new InspectionError(
                fileInfo.FullName,
                InspectionErrorCode.InspectionFailed,
                exception.Message
            );
            return false;
        }
    }

    private static bool TryCreateExecutionPlan(
        FileInfo fileInfo,
        InspectionRequest request,
        ArchiveTypeCapabilities.Capability capabilities,
        List<FileInfo> archiveParts,
        out InspectionExecutionPlan plan,
        out InspectionError? error
    )
    {
        error = null;

        var isMultiVolume = archiveParts.Count > 1;

        if (request.AccessMode == AccessMode.Seekable)
        {
            if (!capabilities.SupportsSeekable)
            {
                plan = default!;
                error = new InspectionError(
                    fileInfo.FullName,
                    InspectionErrorCode.AccessModeNotSupported,
                    "Seekable mode is not available for this archive. Re-run the command with --access forward."
                );
                return false;
            }

            plan = new InspectionExecutionPlan(
                RequestedAccessMode: request.AccessMode,
                UsedAccessMode: AccessMode.Seekable,
                StreamingType: isMultiVolume
                    ? StreamingType.SeekableMultiVolume
                    : StreamingType.Seekable,
                AutoFallbackApplied: false,
                FallbackReason: null,
                AllowSeekableFallback: false
            );
            return true;
        }

        if (isMultiVolume)
        {
            if (!capabilities.SupportsSeekable)
            {
                plan = default!;
                error = new InspectionError(
                    fileInfo.FullName,
                    InspectionErrorCode.AccessModeNotSupported,
                    "Multi-volume archive inspection requires seekable access, which this archive type does not support."
                );
                return false;
            }

            plan = new InspectionExecutionPlan(
                RequestedAccessMode: request.AccessMode,
                UsedAccessMode: AccessMode.Seekable,
                StreamingType: StreamingType.SeekableMultiVolume,
                AutoFallbackApplied: true,
                FallbackReason: "Detected multi-volume archive parts.",
                AllowSeekableFallback: false
            );
            return true;
        }

        if (!capabilities.SupportsForward)
        {
            if (!capabilities.SupportsSeekable)
            {
                plan = default!;
                error = new InspectionError(
                    fileInfo.FullName,
                    InspectionErrorCode.AccessModeNotSupported,
                    "The detected archive type does not support forward or seekable inspection."
                );
                return false;
            }

            plan = new InspectionExecutionPlan(
                RequestedAccessMode: request.AccessMode,
                UsedAccessMode: AccessMode.Seekable,
                StreamingType: StreamingType.AutoFallbackSeekable,
                AutoFallbackApplied: true,
                FallbackReason: "Detected archive type requires seekable access.",
                AllowSeekableFallback: false
            );
            return true;
        }

        plan = new InspectionExecutionPlan(
            RequestedAccessMode: request.AccessMode,
            UsedAccessMode: AccessMode.Forward,
            StreamingType: StreamingType.Forward,
            AutoFallbackApplied: false,
            FallbackReason: null,
            AllowSeekableFallback: capabilities.SupportsSeekable
        );
        return true;
    }

    private static bool TryInspectWithForward(
        FileInfo fileInfo,
        ReaderOptions readerOptions,
        InspectionRequest request,
        ArchiveType detectedArchiveType,
        InspectionExecutionPlan plan,
        out ArchiveInspectionResult? archiveResult,
        out Exception? failure
    )
    {
        archiveResult = null;
        failure = null;
        try
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

            archiveResult = new ArchiveInspectionResult(
                ArchivePath: fileInfo.FullName,
                DetectedArchiveType: detectedArchiveType,
                ArchiveType: reader.ArchiveType.ToString(),
                StreamingType: plan.StreamingType,
                RequestedAccessMode: plan.RequestedAccessMode,
                UsedAccessMode: AccessMode.Forward,
                AutoFallbackApplied: plan.AutoFallbackApplied,
                FallbackReason: plan.FallbackReason,
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

            return true;
        }
        catch (Exception exception)
        {
            failure = exception;
            return false;
        }
    }

    private static bool TryInspectWithSeekable(
        FileInfo fileInfo,
        List<FileInfo> archiveParts,
        ReaderOptions readerOptions,
        InspectionRequest request,
        ArchiveType detectedArchiveType,
        ArchiveTypeCapabilities.Capability capabilities,
        InspectionExecutionPlan plan,
        out ArchiveInspectionResult? archiveResult,
        out InspectionError? error
    )
    {
        archiveResult = null;
        error = null;

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

            archiveResult = new ArchiveInspectionResult(
                ArchivePath: fileInfo.FullName,
                DetectedArchiveType: detectedArchiveType,
                ArchiveType: archive.Type.ToString(),
                StreamingType: plan.StreamingType,
                RequestedAccessMode: plan.RequestedAccessMode,
                UsedAccessMode: AccessMode.Seekable,
                AutoFallbackApplied: plan.AutoFallbackApplied,
                FallbackReason: plan.FallbackReason,
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

            return true;
        }
        catch (Exception exception)
        {
            if (plan.RequestedAccessMode == AccessMode.Seekable && capabilities.SupportsForward)
            {
                error = new InspectionError(
                    fileInfo.FullName,
                    InspectionErrorCode.AccessModeNotSupported,
                    "Seekable mode is not available for this archive. Re-run the command with --access forward."
                );
                return false;
            }

            error = new InspectionError(
                fileInfo.FullName,
                InspectionErrorCode.InspectionFailed,
                exception.Message
            );
            return false;
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

    private sealed record InspectionExecutionPlan(
        AccessMode RequestedAccessMode,
        AccessMode UsedAccessMode,
        StreamingType StreamingType,
        bool AutoFallbackApplied,
        string? FallbackReason,
        bool AllowSeekableFallback
    );
}
