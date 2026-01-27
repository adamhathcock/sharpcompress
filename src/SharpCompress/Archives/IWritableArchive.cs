using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public interface IWritableArchiveCommon
{
    /// <summary>
    /// Use this to pause entry rebuilding when adding large collections of entries.  Dispose when complete.  A  using statement is recommended.
    /// </summary>
    /// <returns>IDisposeable to resume entry rebuilding</returns>
    IDisposable PauseEntryRebuilding();
}

public interface IWritableArchive : IArchive, IWritableArchiveCommon
{
    IArchiveEntry AddEntry(
        string key,
        Stream source,
        bool closeStream,
        long size = 0,
        DateTime? modified = null
    );

    IArchiveEntry AddDirectoryEntry(string key, DateTime? modified = null);

    /// <summary>
    /// Saves the archive to the specified stream using the given writer options.
    /// </summary>
    void SaveTo(Stream stream, WriterOptions options);

    /// <summary>
    /// Removes the specified entry from the archive.
    /// </summary>
    void RemoveEntry(IArchiveEntry entry);
}

public interface IWritableAsyncArchive : IAsyncArchive, IWritableArchiveCommon
{
    /// <summary>
    /// Asynchronously saves the archive to the specified stream using the given writer options.
    /// </summary>
    ValueTask SaveToAsync(
        Stream stream,
        WriterOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously adds an entry to the archive with the specified key, source stream, and options.
    /// </summary>
    ValueTask<IArchiveEntry> AddEntryAsync(
        string key,
        Stream source,
        bool closeStream,
        long size = 0,
        DateTime? modified = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously adds a directory entry to the archive with the specified key and modification time.
    /// </summary>
    ValueTask<IArchiveEntry> AddDirectoryEntryAsync(
        string key,
        DateTime? modified = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes the specified entry from the archive.
    /// </summary>
    ValueTask RemoveEntryAsync(IArchiveEntry entry);
}
