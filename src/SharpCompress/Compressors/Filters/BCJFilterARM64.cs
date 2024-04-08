using System;
using System.Buffers.Binary;
using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterARM64 : Filter
{
    private int _pos;

    public BCJFilterARM64(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 8) => _pos = 0;

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        var end = offset + count - 4;
        int i;

        for (i = offset; i <= end; i += 4)
        {
            uint pc = (uint)(_pos + i - offset);
            uint instr = BinaryPrimitives.ReadUInt32LittleEndian(
                new ReadOnlySpan<byte>(buffer, i, 4)
            );

            if ((instr >> 26) == 0x25)
            {
                uint src = instr;
                instr = 0x94000000;

                pc >>= 2;
                if (!_isEncoder)
                    pc = 0U - pc;

                instr |= (src + pc) & 0x03FFFFFF;
                BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, i, 4), instr);
            }
            else if ((instr & 0x9F000000) == 0x90000000)
            {
                uint src = ((instr >> 29) & 3) | ((instr >> 3) & 0x001FFFFC);

                if (((src + 0x00020000) & 0x001C0000) != 0)
                    continue;

                instr &= 0x9000001F;

                pc >>= 12;
                if (!_isEncoder)
                    pc = 0U - pc;

                uint dest = src + pc;
                instr |= (dest & 3) << 29;
                instr |= (dest & 0x0003FFFC) << 3;
                instr |= (0U - (dest & 0x00020000)) & 0x00E00000;
                BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, i, 4), instr);
            }
        }

        i -= offset;
        _pos += i;
        return i;
    }
}
