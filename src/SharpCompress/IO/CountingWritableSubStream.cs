using System;
using System.IO;

namespace SharpCompress.IO
{
    internal class CountingWritableSubStream : Stream
    {
        private readonly Stream writableStream;

        internal CountingWritableSubStream(Stream stream)
        {
            writableStream = stream;
        }

        public ulong Count { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override void Flush()
        {
            writableStream.Flush();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            writableStream.Write(buffer, offset, count);
            Count += (uint)count;
        }
    }
}