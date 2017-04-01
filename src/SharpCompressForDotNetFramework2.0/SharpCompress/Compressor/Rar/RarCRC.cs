using System;

namespace SharpCompress.Compressor.Rar
{
    internal static class RarCRC
    {
        private static uint[] crcTab;

        public static uint CheckCrc(uint startCrc, byte[] data, int offset, int count)
        {
            int size = Math.Min(data.Length - offset, count);

            for (int i = 0; i < size; i++)
            {
                startCrc = (crcTab[((int)((int)startCrc ^ (int)data[offset + i])) & 0xff] ^ (startCrc >> 8));
            }
            return (startCrc);
        }

        static RarCRC()
        {
            {
                crcTab = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int j = 0; j < 8; j++)
                    {
                        if ((c & 1) != 0)
                        {
                            c = c >> 1;
                            c ^= 0xEDB88320;
                        }
                        else
                        {
                            c = c >> 1;
                        }
                    }
                    crcTab[i] = c;
                }
            }
        }
    }
}