using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class ReadOnlySubStream : NonDisposingStream
    {
        private readonly long startIndexInBaseStream;
        private readonly long length;
        private readonly Stream baseStream;
        private long position;
        private bool canRead;
        private bool canSeek;
        private bool disposedValue;

        public ReadOnlySubStream(Stream stream, long length) : this(stream, null, length)
        {
        }

        public ReadOnlySubStream(Stream stream, long? startIndex, long length) : base(stream, false)
        {
            if (stream == null)
                throw new ArgumentNullException("the stream is null");

            if (!stream.CanRead)
                throw new NotSupportedException("A stream that can be read is required");

            if (startIndex != null)
                stream.Position = startIndex.Value;

            this.canSeek = stream.CanSeek;
            if (this.canSeek)
                this.startIndexInBaseStream = stream.Position;
            else
                this.startIndexInBaseStream = 0;

            this.position = 0;
            this.baseStream = stream;
            this.length = length;
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
                return position;
            }
            set
            {
                ThrowIfDisposed();
                if (!canSeek)
                    throw new NotSupportedException();

                if (value == position)
                    return;

                this.position = value;
            }
        }

        public override bool CanRead => canRead && baseStream.CanRead;

        public override bool CanSeek => canSeek;

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

            lock (baseStream)
            {
                long remaining = length - position;
                if (remaining <= 0)
                    return 0;

                if (count > remaining)
                    count = (int)remaining;

                if (canSeek && baseStream.Position != startIndexInBaseStream + position)
                    baseStream.Seek(startIndexInBaseStream + position, SeekOrigin.Begin);

                int read = baseStream.Read(array, offset, count);
                position += read;
                return read;
            }
        }

        public override int ReadByte()
        {
            long remaining = length - position;
            if (remaining <= 0)
                return -1;

            int value = baseStream.ReadByte();
            if (value != -1)
                position += 1;

            return value;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();

            if (!canSeek)
                throw new NotSupportedException();

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

            this.position = newPos;
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
            }
            base.Dispose(disposing);
        }
    }
}