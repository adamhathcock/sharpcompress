/*
 * BranchExecFilter.cs -- Converters for executable
 * <Contribution by Louis-Michel Bergeron, on behalf of aDolus Technolog Inc., 2022>
 * @TODO Encoding
 */

using System;
using System.IO;

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

    public static void X86Converter(byte[] data, uint ip, ref uint state)
    {
        long i = 0;
        long size = data.Length;
        uint pos = 0;
        var mask = state & 7;
        if (size < 5)
        {
            return;
        }

        size -= 4;
        ip += 5;

        for (; ; )
        {
            i = pos;

            for (; i < size; i++)
            {
                if ((data[i] & 0xFE) == 0xE8)
                {
                    break;
                }
            }

            var d = (uint)(i) - pos;
            pos = (uint)i;
            if (i >= size)
            {
                state = (d > 2 ? 0 : mask >> (int)d);
                return;
            }
            if (d > 2)
            {
                mask = 0;
            }
            else
            {
                mask >>= (int)d;
                if (
                    mask != 0
                    && (mask > 4 || mask == 3 || (((((data[(mask >> 1) + 1])) + 1) & 0xFE) == 0))
                )
                {
                    mask = (mask >> 1) | 4;
                    pos++;
                    continue;
                }
            }

            if ((((data[i + 4]) + 1) & 0xFE) == 0)
            {
                var inst =
                    ((uint)data[i + 4] << 24)
                    | ((uint)data[i + 3] << 16)
                    | ((uint)data[i + 2] << 8)
                    | data[i + 1];
                var cur = ip + pos;
                pos += 5;

                inst -= cur;
                if (mask != 0)
                {
                    var sh = (mask & 6) << 2;
                    if (((((((byte)(inst >> (int)sh))) + 1) & 0xFE) == 0))
                    {
                        inst ^= (((uint)0x100 << (int)sh) - 1);
                        inst -= cur;
                    }
                    mask = 0;
                }
                data[i + 1] = (byte)inst;
                data[i + 2] = (byte)(inst >> 8);
                data[i + 3] = (byte)(inst >> 16);
                data[i + 4] = (byte)(0 - ((inst >> 24) & 1));
            }
            else
            {
                mask = (mask >> 1) | 4;
                pos++;
            }
        }
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
