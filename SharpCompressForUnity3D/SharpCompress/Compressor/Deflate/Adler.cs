namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal sealed class Adler
    {
        private static readonly int BASE = 0xfff1;
        private static readonly int NMAX = 0x15b0;

        internal static uint Adler32(uint adler, byte[] buf, int index, int len)
        {
            if (buf == null)
            {
                return 1;
            }
            int num = ((int) adler) & 0xffff;
            int num2 = ((int) (adler >> 0x10)) & 0xffff;
            while (len > 0)
            {
                int num3 = (len < NMAX) ? len : NMAX;
                len -= num3;
                while (num3 >= 0x10)
                {
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num += buf[index++];
                    num2 += num;
                    num3 -= 0x10;
                }
                if (num3 != 0)
                {
                    do
                    {
                        num += buf[index++];
                        num2 += num;
                    }
                    while (--num3 != 0);
                }
                num = num % BASE;
                num2 = num2 % BASE;
            }
            return (uint) ((num2 << 0x10) | num);
        }
    }
}

