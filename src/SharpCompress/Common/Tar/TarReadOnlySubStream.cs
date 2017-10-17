using System;
using System.IO;

namespace SharpCompress.Common.Tar
{
    internal class TarReadOnlySubStream : Stream
    {
        private bool isDisposed;
        private long amountRead;

        public TarReadOnlySubStream(Stream stream, long bytesToRead)
        {
            Stream = stream;
            BytesLeftToRead = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            if (disposing)
            {
                long skipBytes = amountRead % 512;
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
                Stream.ReadFully(buffer);
            }
        }

        private long BytesLeftToRead { get; set; }

        public Stream Stream { get; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (BytesLeftToRead < count)
            {
                count = (int)BytesLeftToRead;
            }
            int read = Stream.Read(buffer, offset, count);
            if (read > 0)
            {
                BytesLeftToRead -= read;
                amountRead += read;
            }
            return read;
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
    }
}