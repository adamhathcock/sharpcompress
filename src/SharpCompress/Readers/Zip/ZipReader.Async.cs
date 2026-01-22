using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Readers.Zip;

public partial class ZipReader
{
    /// <summary>
    /// Adapts an async header sequence into an async entry sequence.
    /// </summary>
    private sealed class ZipEntryAsyncEnumerable : IAsyncEnumerable<ZipEntry>
    {
        private readonly StreamingZipHeaderFactory _headerFactory;
        private readonly Stream _stream;

        public ZipEntryAsyncEnumerable(StreamingZipHeaderFactory headerFactory, Stream stream)
        {
            _headerFactory = headerFactory;
            _stream = stream;
        }

        public IAsyncEnumerator<ZipEntry> GetAsyncEnumerator(
            CancellationToken cancellationToken = default
        ) => new ZipEntryAsyncEnumerator(_headerFactory, _stream, cancellationToken);
    }

    /// <summary>
    /// Yields entries from streaming ZIP headers without requiring synchronous stream reads.
    /// </summary>
    private sealed class ZipEntryAsyncEnumerator : IAsyncEnumerator<ZipEntry>, IDisposable
    {
        private readonly Stream _stream;
        private readonly IAsyncEnumerator<ZipHeader> _headerEnumerator;
        private ZipEntry? _current;

        public ZipEntryAsyncEnumerator(
            StreamingZipHeaderFactory headerFactory,
            Stream stream,
            CancellationToken cancellationToken
        )
        {
            _stream = stream;
            _headerEnumerator = headerFactory
                .ReadStreamHeaderAsync(stream)
                .GetAsyncEnumerator(cancellationToken);
        }

        public ZipEntry Current =>
            _current ?? throw new InvalidOperationException("No current entry is available.");

        /// <summary>
        /// Advances to the next non-directory entry-relevant header and materializes a <see cref="ZipEntry"/>,
        /// using async I/O for improved performance on non-seekable streams.
        /// </summary>
        public async ValueTask<bool> MoveNextAsync()
        {
            while (await _headerEnumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var header = _headerEnumerator.Current;
                switch (header.ZipHeaderType)
                {
                    case ZipHeaderType.LocalEntry:
                        _current = new ZipEntry(
                            new StreamingZipFilePart((LocalEntryHeader)header, _stream)
                        );
                        return true;
                    case ZipHeaderType.DirectoryEntry:
                        // DirectoryEntry headers are intentionally skipped in streaming mode.
                        break;
                    case ZipHeaderType.DirectoryEnd:
                        _current = null;
                        return false;
                }
            }

            _current = null;
            return false;
        }

        /// <summary>
        /// Disposes the underlying header enumerator asynchronously.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        /// <summary>
        /// Synchronously disposes the underlying header enumerator.
        /// </summary>
        public void Dispose()
        {
            if (_headerEnumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
