namespace SharpCompress.Compressor.Rar
{
    using System;

    internal static class RarCRC
    {
        private static uint[] crcTab = new uint[0x100];

        static RarCRC()
        {
            for (uint i = 0; i < 0x100; i++)
            {
                uint num2 = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((num2 & 1) != 0)
                    {
                        num2 = num2 >> 1;
                        num2 ^= 0xedb88320;
                    }
                    else
                    {
                        num2 = num2 >> 1;
                    }
                }
                crcTab[i] = num2;
            }
        }

        public static uint CheckCrc(uint startCrc, byte[] data, int offset, int count)
        {
            int num = Math.Min(data.Length - offset, count);
            for (int i = 0; i < num; i++)
            {
                startCrc = crcTab[((int) (startCrc ^ data[offset + i])) & 0xff] ^ (startCrc >> 8);
            }
            return startCrc;
        }
    }
}

