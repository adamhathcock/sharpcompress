namespace SharpCompress.Common
{
    using SharpCompress.Reader;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public class EntryStream : Stream
    {
        [CompilerGenerated]
        private IReader <Reader>k__BackingField;
        private bool completed;
        private bool isDisposed;
        private Stream stream;

        internal EntryStream(IReader reader, Stream stream)
        {
            this.Reader = reader;
            this.stream = stream;
        }

        protected override void Dispose(bool disposing)
        {
            if (!(this.completed || this.Reader.Cancelled))
            {
                this.SkipEntry();
            }
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                this.stream.Dispose();
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num = this.stream.Read(buffer, offset, count);
            if (num <= 0)
            {
                this.completed = true;
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

        public void SkipEntry()
        {
            byte[] buffer = new byte[0x1000];
            while (this.Read(buffer, 0, buffer.Length) > 0)
            {
            }
            this.completed = true;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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

        public IReader Reader
        {
            [CompilerGenerated]
            get
            {
                return this.<Reader>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Reader>k__BackingField = value;
            }
        }
    }
}

