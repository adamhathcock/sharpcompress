using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Rar;

namespace SharpCompress.Reader.Rar
{
    internal class MultiVolumeRarReader : RarReader
    {
        private readonly IEnumerator<Stream> streams;
        private Stream tempStream;

        internal MultiVolumeRarReader(IEnumerable<Stream> streams, Options options)
            : base(options)
        {
            this.streams = streams.GetEnumerator();
        }

        internal override void ValidateArchive(RarVolume archive)
        {
        }

        internal override Stream RequestInitialStream()
        {
            if (streams.MoveNext())
            {
                return streams.Current;
            }
            throw new MultiVolumeExtractionException("No stream provided when requested by MultiVolumeRarReader");
        }

        internal override bool NextEntryForCurrentStream()
        {
            if (!base.NextEntryForCurrentStream())
            {
                //if we're got another stream to try to process then do so
                if (streams.MoveNext() && LoadStreamForReading(streams.Current))
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        protected override IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry()
        {
            var enumerator = new MultiVolumeStreamEnumerator(this, streams, tempStream);
            tempStream = null;
            return enumerator;
        }

        private class MultiVolumeStreamEnumerator : IEnumerable<FilePart>, IEnumerator<FilePart>
        {
            private readonly MultiVolumeRarReader reader;
            private readonly IEnumerator<Stream> nextReadableStreams;
            private Stream tempStream;
            private bool isFirst = true;

            internal MultiVolumeStreamEnumerator(MultiVolumeRarReader r, IEnumerator<Stream> nextReadableStreams, Stream tempStream)
            {
                reader = r;
                this.nextReadableStreams = nextReadableStreams;
                this.tempStream = tempStream;
            }

            public IEnumerator<FilePart> GetEnumerator()
            {
                return this;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this;
            }

            public FilePart Current
            {
                get;
                private set;
            }

            public void Dispose()
            {
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public bool MoveNext()
            {
                if (isFirst)
                {
                    Current = reader.Entry.Parts.First();
                    isFirst = false; //first stream already to go
                    return true;
                }

                if (!reader.Entry.IsSplit)
                {
                    return false;
                }
                if (tempStream != null)
                {
                    reader.LoadStreamForReading(tempStream);
                    tempStream = null;
                }
                else if (!nextReadableStreams.MoveNext())
                {
                    throw new MultiVolumeExtractionException("No stream provided when requested by MultiVolumeRarReader");
                }
                else
                {
                    reader.LoadStreamForReading(nextReadableStreams.Current);
                }

                Current = reader.Entry.Parts.First();
                return true;
            }

            public void Reset()
            {
            }
        }
    }
}
