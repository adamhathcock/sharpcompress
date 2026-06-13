using System;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public static class Crc64
{
    public const ulong DefaultSeed = 0x0;
    internal const ulong XZ_SEED = 0xffffffffffffffff;

    internal static ulong[]? Table;
    private static ulong[]? _xzTable;

    public const ulong Iso3309Polynomial = 0xD800000000000000;
    private const ulong XZ_POLYNOMIAL = 0xC96C5795D7870F42;

    public static ulong Compute(byte[] buffer) => Compute(DefaultSeed, buffer);

    public static ulong Compute(ulong seed, byte[] buffer)
    {
        Table ??= CreateTable(Iso3309Polynomial);

        return CalculateHash(seed, Table, buffer);
    }

    public static ulong ComputeXz(byte[] buffer) => ~UpdateXz(XZ_SEED, buffer);

    public static ulong UpdateXz(ulong seed, ReadOnlySpan<byte> buffer)
    {
        _xzTable ??= CreateTable(XZ_POLYNOMIAL);

        return CalculateHash(seed, _xzTable, buffer);
    }

    public static ulong CalculateHash(ulong seed, ulong[] table, ReadOnlySpan<byte> buffer)
    {
        var crc = seed;
        var len = buffer.Length;
        for (var i = 0; i < len; i++)
        {
            unchecked
            {
                crc = (crc >> 8) ^ table[(buffer[i] ^ crc) & 0xff];
            }
        }

        return crc;
    }

    public static ulong[] CreateTable(ulong polynomial)
    {
        var createTable = new ulong[256];
        for (var i = 0; i < 256; ++i)
        {
            var entry = (ulong)i;
            for (var j = 0; j < 8; ++j)
            {
                if ((entry & 1) == 1)
                {
                    entry = (entry >> 1) ^ polynomial;
                }
                else
                {
                    entry >>= 1;
                }
            }

            createTable[i] = entry;
        }
        return createTable;
    }
}
