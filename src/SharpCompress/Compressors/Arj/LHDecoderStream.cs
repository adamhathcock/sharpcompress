using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Arj
{
    [CLSCompliant(true)]
    public sealed class LHDecoderStream : Stream, IStreamStack
    {
#if DEBUG_STREAMS
        long IStreamStack.InstanceId { get; set; }
#endif
        int IStreamStack.DefaultBufferSize { get; set; }

        Stream IStreamStack.BaseStream() => _stream;

        int IStreamStack.BufferSize
        {
            get => 0;
            set { }
        }
        int IStreamStack.BufferPosition
        {
            get => 0;
            set { }
        }

        void IStreamStack.SetPosition(long position) { }

        private readonly BitReader _bitReader;
        private readonly Stream _stream;

        // Buffer containing *all* bytes decoded so far.
        private readonly List<byte> _buffer = new();

        private long _readPosition;
        private readonly int _originalSize;
        private bool _finishedDecoding;
        private bool _disposed;

        private const int THRESHOLD = 3;

        public LHDecoderStream(Stream compressedStream, int originalSize)
        {
            _stream = compressedStream ?? throw new ArgumentNullException(nameof(compressedStream));
            if (!compressedStream.CanRead)
                throw new ArgumentException(
                    "compressedStream must be readable.",
                    nameof(compressedStream)
                );

            _bitReader = new BitReader(compressedStream);
            _originalSize = originalSize;
            _readPosition = 0;
            _finishedDecoding = (originalSize == 0);
        }

        public Stream BaseStream => _stream;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => _originalSize;

        public override long Position
        {
            get => _readPosition;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Decodes a single element (literal or back-reference) and appends it to _buffer.
        /// Returns true if data was added, or false if all input has already been decoded.
        /// </summary>
        private bool DecodeNext()
        {
            if (_buffer.Count >= _originalSize)
            {
                _finishedDecoding = true;
                return false;
            }

            int len = DecodeVal(0, 7);
            if (len == 0)
            {
                byte nextChar = (byte)_bitReader.ReadBits(8);
                _buffer.Add(nextChar);
            }
            else
            {
                int repCount = len + THRESHOLD - 1;
                int backPtr = DecodeVal(9, 13);

                if (backPtr >= _buffer.Count)
                    throw new InvalidDataException("Invalid back_ptr in LH stream");

                int srcIndex = _buffer.Count - 1 - backPtr;
                for (int j = 0; j < repCount && _buffer.Count < _originalSize; j++)
                {
                    byte b = _buffer[srcIndex];
                    _buffer.Add(b);
                    srcIndex++;
                    // srcIndex may grow; it's allowed (source region can overlap destination)
                }
            }

            if (_buffer.Count >= _originalSize)
            {
                _finishedDecoding = true;
            }

            return true;
        }

        private int DecodeVal(int from, int to)
        {
            int add = 0;
            int bit = from;

            while (bit < to && _bitReader.ReadBits(1) == 1)
            {
                add |= 1 << bit;
                bit++;
            }

            int res = bit > 0 ? _bitReader.ReadBits(bit) : 0;
            return res + add;
        }

        /// <summary>
        /// Reads decompressed bytes into buffer[offset..offset+count].
        /// The method decodes additional data on demand when needed.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LHDecoderStream));
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("offset/count");

            if (_readPosition >= _originalSize)
                return 0; // EOF

            int totalRead = 0;

            while (totalRead < count && _readPosition < _originalSize)
            {
                if (_readPosition >= _buffer.Count)
                {
                    bool had = DecodeNext();
                    if (!had)
                    {
                        break;
                    }
                }

                int available = _buffer.Count - (int)_readPosition;
                if (available <= 0)
                {
                    if (!_finishedDecoding)
                    {
                        continue;
                    }
                    break;
                }

                int toCopy = Math.Min(available, count - totalRead);
                _buffer.CopyTo((int)_readPosition, buffer, offset + totalRead, toCopy);

                _readPosition += toCopy;
                totalRead += toCopy;
            }

            return totalRead;
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
