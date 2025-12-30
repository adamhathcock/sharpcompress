using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common
{
    public sealed class AsyncBinaryReader(Stream stream, bool leaveOpen = false) : IDisposable
    {
        private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        private readonly byte[] _buffer = new byte[8];
        private bool _disposed;

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

        private async ValueTask ReadExactAsync(byte[] destination, int offset, int length, CancellationToken ct)
        {
            var read = 0;
            while (read < length)
            {
                var n = await _stream.ReadAsync(destination, offset + read, length - read, ct).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                read += n;
            }
        }

        public void Dispose()
        {
            if (_disposed || leaveOpen)
            {
                _disposed = true;
                return;
            }

            _disposed = true;
            _stream.Dispose();
        }

#if NET6_0_OR_GREATER
        public ValueTask DisposeAsync()
        {
            if (_disposed || leaveOpen)
            {
                _disposed = true;
                return default;
            }

            _disposed = true;
            return _stream.DisposeAsync();
        }
#endif
    }
}
