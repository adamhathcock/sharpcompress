using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers
{
    public interface IReader : IAsyncDisposable
    {
        event EventHandler<ReaderExtractionEventArgs<IEntry>> EntryExtractionProgress;

        event EventHandler<CompressedBytesReadEventArgs> CompressedBytesRead;
        event EventHandler<FilePartExtractionBeginEventArgs> FilePartExtractionBegin;

        ArchiveType ArchiveType { get; }

        IEntry? Entry { get; }

        /// <summary>
        /// Decompresses the current entry to the stream.  This cannot be called twice for the current entry.
        /// </summary>
        /// <param name="writableStream"></param>
        ValueTask WriteEntryToAsync(Stream writableStream, CancellationToken cancellationToken = default);

        bool Cancelled { get; }
        void Cancel();

        /// <summary>
        /// Moves to the next entry by reading more data from the underlying stream.  This skips if data has not been read.
        /// </summary>
        /// <returns></returns>
        ValueTask<bool> MoveToNextEntryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the current entry as a stream that will decompress as it is read.
        /// Read the entire stream or use SkipEntry on EntryStream.
        /// </summary>
        EntryStream OpenEntryStream();
    }
}