using System;
using System.IO;

namespace SharpCompress.Compressors.Filters
{
    internal abstract class Filter : Stream
    {
        protected bool isEncoder;
        protected Stream baseStream;

        private readonly byte[] tail;
        private readonly byte[] window;
        private int transformed;
        private int read;
        private bool endReached;
        private bool isDisposed;

        protected Filter(bool isEncoder, Stream baseStream, int lookahead)
        {
            this.isEncoder = isEncoder;
            this.baseStream = baseStream;
            tail = new byte[lookahead - 1];
            window = new byte[tail.Length * 2];
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            base.Dispose(disposing);
            baseStream.Dispose();
        }

        public override bool CanRead { get { return !isEncoder; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return isEncoder; } }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length { get { return baseStream.Length; } }

        public override long Position { get { return baseStream.Position; } set { throw new NotSupportedException(); } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int size = 0;

            if (transformed > 0)
            {
                int copySize = transformed;
                if (copySize > count)
                {
                    copySize = count;
                }
                Buffer.BlockCopy(tail, 0, buffer, offset, copySize);
                transformed -= copySize;
                read -= copySize;
                offset += copySize;
                count -= copySize;
                size += copySize;
                Buffer.BlockCopy(tail, copySize, tail, 0, read);
            }
            if (count == 0)
            {
                return size;
            }

            int inSize = read;
            if (inSize > count)
            {
                inSize = count;
            }
            Buffer.BlockCopy(tail, 0, buffer, offset, inSize);
            read -= inSize;
            Buffer.BlockCopy(tail, inSize, tail, 0, read);
            while (!endReached && inSize < count)
            {
                int baseRead = baseStream.Read(buffer, offset + inSize, count - inSize);
                inSize += baseRead;
                if (baseRead == 0)
                {
                    endReached = true;
                }
            }
            while (!endReached && read < tail.Length)
            {
                int baseRead = baseStream.Read(tail, read, tail.Length - read);
                read += baseRead;
                if (baseRead == 0)
                {
                    endReached = true;
                }
            }

            if (inSize > tail.Length)
            {
                transformed = Transform(buffer, offset, inSize);
                offset += transformed;
                count -= transformed;
                size += transformed;
                inSize -= transformed;
                transformed = 0;
            }

            if (count == 0)
            {
                return size;
            }

            Buffer.BlockCopy(buffer, offset, window, 0, inSize);
            Buffer.BlockCopy(tail, 0, window, inSize, read);
            if (inSize + read > tail.Length)
            {
                transformed = Transform(window, 0, inSize + read);
            }
            else
            {
                transformed = inSize + read;
            }
            Buffer.BlockCopy(window, 0, buffer, offset, inSize);
            Buffer.BlockCopy(window, inSize, tail, 0, read);
            size += inSize;
            transformed -= inSize;

            return size;
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
            Transform(buffer, offset, count);
            baseStream.Write(buffer, offset, count);
        }

        protected abstract int Transform(byte[] buffer, int offset, int count);
    }
}