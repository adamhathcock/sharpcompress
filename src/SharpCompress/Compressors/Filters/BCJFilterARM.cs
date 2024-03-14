using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterARM : Filter
{
    private int _pos;

    public BCJFilterARM(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 8) => _pos = 8;

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        var end = offset + count - 4;
        int i;

        for (i = offset; i <= end; i += 4)
        {
            if ((buffer[i + 3] & 0xFF) == 0xEB)
            {
                var src =
                    ((buffer[i + 2] & 0xFF) << 16)
                    | ((buffer[i + 1] & 0xFF) << 8)
                    | (buffer[i] & 0xFF);

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
                buffer[i + 2] = (byte)(dest >>> 16);
                buffer[i + 1] = (byte)(dest >>> 8);
                buffer[i] = (byte)dest;
            }
        }

        i -= offset;
        _pos += i;
        return i;
    }
}
