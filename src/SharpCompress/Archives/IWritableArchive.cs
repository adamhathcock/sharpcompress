using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public interface IWritableArchive : IArchive
{
    void RemoveEntry(IArchiveEntry entry);

    IArchiveEntry AddEntry(
        string key,
        Stream source,
        bool closeStream,
        long size = 0,
        DateTime? modified = null
    );

    IArchiveEntry AddDirectoryEntry(string key, DateTime? modified = null);

    void SaveTo(Stream stream, WriterOptions options);

    Task SaveToAsync(
        Stream stream,
        WriterOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Use this to pause entry rebuilding when adding large collections of entries.  Dispose when complete.  A  using statement is recommended.
    /// </summary>
    /// <returns>IDisposeable to resume entry rebuilding</returns>
    IDisposable PauseEntryRebuilding();
}
