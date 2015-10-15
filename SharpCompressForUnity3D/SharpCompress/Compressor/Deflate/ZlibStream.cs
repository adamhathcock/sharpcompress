namespace SharpCompress.Compressor.Deflate
{
    using SharpCompress.Compressor;
    using System;
    using System.IO;

    public class ZlibStream : Stream
    {
        private readonly ZlibBaseStream _baseStream;
        private bool _disposed;

        public ZlibStream(Stream stream, CompressionMode mode) : this(stream, mode, CompressionLevel.Default, false)
        {
        }

        public ZlibStream(Stream stream, CompressionMode mode, CompressionLevel level) : this(stream, mode, level, false)
        {
        }

        public ZlibStream(Stream stream, CompressionMode mode, bool leaveOpen) : this(stream, mode, CompressionLevel.Default, leaveOpen)
        {
        }

        public ZlibStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            this._baseStream = new ZlibBaseStream(stream, mode, level, ZlibStreamFlavor.ZLIB, leaveOpen);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!this._disposed)
                {
                    if (disposing && (this._baseStream != null))
                    {
                        this._baseStream.Dispose();
                    }
                    this._disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override void Flush()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("ZlibStream");
            }
            this._baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("ZlibStream");
            }
            return this._baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("ZlibStream");
            }
            this._baseStream.Write(buffer, offset, count);
        }

        public int BufferSize
        {
            get
            {
                return this._baseStream._bufferSize;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                if (this._baseStream._workingBuffer != null)
                {
                    throw new ZlibException("The working buffer is already set.");
                }
                if (value < 0x400)
                {
                    throw new ZlibException(string.Format("Don't be silly. {0} bytes?? Use a bigger buffer, at least {1}.", value, 0x400));
                }
                this._baseStream._bufferSize = value;
            }
        }

        public override bool CanRead
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                return this._baseStream._stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                return this._baseStream._stream.CanWrite;
            }
        }

        public virtual FlushType FlushMode
        {
            get
            {
                return this._baseStream._flushMode;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                this._baseStream._flushMode = value;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Writer)
                {
                    return this._baseStream._z.TotalBytesOut;
                }
                if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Reader)
                {
                    return this._baseStream._z.TotalBytesIn;
                }
                return 0L;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public virtual long TotalIn
        {
            get
            {
                return this._baseStream._z.TotalBytesIn;
            }
        }

        public virtual long TotalOut
        {
            get
            {
                return this._baseStream._z.TotalBytesOut;
            }
        }
    }
}

