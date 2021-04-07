using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Readers
{
    /// <summary>
    /// A generic push reader that reads unseekable comrpessed streams.
    /// </summary>
    public abstract class AbstractReader<TEntry, TVolume> : IReader, IReaderExtractionListener
        where TEntry : Entry
        where TVolume : Volume
    {
        private bool completed;
        private IAsyncEnumerator<TEntry>? entriesForCurrentReadStream;
        private bool wroteCurrentEntry;

        public event EventHandler<ReaderExtractionEventArgs<IEntry>>? EntryExtractionProgress;

        public event EventHandler<CompressedBytesReadEventArgs>? CompressedBytesRead;
        public event EventHandler<FilePartExtractionBeginEventArgs>? FilePartExtractionBegin;

        internal AbstractReader(ReaderOptions options, ArchiveType archiveType)
        {
            ArchiveType = archiveType;
            Options = options;
        }

        internal ReaderOptions Options { get; }

        public ArchiveType ArchiveType { get; }

        /// <summary>
        /// Current volume that the current entry resides in
        /// </summary>
        public abstract TVolume Volume { get; }

        /// <summary>
        /// Current file entry 
        /// </summary>
        public TEntry? Entry => entriesForCurrentReadStream?.Current ?? default;

        #region IDisposable Members

        public async ValueTask DisposeAsync()
        {
            await (entriesForCurrentReadStream?.DisposeAsync() ?? new ValueTask());
            await Volume.DisposeAsync();
        }

        #endregion

        public bool Cancelled { get; private set; }

        /// <summary>
        /// Indicates that the remaining entries are not required.
        /// On dispose of an EntryStream, the stream will not skip to the end of the entry.
        /// An attempt to move to the next entry will throw an exception, as the compressed stream is not positioned at an entry boundary.
        /// </summary>
        public void Cancel()
        {
            if (!completed)
            {
                Cancelled = true;
            }
        }

        public async ValueTask<bool> MoveToNextEntryAsync(CancellationToken cancellationToken = default)
        {
            if (completed)
            {
                return false;
            }
            if (Cancelled)
            {
                throw new InvalidOperationException("Reader has been cancelled.");
            }
            if (entriesForCurrentReadStream is null)
            {
                var stream = await RequestInitialStream(cancellationToken);
                if (stream is null || !stream.CanRead)
                {
                    throw new MultipartStreamRequiredException("File is split into multiple archives: '"
                                                               + (Entry?.Key ?? "unknown") +
                                                               "'. A new readable stream is required.  Use Cancel if it was intended.");
                }
                entriesForCurrentReadStream = GetEntries(stream, cancellationToken).GetAsyncEnumerator(cancellationToken);
            } 
            else if (!wroteCurrentEntry)
            {
                await SkipEntry(cancellationToken);
            }
            wroteCurrentEntry = false;
            if (await entriesForCurrentReadStream.MoveNextAsync())
            {
                return true;
            }
            completed = true;
            return false;
        }

        protected virtual ValueTask<Stream> RequestInitialStream(CancellationToken cancellationToken)
        {
            return new(Volume.Stream);
        }

        protected abstract IAsyncEnumerable<TEntry> GetEntries(Stream stream, CancellationToken cancellationToken);

        #region Entry Skip/Write

        private async ValueTask SkipEntry(CancellationToken cancellationToken)
        {
            if (Entry?.IsDirectory != true)
            {
                await SkipAsync(cancellationToken);
            }
        }

        private async ValueTask SkipAsync(CancellationToken cancellationToken)
        {
            if (Entry is null)
            {
                return;
            }
            if (ArchiveType != ArchiveType.Rar
                && !Entry.IsSolid
                && Entry.CompressedSize > 0)
            {
                //not solid and has a known compressed size then we can skip raw bytes.
                var part = Entry.Parts.First();
                var rawStream = part.GetRawStream();

                if (rawStream != null)
                {
                    var bytesToAdvance = Entry.CompressedSize;
                    await rawStream.SkipAsync(bytesToAdvance, cancellationToken: cancellationToken);
                    part.Skipped = true;
                    return;
                }
            }
            //don't know the size so we have to try to decompress to skip
            await using var s = await OpenEntryStreamAsync(cancellationToken);
            await s.SkipAsync(cancellationToken);
        }

        public async ValueTask WriteEntryToAsync(Stream writableStream, CancellationToken cancellationToken = default)
        {
            if (wroteCurrentEntry)
            {
                throw new ArgumentException("WriteEntryTo or OpenEntryStream can only be called once.");
            }
            if ((writableStream is null) || (!writableStream.CanWrite))
            {
                throw new ArgumentNullException("A writable Stream was required.  Use Cancel if that was intended.");
            }

            await WriteAsync(writableStream, cancellationToken);
            wroteCurrentEntry = true;
        }

        private async ValueTask WriteAsync(Stream writeStream, CancellationToken cancellationToken)
        {
            if (Entry is null)
            {
                throw new ArgumentException("Entry is null");
            }
            var streamListener = this as IReaderExtractionListener;
            await using Stream s = await OpenEntryStreamAsync(cancellationToken);
            await s.TransferToAsync(writeStream, Entry, streamListener, cancellationToken);
        }

        public async ValueTask<EntryStream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
        {
            if (wroteCurrentEntry)
            {
                throw new ArgumentException("WriteEntryTo or OpenEntryStream can only be called once.");
            }
            var stream = await GetEntryStreamAsync(cancellationToken);
            wroteCurrentEntry = true;
            return stream;
        }

        /// <summary>
        /// Retains a reference to the entry stream, so we can check whether it completed later.
        /// </summary>
        protected EntryStream CreateEntryStream(Stream decompressed)
        {
            return new(this, decompressed);
        }

        protected async ValueTask<EntryStream> GetEntryStreamAsync(CancellationToken cancellationToken)
        {
            if (Entry is null)
            {
                throw new ArgumentException("Entry is null");
            }
            return CreateEntryStream(await Entry.Parts.First().GetCompressedStreamAsync(cancellationToken));
        }

        #endregion

        IEntry? IReader.Entry => Entry;

        void IExtractionListener.FireCompressedBytesRead(long currentPartCompressedBytes, long compressedReadBytes)
        {
            CompressedBytesRead?.Invoke(this, new CompressedBytesReadEventArgs(
                currentFilePartCompressedBytesRead: currentPartCompressedBytes,
                compressedBytesRead: compressedReadBytes
            ));
        }

        void IExtractionListener.FireFilePartExtractionBegin(string name, long size, long compressedSize)
        {
            FilePartExtractionBegin?.Invoke(this, new FilePartExtractionBeginEventArgs(
                compressedSize: compressedSize,
                size: size,
                name: name
            ));
        }

        void IReaderExtractionListener.FireEntryExtractionProgress(Entry entry, long bytesTransferred, int iterations)
        {
            EntryExtractionProgress?.Invoke(this, new ReaderExtractionEventArgs<IEntry>(entry, new ReaderProgress(entry, bytesTransferred, iterations)));
        }
    }
}