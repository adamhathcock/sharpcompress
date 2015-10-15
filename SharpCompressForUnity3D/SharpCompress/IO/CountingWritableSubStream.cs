namespace SharpCompress.IO
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class CountingWritableSubStream : Stream
    {
        [CompilerGenerated]
        private uint <Count>k__BackingField;
        private Stream writableStream;

        internal CountingWritableSubStream(Stream stream)
        {
            this.writableStream = stream;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            this.writableStream.Write(buffer, offset, count);
            this.Count += (uint) count;
        }

        public override bool CanRead
        {
            get
            {
                return false;
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
                return true;
            }
        }

        public uint Count
        {
            [CompilerGenerated]
            get
            {
                return this.<Count>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Count>k__BackingField = value;
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
    }
}

