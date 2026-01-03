using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress;

public static class BinaryReaderExtensions
{
    extension(BinaryReader reader)
    {
        public async Task<byte> ReadByteAsync(CancellationToken cancellationToken = default)
        {
            var buffer = new byte[1];
            await reader
                .BaseStream.ReadExactAsync(buffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            return buffer[0];
        }

        public async Task<byte[]> ReadBytesAsync(
            int count,
            CancellationToken cancellationToken = default
        )
        {
            // For small allocations, direct allocation is more efficient than pooling
            // due to ArrayPool overhead and the need to copy data to return array
            if (count <= 256)
            {
                var bytes = new byte[count];
                await reader
                    .BaseStream.ReadExactAsync(bytes, 0, count, cancellationToken)
                    .ConfigureAwait(false);
                return bytes;
            }

            // For larger allocations, use ArrayPool to reduce GC pressure
            var buffer = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                await reader
                    .BaseStream.ReadExactAsync(buffer, 0, count, cancellationToken)
                    .ConfigureAwait(false);
                var bytes = new byte[count];
                Array.Copy(buffer, 0, bytes, 0, count);
                return bytes;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
