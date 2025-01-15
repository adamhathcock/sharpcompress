using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterIA64 : Filter
{
    private int _pos;

    private static readonly int[] BRANCH_TABLE =
    {
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        4,
        4,
        6,
        6,
        0,
        0,
        7,
        7,
        4,
        4,
        0,
        0,
        4,
        4,
        0,
        0,
    };

    public BCJFilterIA64(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 16) => _pos = 0;

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        var end = offset + count - 16;
        int i;

        for (i = offset; i <= end; i += 16)
        {
            var instrTemplate = buffer[i] & 0x1F;
            var mask = BRANCH_TABLE[instrTemplate];

            for (int slot = 0, bitPos = 5; slot < 3; ++slot, bitPos += 41)
            {
                if (((mask >>> slot) & 1) == 0)
                    continue;

                var bytePos = bitPos >>> 3;
                var bitRes = bitPos & 7;

                long instr = 0;
                for (var j = 0; j < 6; ++j)
                {
                    instr |= (buffer[i + bytePos + j] & 0xFFL) << (8 * j);
                }

                var instrNorm = instr >>> bitRes;

                if (((instrNorm >>> 37) & 0x0F) != 0x05 || ((instrNorm >>> 9) & 0x07) != 0x00)
                    continue;

                var src = (int)((instrNorm >>> 13) & 0x0FFFFF);
                src |= ((int)(instrNorm >>> 36) & 1) << 20;
                src <<= 4;

                int dest;
                if (_isEncoder)
                    dest = src + (_pos + i - offset);
                else
                    dest = src - (_pos + i - offset);

                dest >>>= 4;

                instrNorm &= ~(0x8FFFFFL << 13);
                instrNorm |= (dest & 0x0FFFFFL) << 13;
                instrNorm |= (dest & 0x100000L) << (36 - 20);

                instr &= (1 << bitRes) - 1;
                instr |= instrNorm << bitRes;

                for (var j = 0; j < 6; ++j)
                {
                    buffer[i + bytePos + j] = (byte)(instr >>> (8 * j));
                }
            }
        }

        i -= offset;
        _pos += i;
        return i;
    }
}
