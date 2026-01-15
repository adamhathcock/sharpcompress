using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common
{
    public sealed class AsyncBinaryReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly Stream _originalStream;
        private readonly bool _leaveOpen;
        private readonly byte[] _buffer = new byte[8];
        private bool _disposed;

        public AsyncBinaryReader(Stream stream, bool leaveOpen = false, int bufferSize = 4096)
        {
            _originalStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;

            // Use the stream directly without wrapping in BufferedStream
            // BufferedStream uses synchronous Read internally which doesn't work with async-only streams
            // SharpCompress uses SharpCompressStream for buffering which supports true async reads
            _stream = stream;
        }

        public Stream BaseStream => _stream;

        public async ValueTask<byte> ReadByteAsync(CancellationToken ct = default)
        {
            await _stream.ReadExactAsync(_buffer, 0, 1, ct).ConfigureAwait(false);
            return _buffer[0];
        }

        public async ValueTask<ushort> ReadUInt16Async(CancellationToken ct = default)
        {
            await _stream.ReadExactAsync(_buffer, 0, 2, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
        }

        public async ValueTask<uint> ReadUInt32Async(CancellationToken ct = default)
        {
            await _stream.ReadExactAsync(_buffer, 0, 4, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt32LittleEndian(_buffer);
        }

        public async ValueTask<ulong> ReadUInt64Async(CancellationToken ct = default)
        {
            await _stream.ReadExactAsync(_buffer, 0, 8, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt64LittleEndian(_buffer);
        }

        public async ValueTask ReadBytesAsync(
            byte[] bytes,
            int offset,
            int count,
            CancellationToken ct = default
        )
        {
            await _stream.ReadExactAsync(bytes, offset, count, ct).ConfigureAwait(false);
        }

        public async ValueTask SkipAsync(int count, CancellationToken ct = default)
        {
            await _stream.SkipAsync(count, ct).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose the original stream if we own it
            if (!_leaveOpen)
            {
                _originalStream.Dispose();
            }
        }

#if NET8_0_OR_GREATER
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose the original stream if we own it
            if (!_leaveOpen)
            {
                await _originalStream.DisposeAsync().ConfigureAwait(false);
            }
        }
#endif
    }
}
