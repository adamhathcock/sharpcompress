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
            await ReadExactAsync(_buffer, 0, 1, ct).ConfigureAwait(false);
            return _buffer[0];
        }

        public async ValueTask<ushort> ReadUInt16Async(CancellationToken ct = default)
        {
            await ReadExactAsync(_buffer, 0, 2, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
        }

        public async ValueTask<uint> ReadUInt32Async(CancellationToken ct = default)
        {
            await ReadExactAsync(_buffer, 0, 4, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt32LittleEndian(_buffer);
        }

        public async ValueTask<ulong> ReadUInt64Async(CancellationToken ct = default)
        {
            await ReadExactAsync(_buffer, 0, 8, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt64LittleEndian(_buffer);
        }

        public async ValueTask<byte[]> ReadBytesAsync(int count, CancellationToken ct = default)
        {
            var result = new byte[count];
            await ReadExactAsync(result, 0, count, ct).ConfigureAwait(false);
            return result;
        }

        private async ValueTask ReadExactAsync(
            byte[] destination,
            int offset,
            int length,
            CancellationToken ct
        )
        {
            var read = 0;
            while (read < length)
            {
                var n = await _stream
                    .ReadAsync(destination, offset + read, length - read, ct)
                    .ConfigureAwait(false);
                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                read += n;
            }
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

#if NET6_0_OR_GREATER
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
