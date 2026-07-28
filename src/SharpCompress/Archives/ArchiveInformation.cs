using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>
/// Contains information about a detected archive, including its type and supported capabilities.
/// </summary>
/// <remarks>
/// Use <see cref="ArchiveFactory.GetArchiveInformation(System.IO.Stream)"/> or
/// <see cref="ArchiveFactory.GetArchiveInformationAsync(System.IO.Stream,System.Threading.CancellationToken)"/>
/// to obtain an instance of this record.
/// </remarks>
public record ArchiveInformation
{
    /// <summary>
    /// The type of archive detected, or <see langword="null"/> when the format is not a registered well-known type.
    /// </summary>
    public ArchiveType? Type { get; set; }

    /// <summary>
    /// <see langword="true"/> when this archive format supports random access via the <see cref="IArchive"/> API,
    /// meaning the full file listing can be retrieved without decompressing the entire archive.
    /// <see langword="false"/> when only the <see cref="SharpCompress.Readers.IReader"/> API is available,
    /// which reads entries sequentially and can only report per-entry progress.
    /// </summary>
    public bool SupportsRandomAccess { get; set; }

    /// <summary>
    /// For ZIP archives, the number of entries that use post-data descriptor trailers.
    /// This value is <see langword="null"/> for non-ZIP formats.
    /// </summary>
    public int? ZipDataDescriptorEntryCount { get; set; }

    /// <summary>
    /// For solid-capable archive formats, the number of solid compressed streams present.
    /// This value is <see langword="null"/> for formats that do not support solid entries.
    /// </summary>
    public int? SolidStreamCount { get; set; }

    /// <summary>
    /// Creates a new archive information instance.
    /// </summary>
    /// <param name="type">The detected archive type.</param>
    /// <param name="supportsRandomAccess">Whether the detected format supports random access.</param>
    public ArchiveInformation(ArchiveType? type, bool supportsRandomAccess)
    {
        Type = type;
        SupportsRandomAccess = supportsRandomAccess;
    }
}
