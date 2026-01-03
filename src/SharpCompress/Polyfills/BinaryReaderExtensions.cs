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
                var bytesRead = await reader
                    .BaseStream.ReadAsync(buffer, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead != 1)
                {
                    throw new EndOfStreamException();
                }

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
                var bytesRead = await reader
                    .BaseStream.ReadAsync(buffer, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead != count)
                {
                    throw new EndOfStreamException();
                }
                var bytes = new byte[count];
                System.Buffer.BlockCopy(buffer, 0, bytes, 0, count);
                return bytes;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
