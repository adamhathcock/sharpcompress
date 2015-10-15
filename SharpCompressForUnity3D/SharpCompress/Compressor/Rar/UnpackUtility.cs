namespace SharpCompress.Compressor.Rar
{
    using SharpCompress;
    using SharpCompress.Compressor.Rar.decode;
    using SharpCompress.Compressor.Rar.VM;
    using System;
    using System.Runtime.CompilerServices;

    [Extension]
    internal static class UnpackUtility
    {
        [Extension]
        internal static int decodeNumber(BitInput input, Decode dec)
        {
            int num;
            long num2 = input.GetBits() & 0xfffe;
            int[] decodeLen = dec.DecodeLen;
            if (num2 < decodeLen[8])
            {
                if (num2 < decodeLen[4])
                {
                    if (num2 < decodeLen[2])
                    {
                        if (num2 < decodeLen[1])
                        {
                            num = 1;
                        }
                        else
                        {
                            num = 2;
                        }
                    }
                    else if (num2 < decodeLen[3])
                    {
                        num = 3;
                    }
                    else
                    {
                        num = 4;
                    }
                }
                else if (num2 < decodeLen[6])
                {
                    if (num2 < decodeLen[5])
                    {
                        num = 5;
                    }
                    else
                    {
                        num = 6;
                    }
                }
                else if (num2 < decodeLen[7])
                {
                    num = 7;
                }
                else
                {
                    num = 8;
                }
            }
            else if (num2 < decodeLen[12])
            {
                if (num2 < decodeLen[10])
                {
                    if (num2 < decodeLen[9])
                    {
                        num = 9;
                    }
                    else
                    {
                        num = 10;
                    }
                }
                else if (num2 < decodeLen[11])
                {
                    num = 11;
                }
                else
                {
                    num = 12;
                }
            }
            else if (num2 < decodeLen[14])
            {
                if (num2 < decodeLen[13])
                {
                    num = 13;
                }
                else
                {
                    num = 14;
                }
            }
            else
            {
                num = 15;
            }
            input.AddBits(num);
            int index = dec.DecodePos[num] + Utility.URShift((int) (((int) num2) - decodeLen[num - 1]), (int) (0x10 - num));
            if (index >= dec.MaxNum)
            {
                index = 0;
            }
            return dec.DecodeNum[index];
        }

        internal static void makeDecodeTables(byte[] lenTab, int offset, Decode dec, int size)
        {
            int num;
            int[] array = new int[0x10];
            int[] numArray2 = new int[0x10];
            Utility.Fill<int>(array, 0);
            Utility.Fill<int>(dec.DecodeNum, 0);
            for (num = 0; num < size; num++)
            {
                array[lenTab[offset + num] & 15]++;
            }
            array[0] = 0;
            numArray2[0] = 0;
            dec.DecodePos[0] = 0;
            dec.DecodeLen[0] = 0;
            long num3 = 0L;
            for (num = 1; num < 0x10; num++)
            {
                num3 = 2L * (num3 + array[num]);
                long num2 = num3 << (15 - num);
                if (num2 > 0xffffL)
                {
                    num2 = 0xffffL;
                }
                dec.DecodeLen[num] = (int) num2;
                numArray2[num] = dec.DecodePos[num] = dec.DecodePos[num - 1] + array[num - 1];
            }
            for (num = 0; num < size; num++)
            {
                if (lenTab[offset + num] != 0)
                {
                    dec.DecodeNum[numArray2[lenTab[offset + num] & 15]++] = num;
                }
            }
            dec.MaxNum = size;
        }
    }
}

