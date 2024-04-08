using System;
using System.Buffers.Binary;
using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterRISCV : Filter
{
    private int _pos;

    public BCJFilterRISCV(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 8) => _pos = 0;

    private int Decode(byte[] buffer, int offset, int count)
    {
        if (count < 8)
        {
            return 0;
        }

        var end = offset + count - 8;
        int i;
        for (i = offset; i <= end; i += 2)
        {
            uint inst = buffer[i];
            if (inst == 0xEF)
            {
                uint b1 = buffer[i + 1];
                if ((b1 & 0x0D) != 0)
                    continue;

                uint b2 = buffer[i + 2];
                uint b3 = buffer[i + 3];
                uint pc = (uint)(_pos + i);

                uint addr = ((b1 & 0xF0) << 13) | (b2 << 9) | (b3 << 1);

                addr -= pc;

                buffer[i + 1] = (byte)((b1 & 0x0F) | ((addr >> 8) & 0xF0));

                buffer[i + 2] = (byte)(
                    ((addr >> 16) & 0x0F) | ((addr >> 7) & 0x10) | ((addr << 4) & 0xE0)
                );

                buffer[i + 3] = (byte)(((addr >> 4) & 0x7F) | ((addr >> 13) & 0x80));

                i += 4 - 2;
            }
            else if ((inst & 0x7F) == 0x17)
            {
                uint inst2 = 0;
                inst |= (uint)buffer[i + 1] << 8;
                inst |= (uint)buffer[i + 2] << 16;
                inst |= (uint)buffer[i + 3] << 24;

                if ((inst & 0xE80) != 0)
                {
                    inst2 = BinaryPrimitives.ReadUInt32LittleEndian(
                        new ReadOnlySpan<byte>(buffer, i + 4, 4)
                    );
                    if (((((inst) << 8) ^ (inst2)) & 0xF8003) != 3)
                    {
                        i += 6 - 2;
                        continue;
                    }
                    uint addr = inst & 0xFFFFF000;
                    addr += inst2 >> 20;

                    inst = 0x17 | (2 << 7) | (inst2 << 12);
                    inst2 = addr;
                }
                else
                {
                    uint inst2_rs1 = inst >> 27;
                    if ((uint)(((inst) - 0x3117) << 18) >= ((inst2_rs1) & 0x1D))
                    {
                        i += 4 - 2;
                        continue;
                    }

                    uint addr = BinaryPrimitives.ReadUInt32BigEndian(
                        new ReadOnlySpan<byte>(buffer, i + 4, 4)
                    );

                    addr -= (uint)(_pos + i);

                    inst2 = (inst >> 12) | (addr << 20);

                    inst = 0x17 | (inst2_rs1 << 7) | ((addr + 0x800) & 0xFFFFF000);
                }
                BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, i, 4), inst);
                BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, i + 4, 4), inst2);

                i += 8 - 2;
            }
        }
        i -= offset;
        _pos += i;
        return i;
    }

    private int Encode(byte[] buffer, int offset, int count)
    {
        if (count < 8)
        {
            return 0;
        }

        var end = offset + count - 8;
        int i;
        for (i = offset; i <= end; i += 2)
        {
            uint inst = buffer[i];
            if (inst == 0xEF)
            {
                uint b1 = buffer[i + 1];
                if ((b1 & 0x0D) != 0)
                    continue;

                uint b2 = buffer[i + 2];
                uint b3 = buffer[i + 3];
                uint pc = (uint)(_pos + i);

                uint addr =
                    ((b1 & 0xF0) << 8)
                    | ((b2 & 0x0F) << 16)
                    | ((b2 & 0x10) << 7)
                    | ((b2 & 0xE0) >> 4)
                    | ((b3 & 0x7F) << 4)
                    | ((b3 & 0x80) << 13);

                addr += pc;

                buffer[i + 1] = (byte)((b1 & 0x0F) | ((addr >> 13) & 0xF0));

                buffer[i + 2] = (byte)(addr >> 9);

                buffer[i + 3] = (byte)(addr >> 1);

                i += 4 - 2;
            }
            else if ((inst & 0x7F) == 0x17)
            {
                inst |= (uint)buffer[i + 1] << 8;
                inst |= (uint)buffer[i + 2] << 16;
                inst |= (uint)buffer[i + 3] << 24;

                if ((inst & 0xE80) != 0)
                {
                    uint inst2 = BinaryPrimitives.ReadUInt32LittleEndian(
                        new ReadOnlySpan<byte>(buffer, i + 4, 4)
                    );
                    if (((((inst) << 8) ^ (inst2)) & 0xF8003) != 3)
                    {
                        i += 6 - 2;
                        continue;
                    }
                    uint addr = inst & 0xFFFFF000;
                    addr += (inst2 >> 20) - ((inst2 >> 19) & 0x1000);

                    addr += (uint)(_pos + i);
                    inst = 0x17 | (2 << 7) | (inst2 << 12);

                    BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, i, 4), inst);
                    BinaryPrimitives.WriteUInt32BigEndian(new Span<byte>(buffer, i + 4, 4), addr);
                }
                else
                {
                    uint fake_rs1 = inst >> 27;
                    if ((uint)(((inst) - 0x3117) << 18) >= ((fake_rs1) & 0x1D))
                    {
                        i += 4 - 2;
                        continue;
                    }

                    uint fake_addr = BinaryPrimitives.ReadUInt32LittleEndian(
                        new ReadOnlySpan<byte>(buffer, i + 4, 4)
                    );

                    uint fake_inst2 = (inst >> 12) | (fake_addr << 20);

                    inst = 0x17 | (fake_rs1 << 7) | (fake_addr & 0xFFFFF000);

                    BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, i, 4), inst);
                    BinaryPrimitives.WriteUInt32LittleEndian(
                        new Span<byte>(buffer, i + 4, 4),
                        fake_inst2
                    );
                }
                i += 8 - 2;
            }
        }
        i -= offset;
        _pos += i;
        return i;
    }

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        if (_isEncoder)
        {
            return Encode(buffer, offset, count);
        }
        else
        {
            return Decode(buffer, offset, count);
        }
    }
}
