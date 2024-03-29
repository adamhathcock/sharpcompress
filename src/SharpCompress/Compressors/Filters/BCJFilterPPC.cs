using System.IO;

namespace SharpCompress.Compressors.Filters;

internal class BCJFilterPPC : Filter
{
    private int _pos;

    public BCJFilterPPC(bool isEncoder, Stream baseStream)
        : base(isEncoder, baseStream, 4) => _pos = 0;

    protected override int Transform(byte[] buffer, int offset, int count)
    {
        var end = offset + count - 4;
        int i;

        for (i = offset; i <= end; i += 4)
        {
            if ((buffer[i] & 0xFC) == 0x48 && (buffer[i + 3] & 0x03) == 0x01)
            {
                var src =
                    ((buffer[i] & 0x03) << 24)
                    | ((buffer[i + 1] & 0xFF) << 16)
                    | ((buffer[i + 2] & 0xFF) << 8)
                    | (buffer[i + 3] & 0xFC);

                int dest;
                if (_isEncoder)
                {
                    dest = src + (_pos + i - offset);
                }
                else
                {
                    dest = src - (_pos + i - offset);
                }

                buffer[i] = (byte)(0x48 | ((dest >>> 24) & 0x03));
                buffer[i + 1] = (byte)(dest >>> 16);
                buffer[i + 2] = (byte)(dest >>> 8);
                buffer[i + 3] = (byte)((buffer[i + 3] & 0x03) | dest);
            }
        }

        i -= offset;
        _pos += i;
        return i;
    }
}
