namespace SharpCompress.Reader
{
    using SharpCompress;
    using SharpCompress.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public abstract class AbstractReader<TEntry, TVolume> : IReader, IDisposable, IReaderExtractionListener, IExtractionListener where TEntry: SharpCompress.Common.Entry where TVolume: SharpCompress.Common.Volume
    {
        [CompilerGenerated]
        private SharpCompress.Common.ArchiveType <ArchiveType>k__BackingField;
        [CompilerGenerated]
        private bool <Cancelled>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Options <Options>k__BackingField;
        private bool completed;
        private IEnumerator<TEntry> entriesForCurrentReadStream;
        private readonly byte[] skipBuffer;
        private bool wroteCurrentEntry;

        public event EventHandler<CompressedBytesReadEventArgs> CompressedBytesRead;

        public event EventHandler<ReaderExtractionEventArgs<IEntry>> EntryExtractionBegin;

        public event EventHandler<ReaderExtractionEventArgs<IEntry>> EntryExtractionEnd;

        public event EventHandler<FilePartExtractionBeginEventArgs> FilePartExtractionBegin;

        internal AbstractReader(SharpCompress.Common.Options options, SharpCompress.Common.ArchiveType archiveType)
        {
            this.skipBuffer = new byte[0x1000];
            this.ArchiveType = archiveType;
            this.Options = options;
        }

        public void Cancel()
        {
            if (!this.completed)
            {
                this.Cancelled = true;
            }
        }

        protected EntryStream CreateEntryStream(Stream decompressed)
        {
            return new EntryStream(this, decompressed);
        }

        public void Dispose()
        {
            if (this.entriesForCurrentReadStream != null)
            {
                this.entriesForCurrentReadStream.Dispose();
            }
            this.Volume.Dispose();
        }

        internal abstract IEnumerable<TEntry> GetEntries(Stream stream);
        protected virtual EntryStream GetEntryStream()
        {
            return this.CreateEntryStream(Enumerable.First<FilePart>(this.Entry.Parts).GetCompressedStream());
        }

        internal bool LoadStreamForReading(Stream stream)
        {
            if (this.entriesForCurrentReadStream != null)
            {
                this.entriesForCurrentReadStream.Dispose();
            }
            if (!((stream != null) && stream.CanRead))
            {
                throw new MultipartStreamRequiredException("File is split into multiple archives: '" + this.Entry.Key + "'. A new readable stream is required.  Use Cancel if it was intended.");
            }
            this.entriesForCurrentReadStream = this.GetEntries(stream).GetEnumerator();
            return this.entriesForCurrentReadStream.MoveNext();
        }

        public bool MoveToNextEntry()
        {
            if (!this.completed)
            {
                if (this.Cancelled)
                {
                    throw new InvalidOperationException("Reader has been cancelled.");
                }
                if (this.entriesForCurrentReadStream == null)
                {
                    return this.LoadStreamForReading(this.RequestInitialStream());
                }
                if (!this.wroteCurrentEntry)
                {
                    this.SkipEntry();
                }
                this.wroteCurrentEntry = false;
                if (this.NextEntryForCurrentStream())
                {
                    return true;
                }
                this.completed = true;
            }
            return false;
        }

        internal virtual bool NextEntryForCurrentStream()
        {
            return this.entriesForCurrentReadStream.MoveNext();
        }

        public EntryStream OpenEntryStream()
        {
            if (this.wroteCurrentEntry)
            {
                throw new ArgumentException("WriteEntryTo or OpenEntryStream can only be called once.");
            }
            EntryStream entryStream = this.GetEntryStream();
            this.wroteCurrentEntry = true;
            return entryStream;
        }

        internal virtual Stream RequestInitialStream()
        {
            return this.Volume.Stream;
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

        void IReaderExtractionListener.FireEntryExtractionBegin(SharpCompress.Common.Entry entry)
        {
            if (this.EntryExtractionBegin != null)
            {
                this.EntryExtractionBegin(this, new ReaderExtractionEventArgs<IEntry>(entry));
            }
        }

        void IReaderExtractionListener.FireEntryExtractionEnd(SharpCompress.Common.Entry entry)
        {
            if (this.EntryExtractionEnd != null)
            {
                this.EntryExtractionEnd(this, new ReaderExtractionEventArgs<IEntry>(entry));
            }
        }

        private void Skip()
        {
            if (!this.Entry.IsSolid)
            {
                Stream rawStream = Enumerable.First<FilePart>(this.Entry.Parts).GetRawStream();
                if (rawStream != null)
                {
                    long compressedSize = this.Entry.CompressedSize;
                    for (int i = 0; i < (compressedSize / ((long) this.skipBuffer.Length)); i++)
                    {
                        rawStream.Read(this.skipBuffer, 0, this.skipBuffer.Length);
                    }
                    rawStream.Read(this.skipBuffer, 0, (int) (compressedSize % ((long) this.skipBuffer.Length)));
                    return;
                }
            }
            using (EntryStream stream2 = this.OpenEntryStream())
            {
                while (stream2.Read(this.skipBuffer, 0, this.skipBuffer.Length) > 0)
                {
                }
            }
        }

        private void SkipEntry()
        {
            if (!this.Entry.IsDirectory)
            {
                this.Skip();
            }
        }

        internal void Write(Stream writeStream)
        {
            using (Stream stream = this.OpenEntryStream())
            {
                Utility.TransferTo(stream, writeStream);
            }
        }

        public void WriteEntryTo(Stream writableStream)
        {
            if (this.wroteCurrentEntry)
            {
                throw new ArgumentException("WriteEntryTo or OpenEntryStream can only be called once.");
            }
            if (!((writableStream != null) && writableStream.CanWrite))
            {
                throw new ArgumentNullException("A writable Stream was required.  Use Cancel if that was intended.");
            }
            IReaderExtractionListener listener = this;
            listener.FireEntryExtractionBegin(this.Entry);
            this.Write(writableStream);
            listener.FireEntryExtractionEnd(this.Entry);
            this.wroteCurrentEntry = true;
        }

        public SharpCompress.Common.ArchiveType ArchiveType
        {
            [CompilerGenerated]
            get
            {
                return this.<ArchiveType>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<ArchiveType>k__BackingField = value;
            }
        }

        public bool Cancelled
        {
            [CompilerGenerated]
            get
            {
                return this.<Cancelled>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Cancelled>k__BackingField = value;
            }
        }

        public TEntry Entry
        {
            get
            {
                return this.entriesForCurrentReadStream.Current;
            }
        }

        internal SharpCompress.Common.Options Options
        {
            [CompilerGenerated]
            get
            {
                return this.<Options>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Options>k__BackingField = value;
            }
        }

        IEntry IReader.Entry
        {
            get
            {
                return this.Entry;
            }
        }

        public abstract TVolume Volume { get; }
    }
}

