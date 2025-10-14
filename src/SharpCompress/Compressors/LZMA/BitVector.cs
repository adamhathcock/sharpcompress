using System;
using System.Collections.Generic;
using System.Text;

namespace SharpCompress.Compressors.LZMA;

internal class BitVector
{
    private readonly uint[] _mBits;

    public BitVector(int length)
    {
        Length = length;
        _mBits = new uint[(length + 31) >> 5];
    }

    public BitVector(int length, bool initValue)
    {
        Length = length;
        _mBits = new uint[(length + 31) >> 5];

        if (initValue)
        {
            for (var i = 0; i < _mBits.Length; i++)
            {
                _mBits[i] = ~0u;
            }
        }
    }

    public BitVector(List<bool> bits)
        : this(bits.Count)
    {
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                SetBit(i);
            }
        }
    }

    public bool[] ToArray()
    {
        var bits = new bool[Length];
        for (var i = 0; i < bits.Length; i++)
        {
            bits[i] = this[i];
        }
        return bits;
    }

    public int Length { get; }

    public bool this[int index]
    {
        get
        {
            if (index < 0 || index >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (_mBits[index >> 5] & (1u << (index & 31))) != 0;
        }
    }

    public void SetBit(int index)
    {
        if (index < 0 || index >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _mBits[index >> 5] |= 1u << (index & 31);
    }

    internal bool GetAndSet(int index)
    {
        if (index < 0 || index >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var bits = _mBits[index >> 5];
        var mask = 1u << (index & 31);
        _mBits[index >> 5] |= mask;
        return (bits & mask) != 0;
    }

    public override string ToString()
    {
        var sb = new StringBuilder(Length);
        for (var i = 0; i < Length; i++)
        {
            sb.Append(this[i] ? 'x' : '.');
        }
        return sb.ToString();
    }
}
