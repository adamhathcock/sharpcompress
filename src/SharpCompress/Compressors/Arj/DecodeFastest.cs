using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpCompress.Compressors.Arj
{
    [CLSCompliant(true)]
    public class BitReader
    {
        private readonly byte[] data;
        private int bytePos = 0;
        private int bitPos = 0;

        public BitReader(byte[] input)
        {
            data = input;
        }

        public int ReadBits(int count)
        {
            int result = 0;
            for (int i = 0; i < count; i++)
            {
                if (bytePos >= data.Length)
                {
                    throw new EndOfStreamException();
                }

                int bit = (data[bytePos] >> (7 - bitPos)) & 1;
                result = (result << 1) | bit;

                bitPos++;
                if (bitPos == 8)
                {
                    bitPos = 0;
                    bytePos++;
                }
            }
            return result;
        }
    }

    [CLSCompliant(true)]
    public static class LHDecoder
    {
        private const int THRESHOLD = 3;

        private static int DecodeVal(BitReader r, int from, int to)
        {
            int add = 0;
            int bit = from;

            while (bit < to && r.ReadBits(1) == 1)
            {
                add |= 1 << bit;
                bit++;
            }

            int res = bit > 0 ? r.ReadBits(bit) : 0;
            return res + add;
        }

        public static byte[] DecodeFastest(byte[] data, int originalSize)
        {
            var res = new List<byte>(originalSize);
            var r = new BitReader(data);

            while (res.Count < originalSize)
            {
                int len = DecodeVal(r, 0, 7);
                if (len == 0)
                {
                    byte nextChar = (byte)r.ReadBits(8);
                    res.Add(nextChar);
                }
                else
                {
                    int repCount = len + THRESHOLD - 1;
                    int backPtr = DecodeVal(r, 9, 13);

                    if (backPtr >= res.Count)
                    {
                        throw new InvalidDataException("invalid back_ptr");
                    }

                    int i = res.Count - 1 - backPtr;
                    for (int j = 0; j < repCount; j++)
                    {
                        res.Add(res[i]);
                        i++;
                    }
                }
            }

            return res.ToArray();
        }
    }
}
