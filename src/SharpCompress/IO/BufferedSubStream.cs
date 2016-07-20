﻿using System.IO;

namespace SharpCompress.IO
{
    internal class BufferedSubStream : Stream
    {
        private long position;
        private int cacheOffset;
        private int cacheLength;
        private byte[] cache;

        public BufferedSubStream(Stream stream, long origin, long bytesToRead)
        {
            Stream = stream;
            position = origin;
            BytesLeftToRead = bytesToRead;
            cache = new byte[32 << 10];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Stream.Dispose();
            }
        }

        private long BytesLeftToRead { get; set; }

        public Stream Stream { get; private set; }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new System.NotSupportedException();
        }

        public override long Length
        {
            get { throw new System.NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new System.NotSupportedException(); }
            set { throw new System.NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > BytesLeftToRead)
                count = (int)BytesLeftToRead;

            if (count > 0)
            {
                if (cacheLength == 0)
                {
                    cacheOffset = 0;
                    Stream.Position = position;
                    cacheLength = Stream.Read(cache, 0, cache.Length);
                    position += cacheLength;
                }

                if (count > cacheLength)
                    count = cacheLength;

                System.Buffer.BlockCopy(cache, cacheOffset, buffer, offset, count);
                cacheOffset += count;
                cacheLength -= count;
                BytesLeftToRead -= count;
            }

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotSupportedException();
        }
    }
}