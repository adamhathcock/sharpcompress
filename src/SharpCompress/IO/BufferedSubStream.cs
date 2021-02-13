using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    internal class BufferedSubStream : NonDisposingStream
    {
        private long position;
        private int cacheOffset;
        private int cacheLength;
        private readonly byte[] cache;

        public BufferedSubStream(Stream stream, long origin, long bytesToRead) : base(stream, throwOnDispose: false)
        {
            position = origin;
            BytesLeftToRead = bytesToRead;
            cache = new byte[32 << 10];
        }

        private long BytesLeftToRead { get; set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => BytesLeftToRead;

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count > BytesLeftToRead)
            {
                count = (int)BytesLeftToRead;
            }

            if (count > 0)
            {
                if (cacheLength == 0)
                {
                    cacheOffset = 0;
                    Stream.Position = position;
                    cacheLength = await Stream.ReadAsync(cache, 0, cache.Length, cancellationToken);
                    position += cacheLength;
                }

                if (count > cacheLength)
                {
                    count = cacheLength;
                }

                Buffer.BlockCopy(cache, cacheOffset, buffer, offset, count);
                cacheOffset += count;
                cacheLength -= count;
                BytesLeftToRead -= count;
            }

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}