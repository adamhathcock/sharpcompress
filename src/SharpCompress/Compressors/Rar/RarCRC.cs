using System;

namespace SharpCompress.Compressors.Rar;

internal static class RarCRC
{
    private static readonly uint[] crcTab;

    public static uint CheckCrc(uint startCrc, byte b) =>
        (crcTab[((int)startCrc ^ b) & 0xff] ^ (startCrc >> 8));

    public static uint CheckCrc(uint startCrc, ReadOnlySpan<byte> data, int offset, int count)
    {
        var size = Math.Min(data.Length - offset, count);

        for (var i = 0; i < size; i++)
        {
            startCrc = (crcTab[((int)startCrc ^ data[offset + i]) & 0xff] ^ (startCrc >> 8));
        }
        return (startCrc);
    }

    static RarCRC()
    {
        {
            crcTab = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var c = i;
                for (var j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0)
                    {
                        c >>= 1;
                        c ^= 0xEDB88320;
                    }
                    else
                    {
                        c >>= 1;
                    }
                }
                crcTab[i] = c;
            }
        }
    }
}
