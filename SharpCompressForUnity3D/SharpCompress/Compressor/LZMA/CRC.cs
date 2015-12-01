namespace SharpCompress.Compressor.LZMA
{
    using System;
    using System.IO;

    internal static class CRC
    {
        public const uint kInitCRC = uint.MaxValue;
        private static uint[] kTable;

        static CRC()
        {
            uint num;
            uint num2;
            kTable = new uint[0x400];
            for (num = 0; num < 0x100; num++)
            {
                num2 = num;
                for (int i = 0; i < 8; i++)
                {
                    num2 = (num2 >> 1) ^ (0xedb88320 & ~((num2 & 1) - 1));
                }
                kTable[num] = num2;
            }
            for (num = 0x100; num < kTable.Length; num++)
            {
                num2 = kTable[(int) ((IntPtr) (num - 0x100))];
                kTable[num] = kTable[(int) ((IntPtr) (num2 & 0xff))] ^ (num2 >> 8);
            }
        }

        public static uint Finish(uint crc)
        {
            return ~crc;
        }

        public static uint From(Stream stream, long length)
        {
            uint maxValue = uint.MaxValue;
            byte[] buffer = new byte[Math.Min(length, 0x1000L)];
            while (length > 0L)
            {
                int num2 = stream.Read(buffer, 0, (int) Math.Min(length, (long) buffer.Length));
                if (num2 == 0)
                {
                    throw new EndOfStreamException();
                }
                maxValue = Update(maxValue, buffer, 0, num2);
                length -= num2;
            }
            return Finish(maxValue);
        }

        public static uint Update(uint crc, byte bt)
        {
            return (kTable[(int) ((IntPtr) ((crc & 0xff) ^ bt))] ^ (crc >> 8));
        }

        public static uint Update(uint crc, long value)
        {
            return Update(crc, (ulong) value);
        }

        public static uint Update(uint crc, uint value)
        {
            crc ^= value;
            return (((kTable[(int) ((IntPtr) (0x300 + (crc & 0xff)))] ^ kTable[(int) ((IntPtr) (0x200 + ((crc >> 8) & 0xff)))]) ^ kTable[(int) ((IntPtr) (0x100 + ((crc >> 0x10) & 0xff)))]) ^ kTable[crc >> 0x18]);
        }

        public static uint Update(uint crc, ulong value)
        {
            return Update(Update(crc, (uint) value), (uint) (value >> 0x20));
        }

        public static uint Update(uint crc, byte[] buffer, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                crc = Update(crc, buffer[offset + i]);
            }
            return crc;
        }
    }
}

