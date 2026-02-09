using System.IO;
using System.Runtime.CompilerServices;

namespace SharpCompress.Compressors.LZMA.RangeCoder;

internal partial class Encoder
{
    public const uint K_TOP_VALUE = (1 << 24);

    private Stream? _stream;

    public ulong _low;
    public uint _range;
    private uint _cacheSize;
    private byte _cache;

    //long StartPosition;

    public void SetStream(Stream stream) => _stream = stream;

    public void ReleaseStream() => _stream = null;

    private Stream Stream => _stream.NotNull();

    public void Init()
    {
        //StartPosition = Stream.Position;

        _low = 0;
        _range = 0xFFFFFFFF;
        _cacheSize = 1;
        _cache = 0;
    }

    public void FlushData()
    {
        for (var i = 0; i < 5; i++)
        {
            ShiftLow();
        }
    }

    public void FlushStream() => Stream.Flush();

    public void CloseStream() => Stream.Dispose();

    public void ShiftLow()
    {
        if ((uint)_low < 0xFF000000 || (uint)(_low >> 32) == 1)
        {
            var temp = _cache;
            do
            {
                Stream.WriteByte((byte)(temp + (_low >> 32)));
                temp = 0xFF;
            } while (--_cacheSize != 0);
            _cache = (byte)(((uint)_low) >> 24);
        }
        _cacheSize++;
        _low = ((uint)_low) << 8;
    }

    public void EncodeDirectBits(uint v, int numTotalBits)
    {
        for (var i = numTotalBits - 1; i >= 0; i--)
        {
            _range >>= 1;
            if (((v >> i) & 1) == 1)
            {
                _low += _range;
            }
            if (_range < K_TOP_VALUE)
            {
                _range <<= 8;
                ShiftLow();
            }
        }
    }

    public long GetProcessedSizeAdd() => -1;
}

internal partial class Decoder
{
    public const uint K_TOP_VALUE = (1 << 24);
    public uint _range;
    public uint _code;

    public Stream? _stream;
    public long _total;

    public void Init(Stream stream)
    {
        _stream = stream;

        _code = 0;
        _range = 0xFFFFFFFF;
        for (var i = 0; i < 5; i++)
        {
            _code = (_code << 8) | (byte)stream.ReadByte();
        }
        _total = 5;
    }

    public void ReleaseStream() =>
        // Stream.ReleaseStream();
        _stream = null;

    private Stream Stream => _stream.NotNull();

    public void Normalize()
    {
        while (_range < K_TOP_VALUE)
        {
            _code = (_code << 8) | (byte)Stream.ReadByte();
            _range <<= 8;
            _total++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize2()
    {
        if (_range < K_TOP_VALUE)
        {
            _code = (_code << 8) | (byte)Stream.ReadByte();
            _range <<= 8;
            _total++;
        }
    }

    public uint GetThreshold(uint total) => _code / (_range /= total);

    public void Decode(uint start, uint size)
    {
        _code -= start * _range;
        _range *= size;
        Normalize();
    }

    public uint DecodeDirectBits(int numTotalBits)
    {
        var range = _range;
        var code = _code;
        uint result = 0;
        for (var i = numTotalBits; i > 0; i--)
        {
            range >>= 1;
            var t = (code - range) >> 31;
            code -= range & (t - 1);
            result = (result << 1) | (1 - t);

            if (range < K_TOP_VALUE)
            {
                code = (code << 8) | (byte)Stream.ReadByte();
                range <<= 8;
                _total++;
            }
        }
        _range = range;
        _code = code;
        return result;
    }

    public uint DecodeBit(uint size0, int numTotalBits)
    {
        var newBound = (_range >> numTotalBits) * size0;
        uint symbol;
        if (_code < newBound)
        {
            symbol = 0;
            _range = newBound;
        }
        else
        {
            symbol = 1;
            _code -= newBound;
            _range -= newBound;
        }
        Normalize();
        return symbol;
    }

    public bool IsFinished => _code == 0;

    // ulong GetProcessedSize() {return Stream.GetProcessedSize(); }
}
