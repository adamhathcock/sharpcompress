using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public static class BinaryUtils
{
    public static int ReadLittleEndianInt32(this BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    internal static uint ReadLittleEndianUInt32(this BinaryReader reader) =>
        unchecked((uint)ReadLittleEndianInt32(reader));

    public static int ReadLittleEndianInt32(this Stream stream)
    {
        Span<byte> bytes = stackalloc byte[4];
        var read = stream.ReadFully(bytes);
        if (!read)
        {
            throw new EndOfStreamException();
        }
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    internal static uint ReadLittleEndianUInt32(this Stream stream) =>
        unchecked((uint)ReadLittleEndianInt32(stream));

    public static async Task<int> ReadLittleEndianInt32Async(
        this Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = new byte[4];
        var read = await stream.ReadFullyAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (!read)
        {
            throw new EndOfStreamException();
        }
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    internal static async Task<uint> ReadLittleEndianUInt32Async(
        this Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        unchecked(
            (uint)await ReadLittleEndianInt32Async(stream, cancellationToken).ConfigureAwait(false)
        );

    internal static byte[] ToBigEndianBytes(this uint uint32)
    {
        var result = BitConverter.GetBytes(uint32);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(result);
        }

        return result;
    }

    public static byte[] ToLittleEndianBytes(this uint uint32)
    {
        var result = BitConverter.GetBytes(uint32);

        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(result);
        }

        return result;
    }
}
