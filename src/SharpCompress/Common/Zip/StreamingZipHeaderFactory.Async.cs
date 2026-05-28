using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal sealed partial class StreamingZipHeaderFactory
{
    /// <summary>
    /// Reads ZIP headers asynchronously for streams that do not support synchronous reads.
    /// </summary>
    internal IAsyncEnumerable<ZipHeader> ReadStreamHeaderAsync(Stream stream) =>
        new StreamHeaderAsyncEnumerable(this, stream);

    /// <summary>
    /// Invokes the shared async header parsing logic on the base factory.
    /// </summary>
    private ValueTask<ZipHeader?> ReadHeaderAsyncInternal(
        uint headerBytes,
        AsyncBinaryReader reader
    ) => ReadHeader(headerBytes, reader);

    /// <summary>
    /// Exposes the last parsed local entry header to the async enumerator so it can handle streaming data descriptors.
    /// </summary>
    private LocalEntryHeader? LastEntryHeader
    {
        get => _lastEntryHeader;
        set => _lastEntryHeader = value;
    }

    /// <summary>
    /// Produces an async enumerator for streaming ZIP headers.
    /// </summary>
    private sealed class StreamHeaderAsyncEnumerable : IAsyncEnumerable<ZipHeader>
    {
        private readonly StreamingZipHeaderFactory _headerFactory;
        private readonly Stream _stream;

        public StreamHeaderAsyncEnumerable(StreamingZipHeaderFactory headerFactory, Stream stream)
        {
            _headerFactory = headerFactory;
            _stream = stream;
        }

        public IAsyncEnumerator<ZipHeader> GetAsyncEnumerator(
            CancellationToken cancellationToken = default
        ) => new StreamHeaderAsyncEnumerator(_headerFactory, _stream, cancellationToken);
    }

    /// <summary>
    /// Async implementation of reading stream headers using <see cref="AsyncBinaryReader"/> to avoid sync reads.
    /// </summary>
    private sealed class StreamHeaderAsyncEnumerator : IAsyncEnumerator<ZipHeader>, IDisposable
    {
        private readonly StreamingZipHeaderFactory _headerFactory;
        private readonly SharpCompressStream _sharpCompressStream;
        private readonly AsyncBinaryReader _reader;
        private readonly CancellationToken _cancellationToken;
        private bool _completed;

        public StreamHeaderAsyncEnumerator(
            StreamingZipHeaderFactory headerFactory,
            Stream stream,
            CancellationToken cancellationToken
        )
        {
            _headerFactory = headerFactory;
            // Use Create to avoid double-wrapping if stream is already a SharpCompressStream,
            // and to preserve seekability for DataDescriptorStream which needs to seek backward
            _sharpCompressStream = SharpCompressStream.Create(stream);
            _reader = new AsyncBinaryReader(_sharpCompressStream, leaveOpen: true);
            _cancellationToken = cancellationToken;
        }

        private ZipHeader? _current;

        public ZipHeader Current =>
            _current ?? throw new ArchiveOperationException("No current header is available.");

        /// <summary>
        /// Advances to the next ZIP header in the stream, honoring streaming data descriptors where applicable.
        /// </summary>
        public async ValueTask<bool> MoveNextAsync()
        {
            if (_completed)
            {
                return false;
            }

            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                uint headerBytes;
                var lastEntryHeader = _headerFactory.LastEntryHeader;
                if (
                    lastEntryHeader != null
                    && FlagUtility.HasFlag(lastEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor)
                )
                {
                    if (lastEntryHeader.Part is null)
                    {
                        continue;
                    }

                    var pos = _sharpCompressStream.CanSeek
                        ? (long?)_sharpCompressStream.Position
                        : null;

                    var crc = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);
                    if (crc == POST_DATA_DESCRIPTOR)
                    {
                        crc = await _reader
                            .ReadUInt32Async(_cancellationToken)
                            .ConfigureAwait(false);
                    }
                    lastEntryHeader.Crc = crc;

                    //attempt 32bit read
                    ulong compressedSize = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);
                    ulong uncompressedSize = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);
                    headerBytes = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);

                    //check for zip64 sentinel or unexpected header
                    bool isSentinel =
                        compressedSize == 0xFFFFFFFF || uncompressedSize == 0xFFFFFFFF;
                    bool isHeader = headerBytes == 0x04034b50 || headerBytes == 0x02014b50;

                    if (!isHeader && !isSentinel)
                    {
                        //reshuffle into 64-bit values
                        compressedSize = (uncompressedSize << 32) | compressedSize;
                        uncompressedSize =
                            ((ulong)headerBytes << 32)
                            | await _reader
                                .ReadUInt32Async(_cancellationToken)
                                .ConfigureAwait(false);
                        headerBytes = await _reader
                            .ReadUInt32Async(_cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else if (isSentinel)
                    {
                        //standards-compliant zip64 descriptor
                        compressedSize = await _reader
                            .ReadUInt64Async(_cancellationToken)
                            .ConfigureAwait(false);
                        uncompressedSize = await _reader
                            .ReadUInt64Async(_cancellationToken)
                            .ConfigureAwait(false);
                    }

                    lastEntryHeader.CompressedSize = (long)compressedSize;
                    lastEntryHeader.UncompressedSize = (long)uncompressedSize;

                    if (pos.HasValue)
                    {
                        lastEntryHeader.DataStartPosition = pos - lastEntryHeader.CompressedSize;
                    }
                }
                else if (lastEntryHeader != null && lastEntryHeader.IsZip64)
                {
                    if (lastEntryHeader.Part is null)
                    {
                        continue;
                    }

                    var pos = _sharpCompressStream.CanSeek
                        ? (long?)_sharpCompressStream.Position
                        : null;

                    headerBytes = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);

                    _ = await _reader.ReadUInt16Async(_cancellationToken).ConfigureAwait(false); // version
                    _ = await _reader.ReadUInt16Async(_cancellationToken).ConfigureAwait(false); // flags
                    _ = await _reader.ReadUInt16Async(_cancellationToken).ConfigureAwait(false); // compressionMethod
                    _ = await _reader.ReadUInt16Async(_cancellationToken).ConfigureAwait(false); // lastModifiedDate
                    _ = await _reader.ReadUInt16Async(_cancellationToken).ConfigureAwait(false); // lastModifiedTime

                    var crc = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);

                    if (crc == POST_DATA_DESCRIPTOR)
                    {
                        crc = await _reader
                            .ReadUInt32Async(_cancellationToken)
                            .ConfigureAwait(false);
                    }
                    lastEntryHeader.Crc = crc;

                    // The DataDescriptor can be either 64bit or 32bit
                    var compressedSize = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);
                    var uncompressedSize = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);

                    // Check if we have header or 64bit DataDescriptor
                    var testHeader = !(headerBytes == 0x04034b50 || headerBytes == 0x02014b50);

                    var test64Bit = ((long)uncompressedSize << 32) | compressedSize;
                    if (test64Bit == lastEntryHeader.CompressedSize && testHeader)
                    {
                        lastEntryHeader.UncompressedSize =
                            (
                                (long)
                                    await _reader
                                        .ReadUInt32Async(_cancellationToken)
                                        .ConfigureAwait(false) << 32
                            ) | headerBytes;
                        headerBytes = await _reader
                            .ReadUInt32Async(_cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        lastEntryHeader.UncompressedSize = uncompressedSize;
                    }

                    if (pos.HasValue)
                    {
                        lastEntryHeader.DataStartPosition = pos - lastEntryHeader.CompressedSize;

                        // For SeekableSharpCompressStream, seek back to just after the local header signature.
                        // Plain SharpCompressStream cannot seek to arbitrary positions, so we skip this.
                        // 4 = First 4 bytes of the entry header (i.e. 50 4B 03 04)
                        if (_sharpCompressStream is SeekableSharpCompressStream)
                        {
                            _sharpCompressStream.Position = pos.Value + 4;
                        }
                    }
                }
                else
                {
                    headerBytes = await _reader
                        .ReadUInt32Async(_cancellationToken)
                        .ConfigureAwait(false);
                }

                _headerFactory.LastEntryHeader = null;
                var header = await _headerFactory
                    .ReadHeaderAsyncInternal(headerBytes, _reader)
                    .ConfigureAwait(false);
                if (header is null)
                {
                    _completed = true;
                    return false;
                }

                //entry could be zero bytes so we need to know that.
                if (header.ZipHeaderType == ZipHeaderType.LocalEntry)
                {
                    var localHeader = (LocalEntryHeader)header;
                    var directoryHeader = _headerFactory._entries?.FirstOrDefault(entry =>
                        entry.Key == localHeader.Name
                        && localHeader.CompressedSize == 0
                        && localHeader.UncompressedSize == 0
                        && localHeader.Crc == 0
                        && localHeader.IsDirectory == false
                    );

                    if (directoryHeader != null)
                    {
                        localHeader.UncompressedSize = directoryHeader.Size;
                        localHeader.CompressedSize = directoryHeader.CompressedSize;
                        localHeader.Crc = (uint)directoryHeader.Crc;
                    }

                    // If we have CompressedSize, there is data to be read
                    if (localHeader.CompressedSize > 0)
                    {
                        header.HasData = true;
                    } // Check if zip is streaming ( Length is 0 and is declared in PostDataDescriptor )
                    else if (localHeader.Flags.HasFlag(HeaderFlags.UsePostDataDescriptor))
                    {
                        // Peek ahead to check if next data is a header or file data.
                        // Use the IStreamStack.Rewind mechanism to give back the peeked bytes.
                        var nextHeaderBytes = await _reader
                            .ReadUInt32Async(_cancellationToken)
                            .ConfigureAwait(false);
                        _sharpCompressStream.Rewind(sizeof(uint));

                        // Check if next data is PostDataDescriptor, streamed file with 0 length
                        header.HasData = !IsHeader(nextHeaderBytes);
                    }
                    else // We are not streaming and compressed size is 0, we have no data
                    {
                        header.HasData = false;
                    }
                }

                _current = header;
                return true;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }

        /// <summary>
        /// Disposes the underlying reader (without closing the archive stream).
        /// </summary>
        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
