using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterSPARC : Filter
{
    private int _pos;

    public BCJFilterSPARC(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 4) => _pos = 0;

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        var end = offset + count - 4;
        int i;

        for (i = offset; i <= end; i += 4)
        {
            if (
                (buffer[i] == 0x40 && (buffer[i + 1] & 0xC0) == 0x00)
                || (buffer[i] == 0x7F && (buffer[i + 1] & 0xC0) == 0xC0)
            )
            {
                var src =
                    ((buffer[i] & 0xFF) << 24)
                    | ((buffer[i + 1] & 0xFF) << 16)
                    | ((buffer[i + 2] & 0xFF) << 8)
                    | (buffer[i + 3] & 0xFF);
                src <<= 2;

                int dest;
                if (_isEncoder)
                {
                    dest = src + (_pos + i - offset);
                }
                else
                {
                    dest = src - (_pos + i - offset);
                }

                dest >>>= 2;
                dest =
                    (((0 - ((dest >>> 22) & 1)) << 22) & 0x3FFFFFFF)
                    | (dest & 0x3FFFFF)
                    | 0x40000000;

                buffer[i] = (byte)(dest >>> 24);
                buffer[i + 1] = (byte)(dest >>> 16);
                buffer[i + 2] = (byte)(dest >>> 8);
                buffer[i + 3] = (byte)dest;
            }
        }

        i -= offset;
        _pos += i;
        return i;
    }
}
