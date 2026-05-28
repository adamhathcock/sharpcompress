using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public static partial class BinaryUtils
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
            throw new IncompleteArchiveException("Unexpected end of stream.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    internal static uint ReadLittleEndianUInt32(this Stream stream) =>
        unchecked((uint)ReadLittleEndianInt32(stream));

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
