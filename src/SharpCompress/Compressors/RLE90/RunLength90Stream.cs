using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Compressors.RLE90
{
    /// <summary>
    /// Real-time streaming RLE90 decompression stream.
    /// Decompresses bytes on demand without buffering the entire file in memory.
    /// </summary>
    public class RunLength90Stream : Stream, IStreamStack
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

        private readonly Stream _stream;
        private readonly int _compressedSize;
        private int _bytesReadFromSource;

        private const byte DLE = 0x90;
        private bool _inDleMode;
        private byte _lastByte;
        private int _repeatCount;

        private bool _endOfCompressedData;

        public RunLength90Stream(Stream stream, int compressedSize)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _compressedSize = compressedSize;
#if DEBUG_STREAMS
            this.DebugConstruct(typeof(RunLength90Stream));
#endif
        }

        protected override void Dispose(bool disposing)
        {
#if DEBUG_STREAMS
            this.DebugDispose(typeof(RunLength90Stream));
#endif
            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            int bytesWritten = 0;

            while (bytesWritten < count && !_endOfCompressedData)
            {
                // Handle pending repeat bytes first
                if (_repeatCount > 0)
                {
                    int toWrite = Math.Min(_repeatCount, count - bytesWritten);
                    for (int i = 0; i < toWrite; i++)
                    {
                        buffer[offset + bytesWritten++] = _lastByte;
                    }
                    _repeatCount -= toWrite;
                    continue;
                }

                // Try to read the next byte from compressed data
                if (_bytesReadFromSource >= _compressedSize)
                {
                    _endOfCompressedData = true;
                    break;
                }

                int next = _stream.ReadByte();
                if (next == -1)
                {
                    _endOfCompressedData = true;
                    break;
                }

                _bytesReadFromSource++;
                byte c = (byte)next;

                if (_inDleMode)
                {
                    _inDleMode = false;

                    if (c == 0)
                    {
                        buffer[offset + bytesWritten++] = DLE;
                        _lastByte = DLE;
                    }
                    else
                    {
                        _repeatCount = c - 1;
                        // We’ll handle these repeats in next loop iteration.
                    }
                }
                else if (c == DLE)
                {
                    _inDleMode = true;
                }
                else
                {
                    buffer[offset + bytesWritten++] = c;
                    _lastByte = c;
                }
            }

            return bytesWritten;
        }
    }
}
