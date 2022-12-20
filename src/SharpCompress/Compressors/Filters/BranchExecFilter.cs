/*
 * BranchExecFilter.cs -- Converters for executable
 * <Contribution by Louis-Michel Bergeron, on behalf of aDolus Technolog Inc., 2022>
 * @TODO Encoding
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Filters
{
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

        public static void X86Converter(byte[] data, UInt32 ip, ref UInt32 state)
        {
            long i = 0;
            long size = data.Length;
            UInt32 pos = 0;
            UInt32 mask = state & 7;
            if (size < 5)
                return;
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

                UInt32 d = (UInt32)(i) - pos;
                pos = (UInt32)i;
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
                        && (
                            mask > 4
                            || mask == 3
                            || (((((data[(UInt32)(mask >> 1) + 1])) + 1) & 0xFE) == 0)
                        )
                    )
                    {
                        mask = (mask >> 1) | 4;
                        pos++;
                        continue;
                    }
                }

                if ((((data[i + 4]) + 1) & 0xFE) == 0)
                {
                    UInt32 inst =
                        ((UInt32)data[i + 4] << 24)
                        | ((UInt32)data[i + 3] << 16)
                        | ((UInt32)data[i + 2] << 8)
                        | ((UInt32)data[i + 1]);
                    UInt32 cur = ip + (UInt32)pos;
                    pos += 5;

                    inst -= cur;
                    if (mask != 0)
                    {
                        UInt32 sh = (mask & 6) << 2;
                        if (((((((Byte)(inst >> (int)sh))) + 1) & 0xFE) == 0))
                        {
                            inst ^= (((UInt32)0x100 << (int)sh) - 1);
                            inst -= cur;
                        }
                        mask = 0;
                    }
                    data[i + 1] = (Byte)inst;
                    data[i + 2] = (Byte)(inst >> 8);
                    data[i + 3] = (Byte)(inst >> 16);
                    data[i + 4] = (Byte)(0 - ((inst >> 24) & 1));
                }
                else
                {
                    mask = (mask >> 1) | 4;
                    pos++;
                }
            }
        }

        public static void PowerPCConverter(byte[] data, UInt32 ip)
        {
            long i = 0;
            long size = data.Length;
            size &= ~(UInt32)3;
            ip -= 4;

            for (; ; ) // infinite loop
            {
                for (; ; ) // infinite loop
                {
                    if (i >= size)
                        return;
                    i += 4;

                    if ((data[i - 4] & 0xFC) == 0x48 && (data[i - 1] & 3) == 1)
                        break;
                }
                {
                    UInt32 inst = BitConverter.ToUInt32(data, (int)i - 4);

                    if (BitConverter.IsLittleEndian)
                    {
                        inst = Utility.SwapUINT32(inst);
                    }

                    inst -= (UInt32)(ip + i);
                    inst &= 0x03FFFFFF;
                    inst |= 0x48000000;

                    Utility.SetBigUInt32(ref data, inst, (i - 4));
                }
            }
        }

        public static void ARMConverter(byte[] data, UInt32 ip)
        {
            long i = 0;
            long size = data.Length;
            size &= ~(UInt32)3;
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
                        break;
                }

                UInt32 inst = BitConverter.ToUInt32(data, (int)i - 4);
                inst <<= 2;
                inst -= (UInt32)(ip + i);
                inst >>= 2;
                inst &= 0x00FFFFFF;
                inst |= 0xEB000000;

                Utility.SetLittleUInt32(ref data, inst, i - 4);
            }
        }

        public static void ARMTConverter(byte[] data, UInt32 ip)
        {
            long i = 0;
            long size = data.Length;
            size &= ~(UInt32)1;
            long lim = size - 4;

            for (; ; )
            {
                UInt32 b1;
                for (; ; )
                {
                    UInt32 b3;
                    if (i > lim)
                        return;
                    b1 = data[i + 1];
                    b3 = data[i + 3];
                    i += 2;
                    b1 ^= 8;
                    if ((b3 & b1) >= 0xF8)
                        break;
                }

                UInt32 inst =
                    ((UInt32)b1 << 19)
                    + (((UInt32)data[i + 1] & 0x7) << 8)
                    + (((UInt32)data[i - 2] << 11))
                    + (data[i]);

                i += 2;

                UInt32 cur = ((UInt32)(ip + i)) >> 1;
                inst -= cur;

                data[i - 4] = (Byte)(inst >> 11);
                data[i - 3] = (Byte)(0xF0 | ((inst >> 19) & 0x7));
                data[i - 2] = (Byte)inst;
                data[i - 1] = (Byte)(0xF8 | (inst >> 8));
            }
        }

        public static void IA64Converter(byte[] data, UInt32 ip)
        {
            UInt32 i = 0;
            long size = data.Length;
            if (size < 16)
                throw new InvalidDataException("Unexpected data size");
            size -= 16;

            do
            {
                UInt32 m = ((UInt32)0x334B0000 >> (data[i] & 0x1E)) & 3;
                if (m != 0)
                {
                    m++;
                    do
                    {
                        UInt32 iterator = (UInt32)((i + (m * 5) - 8));
                        if (
                            ((data[iterator + 3] >> (int)m) & 15) == 5
                            && (
                                ((data[iterator - 1] | ((UInt32)data[iterator] << 8)) >> (int)m)
                                & 0x70
                            ) == 0
                        )
                        {
                            UInt32 raw = BitConverter.ToUInt32(data, (int)iterator);
                            UInt32 inst = raw >> (int)m;
                            inst = (inst & 0xFFFFF) | ((inst & (1 << 23)) >> 3);

                            inst <<= 4;
                            inst -= (ip + (UInt32)i);
                            inst >>= 4;

                            inst &= 0x1FFFFF;
                            inst += 0x700000;
                            inst &= 0x8FFFFF;
                            raw &= ~((UInt32)0x8FFFFF << (int)m);
                            raw |= (inst << (int)m);

                            Utility.SetLittleUInt32(ref data, raw, iterator);
                        }
                    } while (++m <= 4);
                }
                i += 16;
            } while (i <= size);
            return;
        }

        public static void SPARCConverter(byte[] data, UInt32 ip)
        {
            long i = 0;
            long size = data.Length;
            size &= ~(UInt32)3;
            ip -= 4;

            for (; ; ) // infinite loop
            {
                for (; ; ) // infinite loop
                {
                    if (i >= size)
                        return;

                    i += 4;
                    if (
                        (data[i - 4] == 0x40 && (data[i - 3] & 0xC0) == 0)
                        || (data[i - 4] == 0x7F && (data[i - 3] >= 0xC0))
                    )
                        break;
                }

                UInt32 inst = BitConverter.ToUInt32(data, (int)i - 4);

                if (BitConverter.IsLittleEndian)
                {
                    inst = Utility.SwapUINT32(inst);
                }

                inst <<= 2;
                inst -= (UInt32)(ip + i);

                inst &= 0x01FFFFFF;
                inst -= (UInt32)1 << 24;
                inst ^= 0xFF000000;
                inst >>= 2;
                inst |= 0x40000000;

                Utility.SetBigUInt32(ref data, inst, (i - 4));
            }
        }
    }
}
