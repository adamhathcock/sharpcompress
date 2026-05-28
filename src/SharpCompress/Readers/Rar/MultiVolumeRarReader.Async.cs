using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;

namespace SharpCompress.Readers.Rar;

internal partial class MultiVolumeRarReader : RarReader
{
    protected override IAsyncEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntryAsync()
    {
        var enumerator = new MultiVolumeStreamAsyncEnumerator(this, streams, tempStream);
        tempStream = null;
        return enumerator;
    }

    private class MultiVolumeStreamAsyncEnumerator
        : IAsyncEnumerable<FilePart>,
            IAsyncEnumerator<FilePart>
    {
        private readonly MultiVolumeRarReader reader;
        private readonly IEnumerator<Stream> nextReadableStreams;
        private Stream? tempStream;
        private bool isFirst = true;

        internal MultiVolumeStreamAsyncEnumerator(
            MultiVolumeRarReader r,
            IEnumerator<Stream> nextReadableStreams,
            Stream? tempStream
        )
        {
            reader = r;
            this.nextReadableStreams = nextReadableStreams;
            this.tempStream = tempStream;
        }

        public FilePart Current { get; private set; } = null!;

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
                await reader.LoadStreamForReadingAsync(tempStream).ConfigureAwait(false);
                tempStream = null;
            }
            else if (!nextReadableStreams.MoveNext())
            {
                throw new MultiVolumeExtractionException(
                    "No stream provided when requested by MultiVolumeRarReader"
                );
            }
            else
            {
                await reader
                    .LoadStreamForReadingAsync(nextReadableStreams.Current)
                    .ConfigureAwait(false);
            }

            Current = reader.Entry.Parts.First();
            return true;
        }

        public IAsyncEnumerator<FilePart> GetAsyncEnumerator(
            CancellationToken cancellationToken = new()
        ) => this;

        public ValueTask DisposeAsync() => new();
    }
}
