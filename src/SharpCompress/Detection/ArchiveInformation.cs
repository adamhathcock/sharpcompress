namespace SharpCompress.Detection;

/// <summary>
/// Contains metadata collected by fully inspecting an archive.
/// </summary>
public sealed class ArchiveInformation
{
    internal ArchiveInformation(
        ArchiveDetection detection,
        ArchiveInformationStatus status,
        ArchiveInformationLimitations limitations,
        string? formatVersion,
        long? entryCount,
        long? entriesWithUnknownSizeCount,
        long? physicalSize,
        long? compressedPayloadSize,
        long? uncompressedPayloadSize,
        bool? isSolid,
        long? solidStreamCount,
        ArchiveEncryptionScope? encryption,
        bool? isEncrypted,
        bool? isMultiVolume,
        int? physicalPartCount,
        int? logicalVolumeCount,
        bool? isComplete,
        string? comment,
        ZipArchiveInformation? zip
    )
    {
        Detection = detection;
        Status = status;
        Limitations = limitations;
        FormatVersion = formatVersion;
        EntryCount = entryCount;
        EntriesWithUnknownSizeCount = entriesWithUnknownSizeCount;
        PhysicalSize = physicalSize;
        CompressedPayloadSize = compressedPayloadSize;
        UncompressedPayloadSize = uncompressedPayloadSize;
        IsSolid = isSolid;
        SolidStreamCount = solidStreamCount;
        Encryption = encryption;
        IsEncrypted = isEncrypted;
        IsMultiVolume = isMultiVolume;
        PhysicalPartCount = physicalPartCount;
        LogicalVolumeCount = logicalVolumeCount;
        IsComplete = isComplete;
        Comment = comment;
        Zip = zip;
    }

    /// <summary>
    /// Gets the format and API capabilities identified before inspection.
    /// </summary>
    public ArchiveDetection Detection { get; }

    /// <summary>
    /// Gets whether metadata collection completed.
    /// </summary>
    public ArchiveInformationStatus Status { get; }

    /// <summary>
    /// Gets conditions that prevented complete metadata collection.
    /// </summary>
    public ArchiveInformationLimitations Limitations { get; }

    /// <summary>
    /// Gets the format version when exposed by the archive format.
    /// </summary>
    public string? FormatVersion { get; }

    /// <summary>
    /// Gets the number of entries, excluding format control records.
    /// </summary>
    public long? EntryCount { get; }

    /// <summary>
    /// Gets the number of entries for which a forward-only reader cannot know the uncompressed size before reading entry data.
    /// </summary>
    public long? EntriesWithUnknownSizeCount { get; }

    /// <summary>
    /// Gets the number of bytes in the supplied source parts, when known.
    /// </summary>
    public long? PhysicalSize { get; }

    /// <summary>
    /// Gets the aggregate stored payload size, when the format exposes it reliably.
    /// </summary>
    public long? CompressedPayloadSize { get; }

    /// <summary>
    /// Gets the aggregate uncompressed entry size, when the format exposes it reliably.
    /// </summary>
    public long? UncompressedPayloadSize { get; }

    /// <summary>
    /// Gets whether entries share compression state.
    /// </summary>
    public bool? IsSolid { get; }

    /// <summary>
    /// Gets the number of independent shared compression streams, when known.
    /// </summary>
    public long? SolidStreamCount { get; }

    /// <summary>
    /// Gets the scopes protected by encryption, when known.
    /// </summary>
    public ArchiveEncryptionScope? Encryption { get; }

    /// <summary>
    /// Gets whether archive headers or entry data are encrypted, when known.
    /// </summary>
    public bool? IsEncrypted { get; }

    /// <summary>
    /// Gets whether the archive is part of a multi-volume set, when known.
    /// </summary>
    public bool? IsMultiVolume { get; }

    /// <summary>
    /// Gets the number of source parts supplied for inspection.
    /// </summary>
    public int? PhysicalPartCount { get; }

    /// <summary>
    /// Gets the number of logical volumes exposed by the archive, when known.
    /// </summary>
    public int? LogicalVolumeCount { get; }

    /// <summary>
    /// Gets whether all discovered entries are complete, when known.
    /// </summary>
    public bool? IsComplete { get; }

    /// <summary>
    /// Gets the archive comment, when supported by the format.
    /// </summary>
    public string? Comment { get; }

    /// <summary>
    /// Gets ZIP-specific metadata when the detected container is ZIP.
    /// </summary>
    public ZipArchiveInformation? Zip { get; }
}
