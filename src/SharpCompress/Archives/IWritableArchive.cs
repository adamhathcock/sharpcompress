using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Writers;

namespace SharpCompress.Archives
{
    public interface IWritableArchive : IArchive
    {
        ValueTask RemoveEntryAsync(IArchiveEntry entry, CancellationToken cancellationToken = default);

        ValueTask<IArchiveEntry> AddEntryAsync(string key, Stream source, bool closeStream, long size = 0, DateTime? modified = null, CancellationToken cancellationToken = default);

        ValueTask SaveToAsync(Stream stream, WriterOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Use this to pause entry rebuilding when adding large collections of entries.  Dispose when complete.  A  using statement is recommended.
        /// </summary>
        /// <returns>IDisposeable to resume entry rebuilding</returns>
        IAsyncDisposable PauseEntryRebuilding();
    }
}