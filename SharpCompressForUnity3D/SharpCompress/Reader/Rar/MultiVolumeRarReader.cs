namespace SharpCompress.Reader.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    internal class MultiVolumeRarReader : RarReader
    {
        private readonly IEnumerator<Stream> streams;
        private Stream tempStream;

        internal MultiVolumeRarReader(IEnumerable<Stream> streams, Options options) : base(options)
        {
            this.streams = streams.GetEnumerator();
        }

        protected override IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry()
        {
            MultiVolumeStreamEnumerator enumerator = new MultiVolumeStreamEnumerator(this, this.streams, this.tempStream);
            this.tempStream = null;
            return enumerator;
        }

        internal override bool NextEntryForCurrentStream()
        {
            if (!base.NextEntryForCurrentStream())
            {
                return (this.streams.MoveNext() && base.LoadStreamForReading(this.streams.Current));
            }
            return true;
        }

        internal override Stream RequestInitialStream()
        {
            if (!this.streams.MoveNext())
            {
                throw new MultiVolumeExtractionException("No stream provided when requested by MultiVolumeRarReader");
            }
            return this.streams.Current;
        }

        internal override void ValidateArchive(RarVolume archive)
        {
        }

        private class MultiVolumeStreamEnumerator : IEnumerable<FilePart>, IEnumerable, IEnumerator<FilePart>, IDisposable, IEnumerator
        {
            [CompilerGenerated]
            private FilePart _Current_k__BackingField;
            private bool isFirst = true;
            private readonly IEnumerator<Stream> nextReadableStreams;
            private readonly MultiVolumeRarReader reader;
            private Stream tempStream;

            internal MultiVolumeStreamEnumerator(MultiVolumeRarReader r, IEnumerator<Stream> nextReadableStreams, Stream tempStream)
            {
                this.reader = r;
                this.nextReadableStreams = nextReadableStreams;
                this.tempStream = tempStream;
            }

            public void Dispose()
            {
            }

            public IEnumerator<FilePart> GetEnumerator()
            {
                return this;
            }

            public bool MoveNext()
            {
                if (this.isFirst)
                {
                    this.Current = Enumerable.First<FilePart>(this.reader.Entry.Parts);
                    this.isFirst = false;
                    return true;
                }
                if (!this.reader.Entry.IsSplit)
                {
                    return false;
                }
                if (this.tempStream != null)
                {
                    this.reader.LoadStreamForReading(this.tempStream);
                    this.tempStream = null;
                }
                else
                {
                    if (!this.nextReadableStreams.MoveNext())
                    {
                        throw new MultiVolumeExtractionException("No stream provided when requested by MultiVolumeRarReader");
                    }
                    this.reader.LoadStreamForReading(this.nextReadableStreams.Current);
                }
                this.Current = Enumerable.First<FilePart>(this.reader.Entry.Parts);
                return true;
            }

            public void Reset()
            {
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            public FilePart Current
            {
                [CompilerGenerated]
                get
                {
                    return this._Current_k__BackingField;
                }
                [CompilerGenerated]
                private set
                {
                    this._Current_k__BackingField = value;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }
        }
    }
}

