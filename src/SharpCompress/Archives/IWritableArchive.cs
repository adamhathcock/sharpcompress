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

    /// <summary>
    /// Removes the specified entry from the archive.
    /// </summary>
    void RemoveEntry(IArchiveEntry entry);

    IArchiveEntry AddEntry(
        string key,
        Stream source,
        bool closeStream,
        long size = 0,
        DateTime? modified = null
    );

    IArchiveEntry AddDirectoryEntry(string key, DateTime? modified = null);
}

public interface IWritableArchive : IArchive, IWritableArchiveCommon
{
    /// <summary>
    /// Saves the archive to the specified stream using the given writer options.
    /// </summary>
    void SaveTo(Stream stream, WriterOptions options);
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
}
