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
            var buffer = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                await reader
                    .BaseStream.ReadExactAsync(buffer, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                return buffer[0];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task<byte[]> ReadBytesAsync(
            int count,
            CancellationToken cancellationToken = default
        )
        {
            var buffer = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                await reader
                    .BaseStream.ReadExactAsync(buffer, 0, count, cancellationToken)
                    .ConfigureAwait(false);
                var bytes = new byte[count];
                System.Array.Copy(buffer, 0, bytes, 0, count);
                return bytes;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
