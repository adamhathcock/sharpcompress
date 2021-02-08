using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    public class NonDisposingStream : Stream
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

            return base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException($"Attempt to dispose of a {nameof(NonDisposingStream)} when {nameof(ThrowOnDispose)} is {ThrowOnDispose}");
            }
        }

        protected Stream Stream { get; }

        public override bool CanRead => Stream.CanRead;

        public override bool CanSeek => Stream.CanSeek;

        public override bool CanWrite => Stream.CanWrite;

        public override void Flush()
        {
            Stream.Flush();
        }

        public override long Length => Stream.Length;

        public override long Position { get => Stream.Position; set => Stream.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            return Stream.ReadAsync(buffer, cancellationToken);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            throw new NotImplementedException();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotImplementedException();
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

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            return Stream.WriteAsync(buffer, cancellationToken);
        }

#if !NET461 && !NETSTANDARD2_0

        public override int Read(Span<byte> buffer)
        {
            return Stream.Read(buffer);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Stream.Write(buffer);
        }

#endif
    }
}