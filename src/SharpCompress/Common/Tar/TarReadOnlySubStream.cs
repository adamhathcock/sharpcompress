using SharpCompress.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Tar
{
    internal class TarReadOnlySubStream : NonDisposingStream
    {
        private bool _isDisposed;
        private long _amountRead;

        public TarReadOnlySubStream(Stream stream, long bytesToRead) : base(stream, throwOnDispose: false)
        {
            BytesLeftToRead = bytesToRead;
        }

        public override async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Ensure we read all remaining blocks for this entry.
            await Stream.SkipAsync(BytesLeftToRead);
            _amountRead += BytesLeftToRead;

            // If the last block wasn't a full 512 bytes, skip the remaining padding bytes.
            var bytesInLastBlock = _amountRead % 512;

            if (bytesInLastBlock != 0)
            {
                await Stream.SkipAsync(512 - bytesInLastBlock);
            }
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
            if (BytesLeftToRead < buffer.Length)
            {
                count = (int)BytesLeftToRead;
            }
            int read = await Stream.ReadAsync(buffer.Slice(0, count), cancellationToken);
            if (read > 0)
            {
                BytesLeftToRead -= read;
                _amountRead += read;
            }
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
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
    }
}