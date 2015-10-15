namespace SharpCompress.Compressor.Filters
{
    using System;
    using System.IO;

    internal abstract class Filter : Stream
    {
        protected Stream baseStream;
        private bool endReached = false;
        private bool isDisposed;
        protected bool isEncoder;
        private int read = 0;
        private byte[] tail;
        private int transformed = 0;
        private byte[] window;

        protected Filter(bool isEncoder, Stream baseStream, int lookahead)
        {
            this.isEncoder = isEncoder;
            this.baseStream = baseStream;
            this.tail = new byte[lookahead - 1];
            this.window = new byte[this.tail.Length * 2];
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                this.baseStream.Dispose();
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num = 0;
            if (this.transformed > 0)
            {
                int transformed = this.transformed;
                if (transformed > count)
                {
                    transformed = count;
                }
                Buffer.BlockCopy(this.tail, 0, buffer, offset, transformed);
                this.transformed -= transformed;
                this.read -= transformed;
                offset += transformed;
                count -= transformed;
                num += transformed;
                Buffer.BlockCopy(this.tail, transformed, this.tail, 0, this.read);
            }
            if (count != 0)
            {
                int num4;
                int read = this.read;
                if (read > count)
                {
                    read = count;
                }
                Buffer.BlockCopy(this.tail, 0, buffer, offset, read);
                this.read -= read;
                Buffer.BlockCopy(this.tail, read, this.tail, 0, this.read);
                while (!this.endReached && (read < count))
                {
                    num4 = this.baseStream.Read(buffer, offset + read, count - read);
                    read += num4;
                    if (num4 == 0)
                    {
                        this.endReached = true;
                    }
                }
                while (!this.endReached && (this.read < this.tail.Length))
                {
                    num4 = this.baseStream.Read(this.tail, this.read, this.tail.Length - this.read);
                    this.read += num4;
                    if (num4 == 0)
                    {
                        this.endReached = true;
                    }
                }
                if (read > this.tail.Length)
                {
                    this.transformed = this.Transform(buffer, offset, read);
                    offset += this.transformed;
                    count -= this.transformed;
                    num += this.transformed;
                    read -= this.transformed;
                    this.transformed = 0;
                }
                if (count == 0)
                {
                    return num;
                }
                Buffer.BlockCopy(buffer, offset, this.window, 0, read);
                Buffer.BlockCopy(this.tail, 0, this.window, read, this.read);
                if ((read + this.read) > this.tail.Length)
                {
                    this.transformed = this.Transform(this.window, 0, read + this.read);
                }
                else
                {
                    this.transformed = read + this.read;
                }
                Buffer.BlockCopy(this.window, 0, buffer, offset, read);
                Buffer.BlockCopy(this.window, read, this.tail, 0, this.read);
                num += read;
                this.transformed -= read;
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

        protected abstract int Transform(byte[] buffer, int offset, int count);
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Transform(buffer, offset, count);
            this.baseStream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return !this.isEncoder;
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
                return this.isEncoder;
            }
        }

        public override long Length
        {
            get
            {
                return this.baseStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this.baseStream.Position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}

