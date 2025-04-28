using System;
using System.IO;

public class BitReader
{
    private readonly Stream _stream;
    private int _bitBuffer;
    private int _bitCount;

    public BitReader(Stream stream)
    {
        _stream = stream;
        _bitBuffer = 0;
        _bitCount = 0;
    }

    public bool ReadBit()
    {
        if (_bitCount == 0)
        {
            int nextByte = _stream.ReadByte();
            if (nextByte == -1)
                throw new EndOfStreamException();
            _bitBuffer = nextByte;
            _bitCount = 8;
        }

        bool bit = (_bitBuffer & 1) != 0;
        _bitBuffer >>= 1;
        _bitCount--;
        return bit;
    }

    public int ReadBits(int count)
    {
        if (count < 1 || count > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 32.");
        }

        int value = 0;
        for (int i = 0; i < count; i++)
        {
            value = (value << 1) | (ReadBit() ? 1 : 0);
        }
        return value;
    }
}
