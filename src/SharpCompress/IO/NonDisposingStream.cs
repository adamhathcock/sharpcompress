using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    public class NonDisposingStream : AsyncStream
    {
        public NonDisposingStream(Stream stream, bool throwOnDispose = false)
        {
            Stream = stream;
            ThrowOnDispose = throwOnDispose;
        }

        public bool ThrowOnDispose { get; set; }

        public override ValueTask DisposeAsync()
        {
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException($"Attempt to dispose of a {nameof(NonDisposingStream)} when {nameof(ThrowOnDispose)} is {ThrowOnDispose}");
            }

            return new ValueTask();
        }

        protected Stream Stream { get; }

        public override bool CanRead => Stream.CanRead;

        public override bool CanSeek => Stream.CanSeek;

        public override bool CanWrite => Stream.CanWrite;

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Stream.FlushAsync(cancellationToken);
        }

        public override long Length => Stream.Length;

        public override long Position { get => Stream.Position; set => Stream.Position = value; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            return Stream.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Stream.SetLength(value);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            return Stream.WriteAsync(buffer, cancellationToken);
        }
    }
}