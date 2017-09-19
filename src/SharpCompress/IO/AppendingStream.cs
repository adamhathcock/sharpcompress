using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.IO
{
    public class ReadOnlyAppendingStream : Stream
    {
        private readonly Queue<Stream> streams;
        private Stream current;

        public ReadOnlyAppendingStream(IEnumerable<Stream> streams)
        {
            this.streams = new Queue<Stream>(streams);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (current == null && streams.Count == 0)
            {
                return -1;
            }
            if (current == null)
            {
                current = streams.Dequeue();
            }
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = current.Read(buffer, offset + totalRead, count - totalRead);
                if (read <= 0)
                {
                    if (streams.Count == 0)
                    {
                        return totalRead;
                    }
                    current = streams.Dequeue();
                }
                totalRead += read;
            }
            return totalRead;
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
            throw new NotImplementedException();
        }
    }
}