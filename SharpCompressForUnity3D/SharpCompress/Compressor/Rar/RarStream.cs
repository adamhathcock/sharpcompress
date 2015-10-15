namespace SharpCompress.Compressor.Rar
{
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.IO;

    internal class RarStream : Stream
    {
        private bool fetch = false;
        private readonly FileHeader fileHeader;
        private bool isDisposed;
        private byte[] outBuffer;
        private int outCount = 0;
        private int outOffset;
        private int outTotal;
        private readonly Stream readStream;
        private byte[] tmpBuffer = new byte[0x10000];
        private int tmpCount = 0;
        private int tmpOffset = 0;
        private readonly Unpack unpack;

        public RarStream(Unpack unpack, FileHeader fileHeader, Stream readStream)
        {
            this.unpack = unpack;
            this.fileHeader = fileHeader;
            this.readStream = readStream;
            this.fetch = true;
            unpack.doUnpack(fileHeader, readStream, this);
            this.fetch = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                this.readStream.Dispose();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.outTotal = 0;
            if (this.tmpCount > 0)
            {
                int num = (this.tmpCount < count) ? this.tmpCount : count;
                Buffer.BlockCopy(this.tmpBuffer, this.tmpOffset, buffer, offset, num);
                this.tmpOffset += num;
                this.tmpCount -= num;
                offset += num;
                count -= num;
                this.outTotal += num;
            }
            if ((count > 0) && (this.unpack.DestSize > 0L))
            {
                this.outBuffer = buffer;
                this.outOffset = offset;
                this.outCount = count;
                this.fetch = true;
                this.unpack.doUnpack();
                this.fetch = false;
            }
            return this.outTotal;
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
            if (!this.fetch)
            {
                throw new NotSupportedException();
            }
            if (this.outCount > 0)
            {
                int num = (this.outCount < count) ? this.outCount : count;
                Buffer.BlockCopy(buffer, offset, this.outBuffer, this.outOffset, num);
                this.outOffset += num;
                this.outCount -= num;
                offset += num;
                count -= num;
                this.outTotal += num;
            }
            if (count > 0)
            {
                if (this.tmpBuffer.Length < (this.tmpCount + count))
                {
                    byte[] dst = new byte[((this.tmpBuffer.Length * 2) > (this.tmpCount + count)) ? (this.tmpBuffer.Length * 2) : (this.tmpCount + count)];
                    Buffer.BlockCopy(this.tmpBuffer, 0, dst, 0, this.tmpCount);
                    this.tmpBuffer = dst;
                }
                Buffer.BlockCopy(buffer, offset, this.tmpBuffer, this.tmpCount, count);
                this.tmpCount += count;
                this.tmpOffset = 0;
                this.unpack.Suspended = true;
            }
            else
            {
                this.unpack.Suspended = false;
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
                return this.fileHeader.UncompressedSize;
            }
        }

        public override long Position
        {
            get
            {
                return (this.fileHeader.UncompressedSize - this.unpack.DestSize);
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}

