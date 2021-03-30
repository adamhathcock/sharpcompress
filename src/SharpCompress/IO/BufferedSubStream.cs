using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class BufferedSubStream : NonDisposingStream
    {
        private const int DEFAULT_BUFFER_SIZE = 32768;
        private readonly long startIndexInBaseStream;
        private readonly long endIndexInBaseStream;
        private long positionInBaseStream;
        private readonly long length;
        private readonly Stream baseStream;
        private bool canRead;
        private bool disposedValue;

        private readonly int bufferSize;
        private int bufferOffset;
        private int bufferLength;
        private byte[] buffer;

        public BufferedSubStream(Stream stream) : this(stream, stream.Position, stream.Length - stream.Position, DEFAULT_BUFFER_SIZE)
        {
        }

        public BufferedSubStream(Stream stream, int bufferSize) : this(stream, stream.Position, stream.Length - stream.Position, bufferSize)
        {
        }

        public BufferedSubStream(Stream stream, long startIndex, long length) : this(stream, startIndex, length, DEFAULT_BUFFER_SIZE)
        {
        }

        public BufferedSubStream(Stream stream, long startIndex, long length, int bufferSize) : base(stream, false)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (!stream.CanRead)
                throw new NotSupportedException("A stream that can be read is required");

            if (!stream.CanSeek)
                throw new NotSupportedException("A stream that supports seeking is required");

            this.endIndexInBaseStream = startIndex + length;
            if (this.endIndexInBaseStream > stream.Length)
                throw new ArgumentException("length");

            this.baseStream = stream;
            this.startIndexInBaseStream = startIndex;
            this.positionInBaseStream = startIndex;
            this.length = length;
            this.bufferOffset = 0;
            this.bufferLength = 0;
            this.bufferSize = bufferSize <= 4096 ? DEFAULT_BUFFER_SIZE : bufferSize;
            this.canRead = true;
            this.disposedValue = false;
        }

        public Stream BaseStream
        {
            get
            {
                ThrowIfDisposed();
                return this.baseStream;
            }
        }

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return this.length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return positionInBaseStream - startIndexInBaseStream - bufferLength + bufferOffset;
            }
            set
            {
                ThrowIfDisposed();
                if (value == Position)
                    return;
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override bool CanRead => canRead && baseStream.CanRead;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        private void ThrowIfDisposed()
        {
            if (disposedValue)
                throw new ObjectDisposedException(GetType().ToString());
        }

        private void ThrowIfCantRead()
        {
            if (!CanRead)
                throw new NotSupportedException("This stream does not support reading");
        }

        public override int Read(byte[] array, int offset, int count)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "offset < 0");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "count < 0");
            if (array.Length - offset < count)
                throw new ArgumentException("The size of the array is not enough.");

            ThrowIfDisposed();
            ThrowIfCantRead();

            if (count == 0)
                return 0;

            int bytesFromBuffer = ReadFromBuffer(array, offset, count);

            if (bytesFromBuffer == count)
                return bytesFromBuffer;

            int alreadySatisfied = bytesFromBuffer;
            if (bytesFromBuffer > 0)
            {
                count -= bytesFromBuffer;
                offset += bytesFromBuffer;
            }

            bufferOffset = bufferLength = 0;
            lock (baseStream)
            {
                long remaining = endIndexInBaseStream - positionInBaseStream;
                if (remaining <= 0)
                    return 0;

                if (count > remaining)
                    count = (int)remaining;

                if (baseStream.Position != positionInBaseStream)
                    baseStream.Seek(positionInBaseStream, SeekOrigin.Begin);

                if (count >= this.bufferSize)
                {
                    int read = baseStream.Read(array, offset, count);
                    this.positionInBaseStream += read;
                    return read + alreadySatisfied;
                }

                if (buffer == null)
                    buffer = new byte[bufferSize];

                bufferLength = baseStream.Read(buffer, 0, (int)Math.Min(remaining, bufferSize));
                if (bufferLength < 0)
                    throw new EndOfStreamException();
                this.positionInBaseStream += bufferLength;
            }

            bytesFromBuffer = ReadFromBuffer(array, offset, count);
            return bytesFromBuffer + alreadySatisfied;
        }

        private int ReadFromBuffer(byte[] array, int offset, int count)
        {
            int readBytes = bufferLength - bufferOffset;
            if (readBytes == 0)
                return 0;

            if (readBytes > count)
                readBytes = count;

            Buffer.BlockCopy(buffer, bufferOffset, array, offset, readBytes);
            bufferOffset += readBytes;
            return readBytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            long newPos = 0;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPos = offset;
                    break;
                case SeekOrigin.Current:
                    newPos = this.Position + offset;
                    break;
                case SeekOrigin.End:
                    newPos = this.length - offset;
                    break;
            }

            long newPosInBaseStream = startIndexInBaseStream + newPos;
            if (positionInBaseStream - bufferLength <= newPosInBaseStream && newPosInBaseStream <= positionInBaseStream)
            {
                bufferOffset = (int)(newPosInBaseStream - (positionInBaseStream - bufferLength));
            }
            else
            {
                bufferLength = 0;
                bufferOffset = 0;
                this.positionInBaseStream = newPosInBaseStream;
            }

            return newPos;
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            ThrowIfDisposed();
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposedValue)
            {
                canRead = false;
                disposedValue = true;
                buffer = null;
                bufferLength = 0;
                bufferOffset = 0;
            }
            base.Dispose(disposing);
        }
    }
}