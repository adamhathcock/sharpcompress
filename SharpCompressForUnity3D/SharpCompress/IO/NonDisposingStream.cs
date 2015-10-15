namespace SharpCompress.IO
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class NonDisposingStream : System.IO.Stream
    {
        [CompilerGenerated]
        private System.IO.Stream <Stream>k__BackingField;

        public NonDisposingStream(System.IO.Stream stream)
        {
            this.Stream = stream;
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override void Flush()
        {
            this.Stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.Stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.Stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Stream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return this.Stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this.Stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.Stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this.Stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this.Stream.Position;
            }
            set
            {
                this.Stream.Position = value;
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

