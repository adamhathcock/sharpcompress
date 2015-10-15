namespace SharpCompress.IO
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class ReadOnlySubStream : System.IO.Stream
    {
        [CompilerGenerated]
        private long <BytesLeftToRead>k__BackingField;
        [CompilerGenerated]
        private System.IO.Stream <Stream>k__BackingField;

        public ReadOnlySubStream(System.IO.Stream stream, long bytesToRead) : this(stream, null, bytesToRead)
        {
        }

        public ReadOnlySubStream(System.IO.Stream stream, long? origin, long bytesToRead)
        {
            this.Stream = stream;
            if (origin.HasValue)
            {
                stream.Position = origin.Value;
            }
            this.BytesLeftToRead = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
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
                return this.<BytesLeftToRead>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<BytesLeftToRead>k__BackingField = value;
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
                return this.<Stream>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Stream>k__BackingField = value;
            }
        }
    }
}

