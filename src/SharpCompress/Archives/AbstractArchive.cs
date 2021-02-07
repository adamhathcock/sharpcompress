using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives
{
    public abstract class AbstractArchive<TEntry, TVolume> : IArchive
        where TEntry : IArchiveEntry
        where TVolume : IVolume
    {
        private readonly LazyReadOnlyCollection<TVolume> lazyVolumes;
        private readonly LazyReadOnlyCollection<TEntry> lazyEntries;

        protected ReaderOptions ReaderOptions { get; } = new ();

        private bool disposed;

        internal AbstractArchive(ArchiveType type, FileInfo fileInfo, ReaderOptions readerOptions)
        {
            Type = type;
            if (!fileInfo.Exists)
            {
                throw new ArgumentException("File does not exist: " + fileInfo.FullName);
            }
            ReaderOptions = readerOptions;
            readerOptions.LeaveStreamOpen = false;
            lazyVolumes = new LazyReadOnlyCollection<TVolume>(LoadVolumes(fileInfo));
            lazyEntries = new LazyReadOnlyCollection<TEntry>(LoadEntries(Volumes));
        }


        protected abstract IAsyncEnumerable<TVolume> LoadVolumes(FileInfo file);

        internal AbstractArchive(ArchiveType type, IAsyncEnumerable<Stream> streams, ReaderOptions readerOptions)
        {
            Type = type;
            ReaderOptions = readerOptions;
            lazyVolumes = new LazyReadOnlyCollection<TVolume>(LoadVolumes(streams.Select(CheckStreams)));
            lazyEntries = new LazyReadOnlyCollection<TEntry>(LoadEntries(Volumes));
        }

        internal AbstractArchive(ArchiveType type)
        {
            Type = type;
            lazyVolumes = new LazyReadOnlyCollection<TVolume>( AsyncEnumerable.Empty<TVolume>());
            lazyEntries = new LazyReadOnlyCollection<TEntry>(AsyncEnumerable.Empty<TEntry>());
        }

        public ArchiveType Type { get; }

        private static Stream CheckStreams(Stream stream)
        {
            if (!stream.CanSeek || !stream.CanRead)
            {
                throw new ArgumentException("Archive streams must be Readable and Seekable");
            }
            return stream;
        }

        /// <summary>
        /// Returns an ReadOnlyCollection of all the RarArchiveEntries across the one or many parts of the RarArchive.
        /// </summary>
        public virtual IAsyncEnumerable<TEntry> Entries => lazyEntries;

        /// <summary>
        /// Returns an ReadOnlyCollection of all the RarArchiveVolumes across the one or many parts of the RarArchive.
        /// </summary>
        public IAsyncEnumerable<TVolume> Volumes => lazyVolumes;

        /// <summary>
        /// The total size of the files compressed in the archive.
        /// </summary>
        public virtual async ValueTask<long> TotalSizeAsync()
        {
            await EnsureEntriesLoaded();
            return await Entries.AggregateAsync(0L, (total, cf) => total + cf.CompressedSize);
        }

        /// <summary>
        /// The total size of the files as uncompressed in the archive.
        /// </summary>
        public virtual async ValueTask<long> TotalUncompressedSizeAsync()
        {
            await EnsureEntriesLoaded();
            return await Entries.AggregateAsync(0L, (total, cf) => total + cf.Size);
        }

        protected abstract IAsyncEnumerable<TVolume> LoadVolumes(IAsyncEnumerable<Stream> streams);
        protected abstract IAsyncEnumerable<TEntry> LoadEntries(IAsyncEnumerable<TVolume> volumes);

        IAsyncEnumerable<IArchiveEntry> IArchive.Entries => Entries.Select(x => (IArchiveEntry)x);

        IAsyncEnumerable<IVolume> IArchive.Volumes => lazyVolumes.Select(x => (IVolume)x);

        public virtual async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                await lazyVolumes.ForEachAsync(v => v.Dispose());
                lazyEntries.GetLoaded().Cast<Entry>().ForEach(x => x.Close());
                disposed = true;
            }
        }

        /// <summary>
        /// Use this method to extract all entries in an archive in order.
        /// This is primarily for SOLID Rar Archives or 7Zip Archives as they need to be
        /// extracted sequentially for the best performance.
        ///
        /// This method will load all entry information from the archive.
        ///
        /// WARNING: this will reuse the underlying stream for the archive.  Errors may
        /// occur if this is used at the same time as other extraction methods on this instance.
        /// </summary>
        /// <returns></returns>
        public async ValueTask<IReader> ExtractAllEntries()
        {
            await EnsureEntriesLoaded();
            return await CreateReaderForSolidExtraction();
        }
        
        public async ValueTask EnsureEntriesLoaded()
        {
            await lazyEntries.EnsureFullyLoaded();
            await lazyVolumes.EnsureFullyLoaded();
        }

        protected abstract ValueTask<IReader> CreateReaderForSolidExtraction();

        /// <summary>
        /// Archive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
        /// </summary>
        public virtual ValueTask<bool> IsSolidAsync() => new(false);

        /// <summary>
        /// The archive can find all the parts of the archive needed to fully extract the archive.  This forces the parsing of the entire archive.
        /// </summary>
        public async ValueTask<bool> IsCompleteAsync()
        {
            await EnsureEntriesLoaded();
            return await Entries.AllAsync(x => x.IsComplete);
        }
    }
}
