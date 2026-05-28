using System;
using System.IO;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Stream extension methods for writing 7z binary format primitives.
/// Mirrors the read-side encoding in DataReader.ReadNumber() and the reference
/// StreamExtensions (ReadDecodedUInt64/WriteEncodedUInt64/WriteBoolVector).
/// </summary>
internal static class SevenZipWriteExtensions
{
    /// <summary>
    /// Writes a variable-length encoded 64-bit unsigned integer to the stream.
    /// Uses the 7z VLQ format: the first byte has leading 1-bits indicating how many
    /// extra bytes follow, with remaining bits holding the high part of the value.
    /// </summary>
    public static int WriteEncodedUInt64(this Stream stream, ulong value)
    {
        var data = new byte[9];
        data[0] = 0xFF;
        byte mask = 0x80;
        var length = 1;

        for (var i = 0; i < 8; i++)
        {
            if (value < mask)
            {
                var headerMask = (byte)((0xFF ^ mask) ^ (mask - 1u));
                data[0] = (byte)(value | headerMask);
                break;
            }

            data[length++] = (byte)(value & 0xFF);
            value >>= 8;
            mask >>= 1;
        }

        stream.Write(data, 0, length);
        return length;
    }

    /// <summary>
    /// Writes a boolean vector as a packed bitmask.
    /// Each bool becomes one bit, MSB first, padded to byte boundary.
    /// </summary>
    public static ulong WriteBoolVector(this Stream stream, bool[] vector)
    {
        byte mask = 0x80;
        byte b = 0;
        ulong bytesWritten = 0;

        for (var i = 0L; i < vector.LongLength; i++)
        {
            if (vector[i])
            {
                b |= mask;
            }

            mask >>= 1;
            if (mask == 0)
            {
                stream.WriteByte(b);
                bytesWritten++;
                mask = 0x80;
                b = 0;
            }
        }

        if (mask != 0x80)
        {
            stream.WriteByte(b);
            bytesWritten++;
        }

        return bytesWritten;
    }

    /// <summary>
    /// Writes an optional bool vector. If all elements are true, writes a single 0x01 byte
    /// (AllAreDefined marker). Otherwise writes 0x00 followed by the packed bitmask.
    /// </summary>
    public static void WriteOptionalBoolVector(this Stream stream, bool[] vector)
    {
        for (var i = 0L; i < vector.LongLength; i++)
        {
            if (!vector[i])
            {
                stream.WriteByte(0);
                stream.WriteBoolVector(vector);
                return;
            }
        }

        stream.WriteByte(1);
    }
}
