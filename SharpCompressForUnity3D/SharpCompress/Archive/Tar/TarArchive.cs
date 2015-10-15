namespace SharpCompress.Archive.Tar
{
    using SharpCompress;
    using SharpCompress.Archive;
    using SharpCompress.Common;
    using SharpCompress.Common.Tar;
    using SharpCompress.Common.Tar.Headers;
    using SharpCompress.IO;
    using SharpCompress.Reader;
    using SharpCompress.Reader.Tar;
    using SharpCompress.Writer.Tar;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
    {
        internal TarArchive() : base(ArchiveType.Tar)
        {
        }

        internal TarArchive(Stream stream, Options options) : base(ArchiveType.Tar, stream, options)
        {
        }

        public static TarArchive Create()
        {
            return new TarArchive();
        }

        protected override TarArchiveEntry CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified, bool closeStream)
        {
            return new TarWritableArchiveEntry(this, source, CompressionType.Unknown, filePath, size, modified, closeStream);
        }

        protected override IReader CreateReaderForSolidExtraction()
        {
            Stream stream = Enumerable.Single<TarVolume>(base.Volumes).Stream;
            stream.Position = 0L;
            return TarReader.Open(stream, Options.KeepStreamsOpen);
        }

        public static bool IsTarFile(Stream stream)
        {
            try
            {
                TarHeader header = new TarHeader();
                header.Read(new BinaryReader(stream));
                return ((header.Name.Length > 0) && Enum.IsDefined(typeof(EntryType), header.EntryType));
            }
            catch
            {
            }
            return false;
        }

        protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
        {
            <LoadEntries>d__0 d__ = new <LoadEntries>d__0(-2);
            d__.<>4__this = this;
            d__.<>3__volumes = volumes;
            return d__;
        }

        protected override IEnumerable<TarVolume> LoadVolumes(IEnumerable<Stream> streams, Options options)
        {
            return Utility.AsEnumerable<TarVolume>(new TarVolume(Enumerable.First<Stream>(streams), options));
        }

        public static TarArchive Open(Stream stream)
        {
            Utility.CheckNotNull(stream, "stream");
            return Open(stream, Options.None);
        }

        public static TarArchive Open(Stream stream, Options options)
        {
            Utility.CheckNotNull(stream, "stream");
            return new TarArchive(stream, options);
        }

        protected override void SaveTo(Stream stream, CompressionInfo compressionInfo, IEnumerable<TarArchiveEntry> oldEntries, IEnumerable<TarArchiveEntry> newEntries)
        {
            using (TarWriter writer = new TarWriter(stream, compressionInfo))
            {
                foreach (TarArchiveEntry entry in Enumerable.Where<TarArchiveEntry>(Enumerable.Concat<TarArchiveEntry>(oldEntries, newEntries), delegate (TarArchiveEntry x) {
                    return !x.IsDirectory;
                }))
                {
                    using (Stream stream2 = entry.OpenEntryStream())
                    {
                        writer.Write(entry.Key, stream2, entry.LastModifiedTime, new long?(entry.Size));
                    }
                }
            }
        }

        [CompilerGenerated]
        private sealed class <LoadEntries>d__0 : IEnumerable<TarArchiveEntry>, IEnumerable, IEnumerator<TarArchiveEntry>, IEnumerator, IDisposable
        {
            private int <>1__state;
            private TarArchiveEntry <>2__current;
            public IEnumerable<TarVolume> <>3__volumes;
            public TarArchive <>4__this;
            public IEnumerator<TarHeader> <>7__wrap4;
            private int <>l__initialThreadId;
            public TarHeader <header>5__3;
            public TarHeader <previousHeader>5__2;
            public Stream <stream>5__1;
            public IEnumerable<TarVolume> volumes;

            [DebuggerHidden]
            public <LoadEntries>d__0(int <>1__state)
            {
                this.<>1__state = <>1__state;
                this.<>l__initialThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            private void <>m__Finally5()
            {
                this.<>1__state = -1;
                if (this.<>7__wrap4 != null)
                {
                    this.<>7__wrap4.Dispose();
                }
            }

            private bool MoveNext()
            {
                bool flag;
                try
                {
                    int num2 = this.<>1__state;
                    if (num2 != 0)
                    {
                        if (num2 != 4)
                        {
                            goto Label_01D5;
                        }
                        goto Label_01AF;
                    }
                    this.<>1__state = -1;
                    this.<stream>5__1 = Enumerable.Single<TarVolume>(this.volumes).Stream;
                    this.<previousHeader>5__2 = null;
                    this.<>7__wrap4 = TarHeaderFactory.ReadHeader(StreamingMode.Seekable, this.<stream>5__1).GetEnumerator();
                    this.<>1__state = 1;
                    while (this.<>7__wrap4.MoveNext())
                    {
                        this.<header>5__3 = this.<>7__wrap4.Current;
                        if (this.<header>5__3 == null)
                        {
                            continue;
                        }
                        if (this.<header>5__3.EntryType == EntryType.LongName)
                        {
                            this.<previousHeader>5__2 = this.<header>5__3;
                            continue;
                        }
                        if (this.<previousHeader>5__2 != null)
                        {
                            TarArchiveEntry entry = new TarArchiveEntry(this.<>4__this, new TarFilePart(this.<previousHeader>5__2, this.<stream>5__1), CompressionType.None);
                            long position = this.<stream>5__1.Position;
                            using (Stream stream = entry.OpenEntryStream())
                            {
                                using (MemoryStream stream2 = new MemoryStream())
                                {
                                    Utility.TransferTo(stream, stream2);
                                    stream2.Position = 0L;
                                    byte[] bytes = stream2.ToArray();
                                    this.<header>5__3.Name = Utility.TrimNulls(ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length));
                                }
                            }
                            this.<stream>5__1.Position = position;
                            this.<previousHeader>5__2 = null;
                        }
                        this.<>2__current = new TarArchiveEntry(this.<>4__this, new TarFilePart(this.<header>5__3, this.<stream>5__1), CompressionType.None);
                        this.<>1__state = 4;
                        return true;
                    Label_01AF:
                        this.<>1__state = 1;
                    }
                    this.<>m__Finally5();
                Label_01D5:
                    flag = false;
                }
                fault
                {
                    this.System.IDisposable.Dispose();
                }
                return flag;
            }

            [DebuggerHidden]
            IEnumerator<TarArchiveEntry> IEnumerable<TarArchiveEntry>.GetEnumerator()
            {
                TarArchive.<LoadEntries>d__0 d__;
                if ((Thread.CurrentThread.ManagedThreadId == this.<>l__initialThreadId) && (this.<>1__state == -2))
                {
                    this.<>1__state = 0;
                    d__ = this;
                }
                else
                {
                    d__ = new TarArchive.<LoadEntries>d__0(0);
                    d__.<>4__this = this.<>4__this;
                }
                d__.volumes = this.<>3__volumes;
                return d__;
            }

            [DebuggerHidden]
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.System.Collections.Generic.IEnumerable<SharpCompress.Archive.Tar.TarArchiveEntry>.GetEnumerator();
            }

            [DebuggerHidden]
            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }

            void IDisposable.Dispose()
            {
                switch (this.<>1__state)
                {
                    case 1:
                    case 4:
                        try
                        {
                        }
                        finally
                        {
                            this.<>m__Finally5();
                        }
                        break;
                }
            }

            TarArchiveEntry IEnumerator<TarArchiveEntry>.Current
            {
                [DebuggerHidden]
                get
                {
                    return this.<>2__current;
                }
            }

            object IEnumerator.Current
            {
                [DebuggerHidden]
                get
                {
                    return this.<>2__current;
                }
            }
        }
    }
}

