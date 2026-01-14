using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Readers.Zip;

public partial class ZipReader : AbstractReader<ZipEntry, ZipVolume>
{
    private readonly StreamingZipHeaderFactory _headerFactory;

    private ZipReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.Zip)
    {
        Volume = new ZipVolume(stream, options);
        _headerFactory = new StreamingZipHeaderFactory(
            options.Password,
            options.ArchiveEncoding,
            null
        );
    }

    private ZipReader(Stream stream, ReaderOptions options, IEnumerable<ZipEntry> entries)
        : base(options, ArchiveType.Zip)
    {
        Volume = new ZipVolume(stream, options);
        _headerFactory = new StreamingZipHeaderFactory(
            options.Password,
            options.ArchiveEncoding,
            entries
        );
    }

    public override ZipVolume Volume { get; }

    #region Open

    /// <summary>
    /// Opens a ZipReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader Open(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, options ?? new ReaderOptions());
    }

    public static IReader Open(Stream stream, ReaderOptions? options, IEnumerable<ZipEntry> entries)
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, options ?? new ReaderOptions(), entries);
    }

    #endregion Open

    protected override IEnumerable<ZipEntry> GetEntries(Stream stream)
    {
        foreach (var h in _headerFactory.ReadStreamHeader(stream))
        {
            if (h != null)
            {
                switch (h.ZipHeaderType)
                {
                    case ZipHeaderType.LocalEntry:
                        {
                            yield return new ZipEntry(
                                new StreamingZipFilePart((LocalEntryHeader)h, stream)
                            );
                        }
                        break;
                    case ZipHeaderType.DirectoryEntry:
                        // DirectoryEntry headers in the central directory are intentionally skipped.
                        // In streaming mode, we can only read forward, and DirectoryEntry headers
                        // reference LocalEntry headers that have already been processed. The file
                        // data comes from LocalEntry headers, not DirectoryEntry headers.
                        // For multi-volume ZIPs where file data spans multiple files, use ZipArchive
                        // instead, which requires seekable streams.
                        break;
                    case ZipHeaderType.DirectoryEnd:
                    {
                        yield break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns entries asynchronously for streams that only support async reads.
    /// </summary>
    protected override IAsyncEnumerable<ZipEntry> GetEntriesAsync(Stream stream) =>
        new ZipEntryAsyncEnumerable(_headerFactory, stream);

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
        /// Advances to the next non-directory entry-relevant header and materializes a <see cref="ZipEntry"/>.
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
        /// Disposes the underlying header enumerator.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        /// <summary>
        /// Disposes the underlying header enumerator.
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
