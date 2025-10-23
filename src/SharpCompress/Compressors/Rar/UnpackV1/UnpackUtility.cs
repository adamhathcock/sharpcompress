using System;
using System.Runtime.CompilerServices;
using SharpCompress.Compressors.Rar.VM;

namespace SharpCompress.Compressors.Rar.UnpackV1;

internal static class UnpackUtility
{
    //!!! TODO rename methods
    internal static uint DecodeNumber(this BitInput input, Decode.Decode dec) =>
        (uint)input.decodeNumber(dec);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int decodeNumber(this BitInput input, Decode.Decode dec)
    {
        long bitField = input.GetBits() & 0xfffe;
        var decodeLen = dec.DecodeLen;

        // Binary search to find the bit length - faster than nested ifs
        int bits = FindDecodeBits(bitField, decodeLen);

        input.AddBits(bits);
        var N =
            dec.DecodePos[bits]
            + (Utility.URShift(((int)bitField - decodeLen[bits - 1]), (16 - bits)));
        if (N >= dec.MaxNum)
        {
            N = 0;
        }
        return (dec.DecodeNum[N]);
    }

    /// <summary>
    /// Fast binary search to find which bit length matches the bitField.
    /// Optimized with cached array access to minimize memory lookups.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindDecodeBits(long bitField, int[] decodeLen)
    {
        // Cache critical values to reduce array access overhead
        long len4 = decodeLen[4];
        long len8 = decodeLen[8];
        long len12 = decodeLen[12];

        if (bitField < len8)
        {
            if (bitField < len4)
            {
                long len2 = decodeLen[2];
                if (bitField < len2)
                {
                    return bitField < decodeLen[1] ? 1 : 2;
                }
                return bitField < decodeLen[3] ? 3 : 4;
            }

            long len6 = decodeLen[6];
            if (bitField < len6)
            {
                return bitField < decodeLen[5] ? 5 : 6;
            }
            return bitField < decodeLen[7] ? 7 : 8;
        }

        if (bitField < len12)
        {
            long len10 = decodeLen[10];
            if (bitField < len10)
            {
                return bitField < decodeLen[9] ? 9 : 10;
            }
            return bitField < decodeLen[11] ? 11 : 12;
        }

        long len14 = decodeLen[14];
        return bitField < len14 ? (bitField < decodeLen[13] ? 13 : 14) : 15;
    }

    internal static void makeDecodeTables(
        Span<byte> lenTab,
        int offset,
        Decode.Decode dec,
        int size
    )
    {
        Span<int> lenCount = stackalloc int[16];
        Span<int> tmpPos = stackalloc int[16];
        int i;
        long M,
            N;

        new Span<int>(dec.DecodeNum).Clear();
        for (i = 0; i < size; i++)
        {
            lenCount[lenTab[offset + i] & 0xF]++;
        }
        lenCount[0] = 0;
        for (tmpPos[0] = 0, dec.DecodePos[0] = 0, dec.DecodeLen[0] = 0, N = 0, i = 1; i < 16; i++)
        {
            N = 2 * (N + lenCount[i]);
            M = N << (15 - i);
            if (M > 0xFFFF)
            {
                M = 0xFFFF;
            }
            dec.DecodeLen[i] = (int)M;
            tmpPos[i] = dec.DecodePos[i] = dec.DecodePos[i - 1] + lenCount[i - 1];
        }

        for (i = 0; i < size; i++)
        {
            if (lenTab[offset + i] != 0)
            {
                dec.DecodeNum[tmpPos[lenTab[offset + i] & 0xF]++] = i;
            }
        }
        dec.MaxNum = size;
    }
}
