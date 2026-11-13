using System;
using System.IO;
using System.Text;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

internal class DataReader
{
    #region Static Methods

    public static uint Get32(byte[] buffer, int offset) =>
        buffer[offset]
        + ((uint)buffer[offset + 1] << 8)
        + ((uint)buffer[offset + 2] << 16)
        + ((uint)buffer[offset + 3] << 24);

    public static ulong Get64(byte[] buffer, int offset) =>
        buffer[offset]
        + ((ulong)buffer[offset + 1] << 8)
        + ((ulong)buffer[offset + 2] << 16)
        + ((ulong)buffer[offset + 3] << 24)
        + ((ulong)buffer[offset + 4] << 32)
        + ((ulong)buffer[offset + 5] << 40)
        + ((ulong)buffer[offset + 6] << 48)
        + ((ulong)buffer[offset + 7] << 56);

    #endregion

    #region Variables

    private readonly byte[]? _buffer;
    private readonly PooledMemoryStream? _pooledBuffer;
    private readonly int _ending;

    #endregion

    #region Public Methods

    public DataReader(byte[] buffer, int offset, int length)
    {
        _buffer = buffer;
        Offset = offset;
        _ending = offset + length;
    }

    public DataReader(PooledMemoryStream buffer)
    {
        _pooledBuffer = buffer;
        _ending = checked((int)buffer.Length);
    }

    private DataReader(DataReader source)
    {
        _buffer = source._buffer;
        _pooledBuffer = source._pooledBuffer;
        _ending = source._ending;
        Offset = source.Offset;
    }

    public int Offset { get; private set; }

    public int Remaining => _ending - Offset;

    public DataReader Clone() => new(this);

    private byte GetByte(int offset) =>
        _buffer is not null ? _buffer[offset] : _pooledBuffer!.ReadByteAt(offset);

    public void RequireBytes(long count)
    {
        if (count < 0 || count > Remaining)
        {
            throw new IncompleteArchiveException("Unexpected end of stream.");
        }
    }

    public byte ReadByte()
    {
        if (Offset >= _ending)
        {
            throw new IncompleteArchiveException("Unexpected end of stream.");
        }

        return GetByte(Offset++);
    }

    public void ReadBytes(byte[] buffer, int offset, int length)
    {
        RequireBytes(length);

        if (_pooledBuffer is not null)
        {
            _pooledBuffer.ReadAt(Offset, buffer, offset, length);
            Offset += length;
            return;
        }
        while (length-- > 0)
        {
            buffer[offset++] = _buffer![Offset++];
        }
    }

    public void SkipData(long size)
    {
        RequireBytes(size);

        Offset += (int)size;
    }

    public void SkipData() => SkipData(checked((long)ReadNumber()));

    public ulong ReadNumber()
    {
        if (Offset >= _ending)
        {
            throw new IncompleteArchiveException("Unexpected end of stream.");
        }

        var firstByte = GetByte(Offset++);
        byte mask = 0x80;
        ulong value = 0;

        for (var i = 0; i < 8; i++)
        {
            if ((firstByte & mask) == 0)
            {
                ulong highPart = firstByte & (mask - 1u);
                value += highPart << (i * 8);
                return value;
            }

            if (Offset >= _ending)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }

            value |= (ulong)GetByte(Offset++) << (8 * i);
            mask >>= 1;
        }

        return value;
    }

    public int ReadNum()
    {
        var value = ReadNumber();
        if (value > int.MaxValue)
        {
            throw new NotSupportedException();
        }

        return (int)value;
    }

    public uint ReadUInt32()
    {
        RequireBytes(4);
        if (_buffer is not null)
        {
            var res = Get32(_buffer, Offset);
            Offset += 4;
            return res;
        }
        uint value = 0;
        for (var i = 0; i < 4; i++)
        {
            value |= (uint)ReadByte() << (8 * i);
        }
        return value;
    }

    public ulong ReadUInt64()
    {
        RequireBytes(8);
        if (_buffer is not null)
        {
            var res = Get64(_buffer, Offset);
            Offset += 8;
            return res;
        }
        ulong value = 0;
        for (var i = 0; i < 8; i++)
        {
            value |= (ulong)ReadByte() << (8 * i);
        }
        return value;
    }

    public string ReadString()
    {
        var ending = Offset;

        for (; ; )
        {
            if (_ending - ending < 2)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }

            if (GetByte(ending) == 0 && GetByte(ending + 1) == 0)
            {
                break;
            }

            ending += 2;
        }

        var str = _buffer is not null
            ? Encoding.Unicode.GetString(_buffer, Offset, ending - Offset)
            : ReadPooledString(ending - Offset);
        Offset = ending + 2;
        return str;
    }

    private string ReadPooledString(int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }
        // Decode bounded chunks, preserving surrogate pairs across block boundaries,
        // rather than copying a potentially header-sized name into a temporary array.
        var bytes = new byte[Math.Min(length, 256)];
        var chars = new char[Encoding.Unicode.GetMaxCharCount(bytes.Length)];
        var decoder = Encoding.Unicode.GetDecoder();
        var result = new StringBuilder();
        for (var offset = 0; offset < length; )
        {
            var count = Math.Min(bytes.Length, length - offset);
            _pooledBuffer!.ReadAt(Offset + offset, bytes, 0, count);
            var charCount = decoder.GetChars(bytes, 0, count, chars, 0, offset + count == length);
            result.Append(chars, 0, charCount);
            offset += count;
        }
        return result.ToString();
    }

    #endregion
}
