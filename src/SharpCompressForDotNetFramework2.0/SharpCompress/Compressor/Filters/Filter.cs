using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SharpCompress.Compressor.Filters
{
    public abstract class Filter : Stream
    {
        protected bool isEncoder;
        protected Stream baseStream;

        private byte[] tail;
        private byte[] window;
        private int transformed = 0;
        private int read = 0;
        private bool endReached = false;

        protected Filter(bool isEncoder, Stream baseStream, int lookahead)
        {
            this.isEncoder = isEncoder;
            this.baseStream = baseStream;
            tail = new byte[lookahead - 1];
            window = new byte[tail.Length*2];
        }

        public override bool CanRead
        {
            get { return !isEncoder; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return isEncoder; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { return baseStream.Length; }
        }

        public override long Position
        {
            get
            {
                return baseStream.Position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int size = 0;

            if (transformed > 0) 
            {
                int copySize = transformed;
                if (copySize > count)
                    copySize = count;
                Buffer.BlockCopy(tail, 0, buffer, offset, copySize);
                transformed -= copySize;
                read -= copySize;
                offset += copySize;
                count -= copySize;
                size += copySize;
                Buffer.BlockCopy(tail, copySize, tail, 0, read);
            }
            if (count == 0)
                return size;

            int inSize = read;
            if (inSize > count)
                inSize = count;
            Buffer.BlockCopy(tail, 0, buffer, offset, inSize);
            read -= inSize;
            Buffer.BlockCopy(tail, inSize, tail, 0, read);
            while (!endReached && inSize < count)
            {
                int baseRead = baseStream.Read(buffer, offset + inSize, count - inSize);
                inSize += baseRead;
                if (baseRead == 0)
                    endReached = true;
            }
            while (!endReached && read < tail.Length)
            {
                int baseRead = baseStream.Read(tail, read, tail.Length - read);
                read += baseRead;
                if (baseRead == 0)
                    endReached = true;
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
                return size;

            Buffer.BlockCopy(buffer, offset, window, 0, inSize);
            Buffer.BlockCopy(tail, 0, window, inSize, read);
            if (inSize + read > tail.Length)
                transformed = Transform(window, 0, inSize + read);
            else
                transformed = inSize + read;
            Buffer.BlockCopy(window, 0, buffer, offset, inSize);
            Buffer.BlockCopy(window, inSize, tail, 0, read);
            size += inSize;
            transformed -= inSize;

            return size;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Transform(buffer, offset, count);
            baseStream.Write(buffer, offset, count);
        }

        protected abstract int Transform(byte[] buffer, int offset, int count);
    }
}
