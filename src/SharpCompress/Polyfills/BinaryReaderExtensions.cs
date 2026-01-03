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
            var bytes = new byte[count];
            await reader
                .BaseStream.ReadExactAsync(bytes, 0, count, cancellationToken)
                .ConfigureAwait(false);
            return bytes;
        }
    }
}
