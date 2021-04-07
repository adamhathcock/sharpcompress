using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    internal class ReadOnlySubStream : NonDisposingStream
    {
        public ReadOnlySubStream(Stream stream, long bytesToRead)
            : this(stream, null, bytesToRead)
        {
        }

        public ReadOnlySubStream(Stream stream, long? origin, long bytesToRead)
            : base(stream, throwOnDispose: false)
        {
            if (origin != null)
            {
                stream.Position = origin.Value;
            }
            BytesLeftToRead = bytesToRead;
        }

        private long BytesLeftToRead { get; set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = buffer.Length;
            if (BytesLeftToRead < count)
            {
                count = (int)BytesLeftToRead;
            }

            if (count == 0)
            {
                return 0;
            }
            int read = await Stream.ReadAsync(buffer.Slice(0, count), cancellationToken);
            if (read > 0)
            {
                BytesLeftToRead -= read;
            }
            return read;
        }
        
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (BytesLeftToRead < count)
            {
                count = (int)BytesLeftToRead;
            }

            if (count == 0)
            {
                return 0;
            }
            int read = await Stream.ReadAsync(buffer, offset, count, cancellationToken);
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
    }
}