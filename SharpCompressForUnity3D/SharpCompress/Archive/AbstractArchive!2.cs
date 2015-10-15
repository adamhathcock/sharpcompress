namespace SharpCompress.Archive
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Reader;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public abstract class AbstractArchive<TEntry, TVolume> : IArchive, IDisposable, IArchiveExtractionListener, IExtractionListener where TEntry: IArchiveEntry where TVolume: IVolume
    {
        [CompilerGenerated]
        private string _Password_k__BackingField;
        [CompilerGenerated]
        private ArchiveType _Type_k__BackingField;
        private bool disposed;
        private readonly LazyReadOnlyCollection<TEntry> lazyEntries;
        private readonly LazyReadOnlyCollection<TVolume> lazyVolumes;

        public event EventHandler<CompressedBytesReadEventArgs> CompressedBytesRead;

        public event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>> EntryExtractionBegin;

        public event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>> EntryExtractionEnd;

        public event EventHandler<FilePartExtractionBeginEventArgs> FilePartExtractionBegin;

        internal AbstractArchive(ArchiveType type)
        {
            this.Type = type;
            this.lazyVolumes = new LazyReadOnlyCollection<TVolume>(Enumerable.Empty<TVolume>());
            this.lazyEntries = new LazyReadOnlyCollection<TEntry>(Enumerable.Empty<TEntry>());
        }

        internal AbstractArchive(ArchiveType type, IEnumerable<Stream> streams, Options options, string password)
        {
            this.Type = type;
            this.Password = password;
            this.lazyVolumes = new LazyReadOnlyCollection<TVolume>(this.LoadVolumes(Enumerable.Select<Stream, Stream>(streams, new Func<Stream, Stream>(AbstractArchive<TEntry, TVolume>.CheckStreams)), options));
            this.lazyEntries = new LazyReadOnlyCollection<TEntry>(this.LoadEntries(this.Volumes));
        }

        private static Stream CheckStreams(Stream stream)
        {
            if (!(stream.CanSeek && stream.CanRead))
            {
                throw new ArgumentException("Archive streams must be Readable and Seekable");
            }
            return stream;
        }

        protected abstract IReader CreateReaderForSolidExtraction();
        public virtual void Dispose()
        {
            if (!this.disposed)
            {
                Utility.ForEach<TVolume>(this.lazyVolumes, delegate (TVolume v) {
                    v.Dispose();
                });
                Utility.ForEach<Entry>(Enumerable.Cast<Entry>(this.lazyEntries.GetLoaded()), delegate (Entry x) {
                    x.Close();
                });
                this.disposed = true;
            }
        }

        public IReader ExtractAllEntries()
        {
            ((IArchiveExtractionListener) this).EnsureEntriesLoaded();
            return this.CreateReaderForSolidExtraction();
        }

        protected abstract IEnumerable<TEntry> LoadEntries(IEnumerable<TVolume> volumes);
        protected abstract IEnumerable<TVolume> LoadVolumes(IEnumerable<Stream> streams, Options options);
        void IArchiveExtractionListener.EnsureEntriesLoaded()
        {
            this.lazyEntries.EnsureFullyLoaded();
            this.lazyVolumes.EnsureFullyLoaded();
        }

        void IArchiveExtractionListener.FireEntryExtractionBegin(IArchiveEntry entry)
        {
            if (this.EntryExtractionBegin != null)
            {
                this.EntryExtractionBegin(this, new ArchiveExtractionEventArgs<IArchiveEntry>(entry));
            }
        }

        void IArchiveExtractionListener.FireEntryExtractionEnd(IArchiveEntry entry)
        {
            if (this.EntryExtractionEnd != null)
            {
                this.EntryExtractionEnd(this, new ArchiveExtractionEventArgs<IArchiveEntry>(entry));
            }
        }

        void IExtractionListener.FireCompressedBytesRead(long currentPartCompressedBytes, long compressedReadBytes)
        {
            if (this.CompressedBytesRead != null)
            {
                CompressedBytesReadEventArgs e = new CompressedBytesReadEventArgs();
                e.CurrentFilePartCompressedBytesRead = currentPartCompressedBytes;
                e.CompressedBytesRead = compressedReadBytes;
                this.CompressedBytesRead(this, e);
            }
        }

        void IExtractionListener.FireFilePartExtractionBegin(string name, long size, long compressedSize)
        {
            if (this.FilePartExtractionBegin != null)
            {
                FilePartExtractionBeginEventArgs e = new FilePartExtractionBeginEventArgs();
                e.CompressedSize = compressedSize;
                e.Size = size;
                e.Name = name;
                this.FilePartExtractionBegin(this, e);
            }
        }

        public virtual ICollection<TEntry> Entries
        {
            get
            {
                return this.lazyEntries;
            }
        }

        public bool IsComplete
        {
            get
            {
                ((IArchiveExtractionListener) this).EnsureEntriesLoaded();
                return Enumerable.All<TEntry>(this.Entries, delegate (TEntry x) {
                    return x.IsComplete;
                });
            }
        }

        public virtual bool IsSolid
        {
            get
            {
                return false;
            }
        }

        protected string Password
        {
            [CompilerGenerated]
            get
            {
                return this._Password_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Password_k__BackingField = value;
            }
        }

        IEnumerable<IArchiveEntry> IArchive.Entries
        {
            get
            {
                return Enumerable.Cast<IArchiveEntry>(this.Entries);
            }
        }

        IEnumerable<IVolume> IArchive.Volumes
        {
            get
            {
                return Enumerable.Cast<IVolume>(this.lazyVolumes);
            }
        }

        public virtual long TotalSize
        {
            get
            {
                return Enumerable.Aggregate<TEntry, long>(this.Entries, 0L, delegate (long total, TEntry cf) {
                    return total + cf.CompressedSize;
                });
            }
        }

        public virtual long TotalUncompressSize
        {
            get
            {
                return Enumerable.Aggregate<TEntry, long>(this.Entries, 0L, delegate (long total, TEntry cf) {
                    return total + cf.Size;
                });
            }
        }

        public ArchiveType Type
        {
            [CompilerGenerated]
            get
            {
                return this._Type_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Type_k__BackingField = value;
            }
        }

        public ICollection<TVolume> Volumes
        {
            get
            {
                return this.lazyVolumes;
            }
        }
    }
}

