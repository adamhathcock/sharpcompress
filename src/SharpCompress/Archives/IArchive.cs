using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public interface IArchiveAsync : IAsyncDisposable
{
    IAsyncEnumerable<IArchiveEntry> EntriesAsync { get; }
    IAsyncEnumerable<IVolume> VolumesAsync { get; }

    ArchiveType Type { get; }

    /// <summary>
    /// Use this method to extract all entries in an archive in order.
    /// This is primarily for SOLID Rar Archives or 7Zip Archives as they need to be
    /// extracted sequentially for the best performance.
    /// </summary>
    ValueTask<IReader> ExtractAllEntriesAsync();

    /// <summary>
    /// Archive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
    /// Rar Archives can be SOLID while all 7Zip archives are considered SOLID.
    /// </summary>
    ValueTask<bool> IsSolidAsync();

    /// <summary>
    /// This checks to see if all the known entries have IsComplete = true
    /// </summary>
    ValueTask<bool> IsCompleteAsync();

    /// <summary>
    /// The total size of the files compressed in the archive.
    /// </summary>
    ValueTask<long> TotalSizeAsync();

    /// <summary>
    /// The total size of the files as uncompressed in the archive.
    /// </summary>
    ValueTask<long> TotalUncompressSizeAsync();
}

public interface IArchive : IDisposable
{
    IEnumerable<IArchiveEntry> Entries { get; }
    IEnumerable<IVolume> Volumes { get; }

    ArchiveType Type { get; }

    /// <summary>
    /// Use this method to extract all entries in an archive in order.
    /// This is primarily for SOLID Rar Archives or 7Zip Archives as they need to be
    /// extracted sequentially for the best performance.
    /// </summary>
    IReader ExtractAllEntries();

    /// <summary>
    /// Archive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
    /// Rar Archives can be SOLID while all 7Zip archives are considered SOLID.
    /// </summary>
    bool IsSolid { get; }

    /// <summary>
    /// This checks to see if all the known entries have IsComplete = true
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// The total size of the files compressed in the archive.
    /// </summary>
    long TotalSize { get; }

    /// <summary>
    /// The total size of the files as uncompressed in the archive.
    /// </summary>
    long TotalUncompressSize { get; }
}
