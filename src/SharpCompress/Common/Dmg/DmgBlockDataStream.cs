using SharpCompress.Common.Dmg.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.ADC;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SharpCompress.Common.Dmg
{
    internal sealed class DmgBlockDataStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly DmgHeader _header;
        private readonly BlkxTable _table;
        private long _position;
        private bool _isEnded;
        private int _chunkIndex;
        private Stream? _chunkStream;
        private long _chunkPos;
        
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => true;
        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set
            {
                if ((value < 0) || (value > Length)) throw new ArgumentOutOfRangeException(nameof(value));

                if (value == Length)
                {
                    // End of the stream

                    _position = Length;
                    _isEnded = true;
                    _chunkIndex = -1;
                    _chunkStream = null;
                }
                else if (value != _position)
                {
                    _position = value;

                    // We can only seek over entire chunks at a time because some chunks may be compressed.
                    // So we first find the chunk that we are now in, then we read to the exact position inside that chunk.

                    for (int i = 0; i < _table.Chunks.Count; i++)
                    {
                        var chunk = _table.Chunks[i];
                        if (IsChunkValid(chunk) && (chunk.UncompressedOffset <= (ulong)_position)
                            && ((chunk.UncompressedOffset + chunk.UncompressedLength) > (ulong)_position))
                        {
                            if (i == _chunkIndex)
                            {
                                // We are still in the same chunk, so if the new position is
                                // behind the previous one we can just read to the new position.

                                long offset = (long)chunk.UncompressedOffset + _chunkPos;
                                if (offset <= _position)
                                {
                                    long skip = _position - offset;
                                    _chunkStream!.Skip(skip);
                                    _chunkPos += skip;
                                    break;
                                }
                            }

                            _chunkIndex = i;
                            _chunkStream = GetChunkStream();
                            _chunkPos = 0;

                            // If the chunk happens to not be compressed this read will still result in a fast seek
                            if ((ulong)_position != chunk.UncompressedOffset)
                            {
                                long skip = _position - (long)chunk.UncompressedOffset;
                                _chunkStream.Skip(skip);
                                _chunkPos = skip;
                            }

                            break;
                        }
                    }
                }
            }
        }

        public DmgBlockDataStream(Stream baseStream, DmgHeader header, BlkxTable table)
        {
            if (!baseStream.CanRead) throw new ArgumentException("Requires a readable stream", nameof(baseStream));
            if (!baseStream.CanSeek) throw new ArgumentException("Requires a seekable stream", nameof(baseStream));

            _baseStream = baseStream;
            _header = header;
            _table = table;

            Length = 0;
            foreach (var chunk in table.Chunks)
            {
                if (IsChunkValid(chunk))
                    Length += (long)chunk.UncompressedLength;
            }

            _position = 0;
            _chunkIndex = -1;
            _chunkIndex = GetNextChunk();
            _isEnded = _chunkIndex < 0;
            if (!_isEnded) _chunkStream = GetChunkStream();
            _chunkPos = 0;
        }

        private static bool IsChunkValid(BlkxChunk chunk)
        {
            return chunk.Type switch
            {
                BlkxChunkType.Zero => true,
                BlkxChunkType.Uncompressed => true,
                BlkxChunkType.Ignore => true,
                BlkxChunkType.AdcCompressed => true,
                BlkxChunkType.ZlibCompressed => true,
                BlkxChunkType.Bz2Compressed => true,
                _ => false
            };
        }

        private int GetNextChunk()
        {
            int index = _chunkIndex;
            bool isValid = false;
            while (!isValid)
            {
                index++;
                if (index >= _table.Chunks.Count) return -1;

                var chunk = _table.Chunks[index];
                if (chunk.Type == BlkxChunkType.Last) return -1;

                isValid = IsChunkValid(chunk);
            }
            return index;
        }

        private Stream GetChunkStream()
        {
            if (_chunkIndex < 0)
                throw new InvalidOperationException("Invalid chunk index");

            var chunk = _table.Chunks[_chunkIndex];

            // For our purposes, ignore behaves the same as zero
            if ((chunk.Type == BlkxChunkType.Zero) || (chunk.Type == BlkxChunkType.Ignore))
                return new ConstantStream(0, (long)chunk.UncompressedLength);

            // We first create a sub-stream on the region of the base stream where the
            // (possibly compressed) data is physically located at.
            var subStream = new SeekableSubStream(_baseStream,
                (long)(_header.DataForkOffset + _table.DataOffset + chunk.CompressedOffset),
                (long)chunk.CompressedLength);

            // Then we nest that sub-stream into the apropriate compressed stream.
            return chunk.Type switch
            {
                BlkxChunkType.Uncompressed => subStream,
                BlkxChunkType.AdcCompressed => new ADCStream(subStream, CompressionMode.Decompress),
                BlkxChunkType.ZlibCompressed => new ZlibStream(subStream, CompressionMode.Decompress),
                BlkxChunkType.Bz2Compressed => new BZip2Stream(subStream, CompressionMode.Decompress, false),
                _ => throw new InvalidOperationException("Invalid chunk type")
            };
        }

        // Decompresses the entire stream in memory for faster extraction.
        // This is about two orders of magnitude faster than decompressing
        // on-the-fly while extracting, but also eats RAM for breakfest.
        public Stream Decompress()
        {
            // We have to load all the chunks into separate memory streams first
            // because otherwise the decompression threads would block each other
            // and actually be slower than just a single decompression thread.

            var rawStreams = new Stream?[_table.Chunks.Count];
            for (int i = 0; i < rawStreams.Length; i++)
            {
                var chunk = _table.Chunks[i];
                if (IsChunkValid(chunk))
                {
                    if ((chunk.Type == BlkxChunkType.Zero) || (chunk.Type == BlkxChunkType.Ignore))
                    {
                        rawStreams[i] = new ConstantStream(0, (long)chunk.UncompressedLength);
                    }
                    else
                    {
                        var subStream = new SeekableSubStream(_baseStream,
                        (long)(_header.DataForkOffset + _table.DataOffset + chunk.CompressedOffset),
                        (long)chunk.CompressedLength);

                        var memStream = new MemoryStream();
                        subStream.CopyTo(memStream);
                        memStream.Position = 0;
                        rawStreams[i] = memStream;
                    }
                }
                else
                {
                    rawStreams[i] = null;
                }
            }

            // Now we can decompress the chunks multithreaded

            var streams = new Stream?[_table.Chunks.Count];
            Parallel.For(0, streams.Length, i =>
            {
                var rawStream = rawStreams[i];
                if (rawStream is not null)
                {
                    var chunk = _table.Chunks[i];
                    if ((chunk.Type == BlkxChunkType.Zero)
                     || (chunk.Type == BlkxChunkType.Ignore)
                     || (chunk.Type == BlkxChunkType.Uncompressed))
                    {
                        streams[i] = rawStream;
                    }
                    else
                    {
                        Stream compStream = chunk.Type switch
                        {
                            BlkxChunkType.AdcCompressed => new ADCStream(rawStream, CompressionMode.Decompress),
                            BlkxChunkType.ZlibCompressed => new ZlibStream(rawStream, CompressionMode.Decompress),
                            BlkxChunkType.Bz2Compressed => new BZip2Stream(rawStream, CompressionMode.Decompress, false),
                            _ => throw new InvalidOperationException("Invalid chunk type")
                        };

                        var memStream = new MemoryStream();
                        compStream.CopyTo(memStream);
                        compStream.Dispose();

                        memStream.Position = 0;
                        streams[i] = memStream;
                    }

                    rawStream.Dispose();
                    rawStreams[i] = null;
                }
                else
                {
                    streams[i] = null;
                }
            });

            return new CompositeStream((IEnumerable<Stream>)streams.Where(s => s is not null));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_isEnded) return 0;

            int readCount = _chunkStream!.Read(buffer, offset, count);
            _chunkPos += readCount;

            while (readCount < count)
            {
                // Current chunk has ended, so we have to continue reading from the next chunk.

                _chunkIndex = GetNextChunk();
                if (_chunkIndex < 0)
                {
                    // We have reached the last chunk

                    _isEnded = true;
                    _chunkPos = 0;
                    _position += readCount;
                    return readCount;
                }

                _chunkStream = GetChunkStream();
                int rc = _chunkStream.Read(buffer, offset + readCount, count - readCount);
                _chunkPos = rc;
                readCount += rc;
            }

            _position += readCount;
            return readCount;
        }

        public override void Flush()
        { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;

                case SeekOrigin.End:
                    Position = Length - offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        { }
    }
}
