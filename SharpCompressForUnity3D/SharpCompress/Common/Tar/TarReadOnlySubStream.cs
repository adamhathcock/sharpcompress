namespace SharpCompress.Common.Tar
{
    using SharpCompress;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class TarReadOnlySubStream : System.IO.Stream
    {
        [CompilerGenerated]
        private long _BytesLeftToRead_k__BackingField;
        [CompilerGenerated]
        private System.IO.Stream _Stream_k__BackingField;
        private long amountRead;
        private bool isDisposed;

        public TarReadOnlySubStream(System.IO.Stream stream, long bytesToRead)
        {
            this.Stream = stream;
            this.BytesLeftToRead = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                if (disposing)
                {
                    long num = this.amountRead % 0x200L;
                    if (num != 0L)
                    {
                        num = 0x200L - num;
                        if (num != 0L)
                        {
                            byte[] buffer = new byte[num];
                            Utility.ReadFully(this.Stream, buffer);
                        }
                    }
                }
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.BytesLeftToRead < count)
            {
                count = (int) this.BytesLeftToRead;
            }
            int num = this.Stream.Read(buffer, offset, count);
            if (num > 0)
            {
                this.BytesLeftToRead -= num;
                this.amountRead += num;
            }
            return num;
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
            throw new NotSupportedException();
        }

        private long BytesLeftToRead
        {
            [CompilerGenerated]
            get
            {
                return this._BytesLeftToRead_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._BytesLeftToRead_k__BackingField = value;
            }
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
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public System.IO.Stream Stream
        {
            [CompilerGenerated]
            get
            {
                return this._Stream_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._Stream_k__BackingField = value;
            }
        }
    }
}

