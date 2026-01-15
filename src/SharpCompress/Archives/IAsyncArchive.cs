using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public interface IAsyncArchive : IAsyncDisposable
{
    IAsyncEnumerable<IArchiveEntry> EntriesAsync { get; }
    IAsyncEnumerable<IVolume> VolumesAsync { get; }

    ArchiveType Type { get; }

    /// <summary>
    /// Use this method to extract all entries in an archive in order.
    /// This is primarily for SOLID Rar Archives or 7Zip Archives as they need to be
    /// extracted sequentially for the best performance.
    /// </summary>
    ValueTask<IAsyncReader> ExtractAllEntriesAsync();

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
    ValueTask<long> TotalUncompressedSizeAsync();

    /// <summary>
    /// Returns whether the archive is encrypted.
    /// </summary>
    ValueTask<bool> IsEncryptedAsync();
}
