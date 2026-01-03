#nullable disable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;

namespace SharpCompress.Readers.Rar;

internal class MultiVolumeRarReader : RarReader
{
    private readonly IEnumerator<Stream> streams;
    private Stream tempStream;

    internal MultiVolumeRarReader(IEnumerable<Stream> streams, ReaderOptions options)
        : base(options) => this.streams = streams.GetEnumerator();

    protected override void ValidateArchive(RarVolume archive) { }

    protected override Stream RequestInitialStream()
    {
        if (streams.MoveNext())
        {
            return streams.Current;
        }
        throw new MultiVolumeExtractionException(
            "No stream provided when requested by MultiVolumeRarReader"
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

    internal override async Task<bool> NextEntryForCurrentStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!await base.NextEntryForCurrentStreamAsync(cancellationToken))
        {
            // if we're got another stream to try to process then do so
            return streams.MoveNext()
                && await LoadStreamForReadingAsync(streams.Current, cancellationToken);
        }
        return true;
    }

    protected override IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry()
    {
        var enumerator = new MultiVolumeStreamEnumerator(this, streams, tempStream);
        tempStream = null;
        return enumerator;
    }

    protected override IAsyncEnumerable<FilePart> CreateFilePartAsyncEnumerableForCurrentEntry(
        CancellationToken cancellationToken = default
    )
    {
        var enumerator = new MultiVolumeStreamAsyncEnumerable(
            this,
            streams,
            tempStream,
            cancellationToken
        );
        tempStream = null;
        return enumerator;
    }

    private class MultiVolumeStreamEnumerator : IEnumerable<FilePart>, IEnumerator<FilePart>
    {
        private readonly MultiVolumeRarReader reader;
        private readonly IEnumerator<Stream> nextReadableStreams;
        private Stream tempStream;
        private bool isFirst = true;

        internal MultiVolumeStreamEnumerator(
            MultiVolumeRarReader r,
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
                var result = reader.LoadStreamForReading(tempStream);
                tempStream = null;
                Current = reader.Entry.Parts.First();
                return result;
            }
            else if (!nextReadableStreams.MoveNext())
            {
                throw new MultiVolumeExtractionException(
                    "No stream provided when requested by MultiVolumeRarReader"
                );
            }
            else
            {
                var result = reader.LoadStreamForReading(nextReadableStreams.Current);
                Current = reader.Entry.Parts.First();
                return result;
            }
        }

        public void Reset() { }
    }

    private class MultiVolumeStreamAsyncEnumerable
        : IAsyncEnumerable<FilePart>,
            IAsyncEnumerator<FilePart>
    {
        private readonly MultiVolumeRarReader reader;
        private readonly IEnumerator<Stream> nextReadableStreams;
        private Stream tempStream;
        private bool isFirst = true;
        private readonly CancellationToken cancellationToken;

        internal MultiVolumeStreamAsyncEnumerable(
            MultiVolumeRarReader r,
            IEnumerator<Stream> nextReadableStreams,
            Stream tempStream,
            CancellationToken cancellationToken
        )
        {
            reader = r;
            this.nextReadableStreams = nextReadableStreams;
            this.tempStream = tempStream;
            this.cancellationToken = cancellationToken;
        }

        public IAsyncEnumerator<FilePart> GetAsyncEnumerator(
            CancellationToken cancellationToken = default
        ) => this;

        public FilePart Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
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
                var result = await reader.LoadStreamForReadingAsync(tempStream, cancellationToken);
                tempStream = null;
                Current = reader.Entry.Parts.First();
                return result;
            }
            else if (!nextReadableStreams.MoveNext())
            {
                throw new MultiVolumeExtractionException(
                    "No stream provided when requested by MultiVolumeRarReader"
                );
            }
            else
            {
                var result = await reader.LoadStreamForReadingAsync(
                    nextReadableStreams.Current,
                    cancellationToken
                );
                Current = reader.Entry.Parts.First();
                return result;
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
