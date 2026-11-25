using System;
using System.IO;
using SharpCompress.Crypto;

namespace SharpCompress.Test.Arj;

internal static class ArjTestArchives
{
    // Offset of the original size field within a local header body.
    private const int OriginalSizeOffset = 16;

    /// <summary>
    /// Loads a test archive and rewrites the first entry's original size, recomputing the
    /// header CRC so the archive still parses. Lets size handling be tested without
    /// multi-gigabyte fixtures.
    /// </summary>
    public static byte[] WithFirstEntryOriginalSize(string path, uint originalSize)
    {
        var bytes = File.ReadAllBytes(path);
        var localHeaderStart = SkipHeader(bytes, 0);

        var bodyStart = localHeaderStart + 4;
        var bodySize = BitConverter.ToUInt16(bytes, localHeaderStart + 2);
        WriteUInt32(bytes, bodyStart + OriginalSizeOffset, originalSize);

        var body = new byte[bodySize];
        Array.Copy(bytes, bodyStart, body, 0, bodySize);
        WriteUInt32(bytes, bodyStart + bodySize, Crc32Stream.Compute(body));
        return bytes;
    }

    // Header layout: magic (2), body size (2), body, CRC (4), then extended headers
    // (size (2), data, CRC (4)) terminated by a zero size.
    private static int SkipHeader(byte[] bytes, int start)
    {
        var position = start + 4 + BitConverter.ToUInt16(bytes, start + 2) + 4;
        while (true)
        {
            var extendedSize = BitConverter.ToUInt16(bytes, position);
            position += 2;
            if (extendedSize == 0)
            {
                return position;
            }
            position += extendedSize + 4;
        }
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
