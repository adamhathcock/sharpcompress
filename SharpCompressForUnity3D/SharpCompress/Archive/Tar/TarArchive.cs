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

        //protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes)
        //{
        //    _LoadEntries_d__0 d__ = new _LoadEntries_d__0(-2);
        //    d__.__4__this = this;
        //    d__.__3__volumes = volumes;
        //    return d__;
        //}
        protected override IEnumerable<TarArchiveEntry> LoadEntries(IEnumerable<TarVolume> volumes) {
            Stream stream = volumes.Single().Stream;
            TarHeader previousHeader = null;
            foreach (TarHeader header in TarHeaderFactory.ReadHeader(StreamingMode.Seekable, stream)) {
                if (header != null) {
                    if (header.EntryType == EntryType.LongName) {
                        previousHeader = header;
                    }
                    else {
                        if (previousHeader != null) {
                            var entry = new TarArchiveEntry(this, new TarFilePart(previousHeader, stream),
                                                            CompressionType.None);

                            var oldStreamPos = stream.Position;

                            using (var entryStream = entry.OpenEntryStream())
                            using (var memoryStream = new MemoryStream()) {
                                //entryStream.TransferTo(memoryStream);
                                Utility.TransferTo(entryStream, memoryStream);
                                memoryStream.Position = 0;
                                var bytes = memoryStream.ToArray();

                                //header.Name = ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length).TrimNulls();
                                header.Name = Utility.TrimNulls(ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length));
                            }

                            stream.Position = oldStreamPos;

                            previousHeader = null;
                        }
                        yield return new TarArchiveEntry(this, new TarFilePart(header, stream), CompressionType.None);
                    }
                }
            }
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

        //[CompilerGenerated]
        //private sealed class _LoadEntries_d__0 : IEnumerable<TarArchiveEntry>, IEnumerable, IEnumerator<TarArchiveEntry>, IEnumerator, IDisposable
        //{
        //    private int __1__state;
        //    private TarArchiveEntry __2__current;
        //    public IEnumerable<TarVolume> __3__volumes;
        //    public TarArchive __4__this;
        //    public IEnumerator<TarHeader> __7__wrap4;
        //    private int __l__initialThreadId;
        //    public TarHeader _header_5__3;
        //    public TarHeader _previousHeader_5__2;
        //    public Stream _stream_5__1;
        //    public IEnumerable<TarVolume> volumes;

        //    [DebuggerHidden]
        //    public _LoadEntries_d__0(int __1__state)
        //    {
        //        this.__1__state = __1__state;
        //        this.__l__initialThreadId = Thread.CurrentThread.ManagedThreadId;
        //    }

        //    private void __m__Finally5()
        //    {
        //        this.__1__state = -1;
        //        if (this.__7__wrap4 != null)
        //        {
        //            this.__7__wrap4.Dispose();
        //        }
        //    }

        //    public bool MoveNext()
        //    {
        //        bool flag;
        //        try
        //        {
        //            int num2 = this.__1__state;
        //            if (num2 != 0)
        //            {
        //                if (num2 != 4)
        //                {
        //                    goto Label_01D5;
        //                }
        //                goto Label_01AF;
        //            }
        //            this.__1__state = -1;
        //            this._stream_5__1 = Enumerable.Single<TarVolume>(this.volumes).Stream;
        //            this._previousHeader_5__2 = null;
        //            this.__7__wrap4 = TarHeaderFactory.ReadHeader(StreamingMode.Seekable, this._stream_5__1).GetEnumerator();
        //            this.__1__state = 1;
        //       Label_01AF_2MoveNext:
        //            while (this.__7__wrap4.MoveNext())
        //            {
        //                this._header_5__3 = this.__7__wrap4.Current;
        //                if (this._header_5__3 == null)
        //                {
        //                    continue;
        //                }
        //                if (this._header_5__3.EntryType == EntryType.LongName)
        //                {
        //                    this._previousHeader_5__2 = this._header_5__3;
        //                    continue;
        //                }
        //                if (this._previousHeader_5__2 != null)
        //                {
        //                    TarArchiveEntry entry = new TarArchiveEntry(this.__4__this, new TarFilePart(this._previousHeader_5__2, this._stream_5__1), CompressionType.None);
        //                    long position = this._stream_5__1.Position;
        //                    using (Stream stream = entry.OpenEntryStream())
        //                    {
        //                        using (MemoryStream stream2 = new MemoryStream())
        //                        {
        //                            Utility.TransferTo(stream, stream2);
        //                            stream2.Position = 0L;
        //                            byte[] bytes = stream2.ToArray();
        //                            this._header_5__3.Name = Utility.TrimNulls(ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length));
        //                        }
        //                    }
        //                    this._stream_5__1.Position = position;
        //                    this._previousHeader_5__2 = null;
        //                }
        //                this.__2__current = new TarArchiveEntry(this.__4__this, new TarFilePart(this._header_5__3, this._stream_5__1), CompressionType.None);
        //                this.__1__state = 4;
        //                return true;
        //            //Label_01AF:
        //              //  this.__1__state = 1;
        //            }
        //        Label_01AF: {
        //                this.__1__state = 1;
        //                goto Label_01AF_2MoveNext;
        //            }
        //            this.__m__Finally5();
        //        Label_01D5:
        //            flag = false;
        //        }
        //        //fault
        //        finally
        //        {
                    
        //            this.Dispose();
                    
        //        }
        //        return flag;
        //    }

        //    [DebuggerHidden]
        //   public IEnumerator<TarArchiveEntry> GetEnumerator()
        //    {
        //        TarArchive._LoadEntries_d__0 d__;
        //        if ((Thread.CurrentThread.ManagedThreadId == this.__l__initialThreadId) && (this.__1__state == -2))
        //        {
        //            this.__1__state = 0;
        //            d__ = this;
        //        }
        //        else
        //        {
        //            d__ = new TarArchive._LoadEntries_d__0(0);
        //            d__.__4__this = this.__4__this;
        //        }
        //        d__.volumes = this.__3__volumes;
        //        return d__;
        //    }

        //   // [DebuggerHidden]
        //   //public IEnumerator GetEnumerator() {
        //        //return this.IEnumerable<TarArchiveEntry>.GetEnumerator();
        //        //return this.System.Collections.Generic.IEnumerable<SharpCompress.Archive.Tar.TarArchiveEntry>.GetEnumerator();
                
        //   // }
        //    #region IEnumerable 成员

        //    IEnumerator IEnumerable.GetEnumerator() {
        //        throw new NotImplementedException();
        //    }

        //    #endregion
        //    [DebuggerHidden]
        //    void IEnumerator.Reset()
        //    {
        //        throw new NotSupportedException();
        //    }

        //    public void Dispose()
        //    {
        //        switch (this.__1__state)
        //        {
        //            case 1:
        //            case 4:
        //                try
        //                {
        //                }
        //                finally
        //                {
        //                    this.__m__Finally5();
        //                }
        //                break;
        //        }
        //    }

        //    TarArchiveEntry IEnumerator<TarArchiveEntry>.Current
        //    {
        //        [DebuggerHidden]
        //        get
        //        {
        //            return this.__2__current;
        //        }
        //    }

        //    object IEnumerator.Current
        //    {
        //        [DebuggerHidden]
        //        get
        //        {
        //            return this.__2__current;
        //        }
        //    }



          
        //}
    }
}

