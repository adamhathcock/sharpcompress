using System;
using System.Buffers.Binary;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz
{
    public static class BinaryUtils
    {
        public static int ReadLittleEndianInt32(this BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }

        internal static uint ReadLittleEndianUInt32(this BinaryReader reader)
        {
            return unchecked((uint)ReadLittleEndianInt32(reader));
        }
        public static async ValueTask<int> ReadLittleEndianInt32(this Stream stream, CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(4);
            var slice = buffer.Memory.Slice(0, 4);
            var read = await stream.ReadAsync(slice, cancellationToken);
            if (read != 4)
            {
                throw new EndOfStreamException();
            }
            return BinaryPrimitives.ReadInt32LittleEndian(slice.Span);
        }

        internal static async ValueTask<uint> ReadLittleEndianUInt32(this Stream stream, CancellationToken cancellationToken)
        {
            return unchecked((uint)await ReadLittleEndianInt32(stream, cancellationToken));
        }

        internal static byte[] ToBigEndianBytes(this uint uint32)
        {
            var result = BitConverter.GetBytes(uint32);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(result);
            }

            return result;
        }

        internal static byte[] ToLittleEndianBytes(this uint uint32)
        {
            var result = BitConverter.GetBytes(uint32);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(result);
            }

            return result;
        }
    }
}
