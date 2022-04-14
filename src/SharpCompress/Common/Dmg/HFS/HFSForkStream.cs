using SharpCompress.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpCompress.Common.Dmg.HFS
{
    internal sealed class HFSForkStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly HFSVolumeHeader _volumeHeader;
        private readonly IReadOnlyList<HFSExtentDescriptor> _extents;
        private long _position;
        private bool _isEnded;
        private int _extentIndex;
        private Stream? _extentStream;

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
                    _extentIndex = -1;
                    _extentStream = null;
                }
                else if (value != _position)
                {
                    _position = value;

                    // We first have to determine in which extent we are now, then we seek to the exact position in that extent.

                    long offsetInExtent = _position;
                    for (int i = 0; i < _extents.Count; i++)
                    {
                        var extent = _extents[i];
                        long extentSize = extent.BlockCount * _volumeHeader.BlockSize;
                        if (extentSize < offsetInExtent)
                        {
                            if (i == _extentIndex)
                            {
                                // We are in the same extent so just seek to the correct position
                                _extentStream!.Position = offsetInExtent;
                            }
                            else
                            {
                                _extentIndex = i;
                                _extentStream = GetExtentStream();
                                _extentStream.Position = offsetInExtent;
                            }

                            break;
                        }
                        else
                        {
                            offsetInExtent -= extentSize;
                        }
                    }
                }
            }
        }

        public HFSForkStream(Stream baseStream, HFSVolumeHeader volumeHeader, HFSForkData forkData)
        {
            _baseStream = baseStream;
            _volumeHeader = volumeHeader;
            _extents = forkData.Extents;
            Length = (long)forkData.LogicalSize;

            _position = 0;
            _extentIndex = -1;
            _extentIndex = GetNextExtent();
            _isEnded = _extentIndex < 0;
            if (!_isEnded) _extentStream = GetExtentStream();
        }

        public HFSForkStream(
            Stream baseStream, HFSVolumeHeader volumeHeader, HFSForkData forkData, uint fileId,
            IReadOnlyDictionary<HFSExtentKey, HFSExtentRecord> extents)
        {
            _baseStream = baseStream;
            _volumeHeader = volumeHeader;
            Length = (long)forkData.LogicalSize;

            uint blocks = (uint)forkData.Extents.Sum(e => e.BlockCount);
            var totalExtents = new List<HFSExtentDescriptor>(forkData.Extents);
            _extents = totalExtents;

            var nextKey = new HFSExtentKey(0, fileId, blocks);
            while (extents.TryGetValue(nextKey, out var record))
            {
                blocks += (uint)record.Extents.Sum(e => e.BlockCount);
                totalExtents.AddRange(record.Extents);

                nextKey = new HFSExtentKey(0, fileId, blocks);
            }

            _position = 0;
            _extentIndex = -1;
            _extentIndex = GetNextExtent();
            _isEnded = _extentIndex < 0;
            if (!_isEnded) _extentStream = GetExtentStream();
        }

        private int GetNextExtent()
        {
            int index = _extentIndex + 1;
            if (index >= _extents.Count) return -1;

            var extent = _extents[index];
            if ((extent.StartBlock == 0) && (extent.BlockCount == 0)) return -1;
            return index;
        }

        private Stream GetExtentStream()
        {
            if (_extentIndex < 0)
                throw new InvalidOperationException("Invalid extent index");

            var extent = _extents[_extentIndex];
            return new HFSExtentStream(_baseStream, _volumeHeader, extent);
        }

        public override void Flush()
        { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_isEnded) return 0;

            count = (int)Math.Min(count, Length - Position);
            int readCount = _extentStream!.Read(buffer, offset, count);
            while (readCount < count)
            {
                _extentIndex = GetNextExtent();
                if (_extentIndex < 0)
                {
                    _isEnded = true;
                    return readCount;
                }

                _extentStream = GetExtentStream();
                readCount += _extentStream.Read(buffer, offset + readCount, count - readCount);
            }

            _position += readCount;
            return readCount;
        }

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

        private sealed class HFSExtentStream : SeekableSubStream
        {
            public HFSExtentStream(Stream stream, HFSVolumeHeader volumeHeader, HFSExtentDescriptor extent)
                : base(stream, (long)extent.StartBlock * volumeHeader.BlockSize, (long)extent.BlockCount * volumeHeader.BlockSize)
            { }
        }
    }
}
