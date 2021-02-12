#nullable disable

using System;
using System.Collections.Generic;

namespace SharpCompress.Compressors.Xz
{
    internal static class Crc64
    {
        public const UInt64 DefaultSeed = 0x0;

        internal static UInt64[] Table;

        public const UInt64 Iso3309Polynomial = 0xD800000000000000;

        public static UInt64 Compute(byte[] buffer)
        {
            return Compute(DefaultSeed, buffer);
        }

        public static UInt64 Compute(UInt64 seed, byte[] buffer)
        {
            Table ??= CreateTable(Iso3309Polynomial);

            return CalculateHash(seed, Table, buffer);
        }

        public static UInt64 CalculateHash(UInt64 seed, UInt64[] table, ReadOnlySpan<byte> buffer)
        {
            var crc = seed;
            int len = buffer.Length;
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
            var createTable = new UInt64[256];
            for (var i = 0; i < 256; ++i)
            {
                var entry = (UInt64)i;
                for (var j = 0; j < 8; ++j)
                {
                    if ((entry & 1) == 1)
                    {
                        entry = (entry >> 1) ^ polynomial;
                    }
                    else
                    {
                        entry = entry >> 1;
                    }
                }

                createTable[i] = entry;
            }
            return createTable;
        }
    }

}
