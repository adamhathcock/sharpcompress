using System.IO;

namespace SharpCompress.Common.Tar
{
    internal class TarReadOnlySubStream : Stream
    {
        private int amountRead;

        public TarReadOnlySubStream(Stream stream, long bytesToRead)
        {
            this.Stream = stream;
            this.BytesLeftToRead = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                int skipBytes = this.amountRead % 512;
                if (skipBytes == 0)
                {
                    return;
                }
                skipBytes = 512 - skipBytes;
                if (skipBytes == 0)
                {
                    return;
                }
                var buffer = new byte[skipBytes];
                this.Stream.ReadFully(buffer);
            }
        }

        private long BytesLeftToRead
        {
            get;
            set;
        }

        public Stream Stream
        {
            get;
            private set;
        }

        public override bool CanRead
        {
            get
            {
                return true;
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
                return false;
            }
        }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override long Length
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
                throw new System.NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.BytesLeftToRead < count)
            {
                count = (int)this.BytesLeftToRead;
            }
            int read = this.Stream.Read(buffer, offset, count);
            if (read > 0)
            {
                this.BytesLeftToRead -= read;
                this.amountRead += read;
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }
    }
}
