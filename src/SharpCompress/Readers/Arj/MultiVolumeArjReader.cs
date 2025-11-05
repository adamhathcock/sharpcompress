#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arj;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Readers.Arj;

internal class MultiVolumeArjReader : ArjReader
{
    private readonly IEnumerator<Stream> streams;
    private Stream tempStream;

    internal MultiVolumeArjReader(IEnumerable<Stream> streams, ReaderOptions options)
        : base(options) => this.streams = streams.GetEnumerator();

    protected override void ValidateArchive(ArjVolume archive) { }

    protected override Stream RequestInitialStream()
    {
        if (streams.MoveNext())
        {
            return streams.Current;
        }
        throw new MultiVolumeExtractionException(
            "No stream provided when requested by MultiVolumeArjReader"
        );
    }

    internal override bool NextEntryForCurrentStream()
    {
        if (!base.NextEntryForCurrentStream())
        {
            // if we're got another stream to try to process then do so
            return streams.MoveNext() && LoadStreamForReading(streams.Current);
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
        private readonly MultiVolumeArjReader reader;
        private readonly IEnumerator<Stream> nextReadableStreams;
        private Stream tempStream;
        private bool isFirst = true;

        internal MultiVolumeStreamEnumerator(
            MultiVolumeArjReader r,
            IEnumerator<Stream> nextReadableStreams,
            Stream tempStream
        )
        {
            reader = r;
            this.nextReadableStreams = nextReadableStreams;
            this.tempStream = tempStream;
        }

        public IEnumerator<FilePart> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => this;

        public FilePart Current { get; private set; }

        public void Dispose() { }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (isFirst)
            {
                Current = reader.Entry.Parts.First();
                isFirst = false; //first stream already to go
                return true;
            }

            if (!reader.Entry.IsSplitAfter)
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
                throw new MultiVolumeExtractionException(
                    "No stream provided when requested by MultiVolumeArjReader"
                );
            }
            else
            {
                reader.LoadStreamForReading(nextReadableStreams.Current);
            }

            Current = reader.Entry.Parts.First();
            return true;
        }

        public void Reset() { }
    }
}
