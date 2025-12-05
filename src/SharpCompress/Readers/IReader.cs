using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers;

public interface IReader : IDisposable
{
    ArchiveType ArchiveType { get; }

    IEntry Entry { get; }

    /// <summary>
    /// Decompresses the current entry to the stream.  This cannot be called twice for the current entry.
    /// </summary>
    /// <param name="writableStream"></param>
    void WriteEntryTo(Stream writableStream);

    /// <summary>
    /// Decompresses the current entry to the stream asynchronously.  This cannot be called twice for the current entry.
    /// </summary>
    /// <param name="writableStream"></param>
    /// <param name="cancellationToken"></param>
    Task WriteEntryToAsync(Stream writableStream, CancellationToken cancellationToken = default);

    bool Cancelled { get; }
    void Cancel();

    /// <summary>
    /// Moves to the next entry by reading more data from the underlying stream.  This skips if data has not been read.
    /// </summary>
    /// <returns></returns>
    bool MoveToNextEntry();

    /// <summary>
    /// Moves to the next entry asynchronously by reading more data from the underlying stream.  This skips if data has not been read.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> MoveToNextEntryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the current entry as a stream that will decompress as it is read.
    /// Read the entire stream or use SkipEntry on EntryStream.
    /// </summary>
    EntryStream OpenEntryStream();

    /// <summary>
    /// Opens the current entry asynchronously as a stream that will decompress as it is read.
    /// Read the entire stream or use SkipEntry on EntryStream.
    /// </summary>
    /// <param name="cancellationToken"></param>
    Task<EntryStream> OpenEntryStreamAsync(CancellationToken cancellationToken = default);
}
