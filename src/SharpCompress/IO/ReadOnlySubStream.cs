using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class ReadOnlySubStream : Stream
    {
        public ReadOnlySubStream(Stream stream, long bytesToRead)
            : this(stream, null, bytesToRead)
        {
        }

        public ReadOnlySubStream(Stream stream, long? origin, long bytesToRead)
        {
            Stream = stream;
            if (origin != null)
            {
                stream.Position = origin.Value;
            }
            BytesLeftToRead = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Stream.Dispose();
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