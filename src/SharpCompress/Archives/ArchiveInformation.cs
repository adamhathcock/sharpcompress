using System.IO;
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
    /// Indicates the safest known concurrent extraction strategy for this archive instance.
    /// </summary>
    public ArchiveConcurrencyMode ConcurrencyMode { get; set; } =
        ArchiveConcurrencyMode.SequentialOnly;

    /// <summary>
    /// <see langword="true"/> when entries can be opened through independent streams for concurrent extraction.
    /// </summary>
    public bool SupportsIndependentEntryStreams { get; set; }

    /// <summary>
    /// <see langword="true"/> when parallel extraction requires seekable archive input.
    /// </summary>
    public bool RequiresSeekableStreamForParallelExtraction { get; set; }

    internal FileInfo? ParallelExtractionSourceFile { get; set; }

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

/// <summary>
/// Describes the safest known concurrent extraction strategy for an archive instance.
/// </summary>
public enum ArchiveConcurrencyMode
{
    /// <summary>
    /// Entries must be extracted through the existing sequential path.
    /// </summary>
    SequentialOnly,

    /// <summary>
    /// Entries can be opened independently and extracted concurrently.
    /// </summary>
    IndependentEntries,

    /// <summary>
    /// Entries are grouped into solid blocks that must be read sequentially within each block.
    /// </summary>
    SolidBlocks,

    /// <summary>
    /// The archive contains a mixture of independent and sequential regions.
    /// </summary>
    Mixed,
}
