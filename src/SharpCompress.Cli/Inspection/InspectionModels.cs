using SharpCompress.Common;

namespace SharpCompress.Cli.Inspection;

public sealed record InspectionExecutionResult(
    List<ArchiveInspectionResult> Archives,
    List<InspectionError> Errors
);

public sealed record ArchiveInspectionResult(
    string ArchivePath,
    string ArchiveType,
    AccessMode RequestedAccessMode,
    AccessMode UsedAccessMode,
    bool AutoFallbackApplied,
    string? FallbackReason,
    bool? IsComplete,
    bool IsSolid,
    bool IsEncrypted,
    int VolumeCount,
    int EntryCount,
    int DisplayedEntryCount,
    bool OutputTruncated,
    long TotalCompressedSize,
    long TotalUncompressedSize,
    List<ArchiveEntryResult> Entries
);

public sealed record ArchiveEntryResult(
    string Key,
    CompressionType CompressionType,
    long CompressedSize,
    long Size,
    bool IsDirectory,
    bool IsEncrypted,
    bool IsSplitAfter,
    bool IsSolid,
    int VolumeIndexFirst,
    int VolumeIndexLast,
    DateTime? LastModifiedTime,
    DateTime? CreatedTime,
    DateTime? LastAccessedTime,
    DateTime? ArchivedTime,
    string? LinkTarget
);

public sealed record InspectionError(string ArchivePath, string Message);
