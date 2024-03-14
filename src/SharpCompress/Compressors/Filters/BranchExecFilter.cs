/*
 * BranchExecFilter.cs -- Converters for executable
 * <Contribution by Louis-Michel Bergeron, on behalf of aDolus Technolog Inc., 2022>
 * @TODO Encoding
 */

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SharpCompress.Compressors.Filters;

[CLSCompliant(false)]
public sealed class BranchExecFilter
{
    public enum Alignment : int
    {
        ARCH_x86_ALIGNMENT = 1,
        ARCH_PowerPC_ALIGNMENT = 4,
        ARCH_IA64_ALIGNMENT = 16,
        ARCH_ARM_ALIGNMENT = 4,
        ARCH_ARMTHUMB_ALIGNMENT = 2,
        ARCH_SPARC_ALIGNMENT = 4,
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool X86TestByte(byte b) => b == 0x00 || b == 0xFF;

    //Replaced X86Converter with bcj_x86() - https://github.com/torvalds/linux/blob/master/lib/xz/xz_dec_bcj.c
    //This was to fix an issue decoding a Test zip made with WinZip (that 7zip was also able to read).
    //The previous version of the code would corrupt 2 bytes in the Test.exe at 0x6CF9 (3D6D - should be 4000) - Test zip: WinZip27.Xz.zipx
    public static void X86Converter(byte[] buf, uint ip, ref uint state)
    {
        var mask_to_allowed_status = new[] { true, true, true, false, true, false, false, false };

        var mask_to_bit_num = new byte[] { 0, 1, 2, 2, 3, 3, 3, 3 };

        int i;
        var prev_pos = -1;
        var prev_mask = state & 7;
        uint src;
        uint dest;
        uint j;
        byte b;
        var pos = ip;

        var size = (uint)buf.Length;

        if (size <= 4)
            return;

        size -= 4;
        for (i = 0; i < size; ++i)
        {
            if ((buf[i] & 0xFE) != 0xE8)
                continue;

            prev_pos = i - prev_pos;
            if (prev_pos > 3)
            {
                prev_mask = 0;
            }
            else
            {
                prev_mask = (prev_mask << (prev_pos - 1)) & 7;
                if (prev_mask != 0)
                {
                    b = buf[i + 4 - mask_to_bit_num[prev_mask]];
                    if (!mask_to_allowed_status[prev_mask] || X86TestByte(b))
                    {
                        prev_pos = i;
                        prev_mask = (prev_mask << 1) | 1;
                        continue;
                    }
                }
            }

            prev_pos = i;

            if (X86TestByte(buf[i + 4]))
            {
                src =
                    ((uint)buf[i + 4] << 24)
                    | ((uint)buf[i + 3] << 16)
                    | ((uint)buf[i + 2] << 8)
                    | buf[i + 1];

                while (true)
                {
                    dest = src - (pos + (uint)i + 5);
                    if (prev_mask == 0)
                        break;

                    j = mask_to_bit_num[prev_mask] * 8u;
                    b = (byte)(dest >> (24 - (int)j));
                    if (!X86TestByte(b))
                        break;

                    src = dest ^ ((1u << (32 - (int)j)) - 1u);
                }

                dest &= 0x01FFFFFF;
                dest |= 0 - (dest & 0x01000000);
                buf[i + 1] = (byte)dest;
                buf[i + 2] = (byte)(dest >> 8);
                buf[i + 3] = (byte)(dest >> 16);
                buf[i + 4] = (byte)(dest >> 24);
                i += 4;
            }
            else
            {
                prev_mask = (prev_mask << 1) | 1;
            }
        }
        prev_pos = i - prev_pos;
        prev_mask = prev_pos > 3 ? 0 : prev_mask << (prev_pos - 1);
        state = prev_mask;
        return;
    }

    public static void PowerPCConverter(byte[] data, uint ip)
    {
        long i = 0;
        long size = data.Length;
        size &= ~(uint)3;
        ip -= 4;

        for (; ; ) // infinite loop
        {
            for (; ; ) // infinite loop
            {
                if (i >= size)
                {
                    return;
                }

                i += 4;

                if ((data[i - 4] & 0xFC) == 0x48 && (data[i - 1] & 3) == 1)
                {
                    break;
                }
            }
            {
                var inst = BitConverter.ToUInt32(data, (int)i - 4);

                if (BitConverter.IsLittleEndian)
                {
                    inst = Utility.SwapUINT32(inst);
                }

                inst -= (uint)(ip + i);
                inst &= 0x03FFFFFF;
                inst |= 0x48000000;

                Utility.SetBigUInt32(ref data, inst, (i - 4));
            }
        }
    }

