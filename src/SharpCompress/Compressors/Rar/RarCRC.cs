using System;
using SharpCompress.Crypto;

namespace SharpCompress.Compressors.Rar;

internal static class RarCRC
{
    // Reuse Crc32Stream's cached slice-by-16 table (same polynomial) instead of a separate
    // byte-at-a-time table, so bulk CRC checks during Rar extraction get the same throughput
    // as Zip/GZip/7Zip/LZip CRC32 validation instead of a much slower one-byte-per-iteration loop.
    private static readonly uint[] crcTab = Crc32Stream.InitializeTable(
        Crc32Stream.DEFAULT_POLYNOMIAL
    );

    public static uint CheckCrc(uint startCrc, byte b) =>
        (crcTab[((int)startCrc ^ b) & 0xff] ^ (startCrc >> 8));

    public static uint CheckCrc(uint startCrc, ReadOnlySpan<byte> data, int offset, int count)
    {
        var size = Math.Min(data.Length - offset, count);
        return Crc32Stream.CalculateCrc(crcTab, startCrc, data.Slice(offset, size));
    }
}
