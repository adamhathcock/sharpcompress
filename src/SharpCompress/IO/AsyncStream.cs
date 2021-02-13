using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    public abstract class AsyncStream : Stream
    {
        protected sealed override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new NotSupportedException();
            }
        }
        
        public sealed override void Flush()
        {
            throw new NotSupportedException();
        }

        public abstract override ValueTask DisposeAsync();

        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        
        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            throw new NotSupportedException();
        }

        public sealed override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }
        
        public sealed override int ReadByte()
        {
            throw new NotSupportedException();
        }
        
        public sealed override void WriteByte(byte b)
        {
            throw new NotSupportedException();
        }

        public abstract override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        public abstract override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

#if !NET461 && !NETSTANDARD2_0

        public sealed override int Read(Span<byte> buffer)
        {
            throw new NotSupportedException();
        }

        public sealed override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException();
        }

#endif
    }
}