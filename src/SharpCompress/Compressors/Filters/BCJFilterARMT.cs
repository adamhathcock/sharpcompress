using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterARMT : Filter
{
    private int _pos;

    public BCJFilterARMT(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 4) => _pos = 4;

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        var end = offset + count - 4;
        int i;

        for (i = offset; i <= end; i += 2)
        {
            if ((buffer[i + 1] & 0xF8) == 0xF0 && (buffer[i + 3] & 0xF8) == 0xF8)
            {
                var src =
                    ((buffer[i + 1] & 0x07) << 19)
                    | ((buffer[i] & 0xFF) << 11)
                    | ((buffer[i + 3] & 0x07) << 8)
                    | (buffer[i + 2] & 0xFF);
                src <<= 1;

                int dest;
                if (_isEncoder)
                    dest = src + (_pos + i - offset);
                else
                    dest = src - (_pos + i - offset);

                dest >>>= 1;
                buffer[i + 1] = (byte)(0xF0 | ((dest >>> 19) & 0x07));
                buffer[i] = (byte)(dest >>> 11);
                buffer[i + 3] = (byte)(0xF8 | ((dest >>> 8) & 0x07));
                buffer[i + 2] = (byte)dest;
                i += 2;
            }
        }

        i -= offset;
        _pos += i;
        return i;
    }
}
