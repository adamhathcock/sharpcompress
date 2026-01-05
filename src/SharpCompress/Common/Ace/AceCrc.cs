using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Common.Ace
{
    public class AceCrc
    {
        // CRC-32 lookup table (standard polynomial 0xEDB88320, reflected)
        private static readonly uint[] Crc32Table = GenerateTable();

        private static uint[] GenerateTable()
        {
            var table = new uint[256];

            for (int i = 0; i < 256; i++)
            {
                uint crc = (uint)i;

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320u;
                    else
                        crc >>= 1;
                }

                table[i] = crc;
            }

            return table;
        }

        /// <summary>
        /// Calculate ACE CRC-32 checksum.
        /// ACE CRC-32 uses standard CRC-32 polynomial (0xEDB88320, reflected)
        /// with init=0xFFFFFFFF but NO final XOR.
        /// </summary>
        public static uint AceCrc32(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFFu;

            foreach (byte b in data)
            {
                crc = (crc >> 8) ^ Crc32Table[(crc ^ b) & 0xFF];
            }

            return crc; // No final XOR for ACE
        }

        /// <summary>
        /// ACE CRC-16 is the lower 16 bits of the ACE CRC-32.
        /// </summary>
        public static ushort AceCrc16(ReadOnlySpan<byte> data)
        {
            return (ushort)(AceCrc32(data) & 0xFFFF);
        }
    }
}
