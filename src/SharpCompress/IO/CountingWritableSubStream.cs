using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    internal class CountingWritableSubStream : NonDisposingStream
    {
        internal CountingWritableSubStream(Stream stream) : base(stream, throwOnDispose: false)
        {
        }

        public ulong Count { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override void Flush()
        {
            Stream.Flush();
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

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Stream.WriteAsync(buffer, offset, count, cancellationToken);
            Count += (uint)buffer.Length;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Stream.WriteAsync(buffer, cancellationToken);
            Count += (uint)buffer.Length;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Stream.Write(buffer, offset, count);
            Count += (uint)count;
        }

        public override void WriteByte(byte value)
        {
            Stream.WriteByte(value);
            ++Count;
        }
    }
}