using System;
using System.IO;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressor.Rar
{
    internal class RarStream : Stream
    {
        private readonly Unpack unpack;
        private readonly FileHeader fileHeader;
        private readonly Stream readStream;

        private bool fetch = false;

        private byte[] tmpBuffer = new byte[65536];
        private int tmpOffset = 0;
        private int tmpCount = 0;

        private byte[] outBuffer;
        private int outOffset;
        private int outCount = 0;
        private int outTotal;
        private bool isDisposed;

        public RarStream(Unpack unpack, FileHeader fileHeader, Stream readStream)
        {
            this.unpack = unpack;
            this.fileHeader = fileHeader;
            this.readStream = readStream;
            fetch = true;
            unpack.doUnpack(fileHeader, readStream, this);
            fetch = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            base.Dispose(disposing);
            readStream.Dispose();
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
        }

        public override long Length
        {
            get { return fileHeader.UncompressedSize; }
        }

        public override long Position
        {
            get { return fileHeader.UncompressedSize - unpack.DestSize; }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            outTotal = 0;
            if (tmpCount > 0)
            {
                int toCopy = tmpCount < count ? tmpCount : count;
                Buffer.BlockCopy(tmpBuffer, tmpOffset, buffer, offset, toCopy);
                tmpOffset += toCopy;
                tmpCount -= toCopy;
                offset += toCopy;
                count -= toCopy;
                outTotal += toCopy;
            }
            if (count > 0 && unpack.DestSize > 0)
            {
                outBuffer = buffer;
                outOffset = offset;
                outCount = count;
                fetch = true;
                unpack.doUnpack();
                fetch = false;
            }
            return outTotal;
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
            if (!fetch)
            {
                throw new NotSupportedException();
            }
            if (outCount > 0)
            {
                int toCopy = outCount < count ? outCount : count;
                Buffer.BlockCopy(buffer, offset, outBuffer, outOffset, toCopy);
                outOffset += toCopy;
                outCount -= toCopy;
                offset += toCopy;
                count -= toCopy;
                outTotal += toCopy;
            }
            if (count > 0)
            {
                if (tmpBuffer.Length < tmpCount + count)
                {
                    byte[] newBuffer =
                        new byte[tmpBuffer.Length * 2 > tmpCount + count ? tmpBuffer.Length * 2 : tmpCount + count];
                    Buffer.BlockCopy(tmpBuffer, 0, newBuffer, 0, tmpCount);
                    tmpBuffer = newBuffer;
                }
                Buffer.BlockCopy(buffer, offset, tmpBuffer, tmpCount, count);
                tmpCount += count;
                tmpOffset = 0;
                unpack.Suspended = true;
            }
            else
                unpack.Suspended = false;
        }
    }
}