    public static void ARMConverter(byte[] data, uint ip)
    {
        long i = 0;
        long size = data.Length;
        size &= ~(uint)3;
        ip += 4;

        for (; ; ) // infinite loop
        {
            for (; ; ) // infinite loop
            {
                if (i >= size)
                {
                    return;
                }

                i += 4;
                if (data[i - 1] == 0xEB)
                {
                    break;
                }
            }

            var inst = BitConverter.ToUInt32(data, (int)i - 4);
            inst <<= 2;
            inst -= (uint)(ip + i);
            inst >>= 2;
            inst &= 0x00FFFFFF;
            inst |= 0xEB000000;

            Utility.SetLittleUInt32(ref data, inst, i - 4);
        }
    }

    public static void ARMTConverter(byte[] data, uint ip)
    {
        long i = 0;
        long size = data.Length;
        size &= ~(uint)1;
        var lim = size - 4;

        for (; ; )
        {
            uint b1;
            for (; ; )
            {
                uint b3;
                if (i > lim)
                {
                    return;
                }

                b1 = data[i + 1];
                b3 = data[i + 3];
                i += 2;
                b1 ^= 8;
                if ((b3 & b1) >= 0xF8)
                {
                    break;
                }
            }

            var inst =
                (b1 << 19)
                + (((uint)data[i + 1] & 0x7) << 8)
                + (((uint)data[i - 2] << 11))
                + (data[i]);

            i += 2;

            var cur = ((uint)(ip + i)) >> 1;
            inst -= cur;

            data[i - 4] = (byte)(inst >> 11);
            data[i - 3] = (byte)(0xF0 | ((inst >> 19) & 0x7));
            data[i - 2] = (byte)inst;
            data[i - 1] = (byte)(0xF8 | (inst >> 8));
        }
    }

    public static void IA64Converter(byte[] data, uint ip)
    {
        uint i = 0;
        long size = data.Length;
        if (size < 16)
        {
            throw new InvalidDataException("Unexpected data size");
        }

        size -= 16;

        do
        {
            var m = ((uint)0x334B0000 >> (data[i] & 0x1E)) & 3;
            if (m != 0)
            {
                m++;
                do
                {
                    var iterator = (i + (m * 5) - 8);
                    if (
                        ((data[iterator + 3] >> (int)m) & 15) == 5
                        && (((data[iterator - 1] | ((uint)data[iterator] << 8)) >> (int)m) & 0x70)
                            == 0
                    )
                    {
                        var raw = BitConverter.ToUInt32(data, (int)iterator);
                        var inst = raw >> (int)m;
                        inst = (inst & 0xFFFFF) | ((inst & (1 << 23)) >> 3);

                        inst <<= 4;
                        inst -= (ip + i);
                        inst >>= 4;

                        inst &= 0x1FFFFF;
                        inst += 0x700000;
                        inst &= 0x8FFFFF;
                        raw &= ~((uint)0x8FFFFF << (int)m);
                        raw |= (inst << (int)m);

                        Utility.SetLittleUInt32(ref data, raw, iterator);
                    }
                } while (++m <= 4);
            }
            i += 16;
        } while (i <= size);
        return;
    }

    public static void SPARCConverter(byte[] data, uint ip)
    {
        long i = 0;
        long size = data.Length;
        size &= ~(uint)3;
        ip -= 4;

        for (; ; ) // infinite loop
        {
            for (; ; ) // infinite loop
            {
                if (i >= size)
                {
                    return;
                }

                i += 4;
                if (
                    (data[i - 4] == 0x40 && (data[i - 3] & 0xC0) == 0)
                    || (data[i - 4] == 0x7F && (data[i - 3] >= 0xC0))
                )
                {
                    break;
                }
            }

            var inst = BitConverter.ToUInt32(data, (int)i - 4);

            if (BitConverter.IsLittleEndian)
            {
                inst = Utility.SwapUINT32(inst);
            }

            inst <<= 2;
            inst -= (uint)(ip + i);

            inst &= 0x01FFFFFF;
            inst -= (uint)1 << 24;
            inst ^= 0xFF000000;
            inst >>= 2;
            inst |= 0x40000000;

            Utility.SetBigUInt32(ref data, inst, (i - 4));
        }
    }
}
