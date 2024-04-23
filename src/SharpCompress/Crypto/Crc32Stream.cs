#nullable disable

using System;
using System.IO;

namespace SharpCompress.Crypto;

[CLSCompliant(false)]
public sealed class Crc32Stream(Stream stream, uint polynomial, uint seed) : Stream
{
    public const uint DEFAULT_POLYNOMIAL = 0xedb88320u;
    public const uint DEFAULT_SEED = 0xffffffffu;

    private static uint[] _defaultTable;

    private readonly uint[] _table = InitializeTable(polynomial);

    public Crc32Stream(Stream stream)
        : this(stream, DEFAULT_POLYNOMIAL, DEFAULT_SEED) { }

    public Stream WrappedStream => stream;

    public override void Flush() => stream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

#if !NETFRAMEWORK && !NETSTANDARD2_0

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        stream.Write(buffer);

        seed = CalculateCrc(_table, seed, buffer);
    }
#endif

    public override void Write(byte[] buffer, int offset, int count)
    {
        stream.Write(buffer, offset, count);
        seed = CalculateCrc(_table, seed, buffer.AsSpan(offset, count));
    }

    public override void WriteByte(byte value)
    {
        stream.WriteByte(value);
        seed = CalculateCrc(_table, seed, value);
    }

    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public uint Crc => ~seed;

    public static uint Compute(byte[] buffer) => Compute(DEFAULT_SEED, buffer);

    public static uint Compute(uint seed, byte[] buffer) =>
        Compute(DEFAULT_POLYNOMIAL, seed, buffer);

    public static uint Compute(uint polynomial, uint seed, ReadOnlySpan<byte> buffer) =>
        ~CalculateCrc(InitializeTable(polynomial), seed, buffer);

    private static uint[] InitializeTable(uint polynomial)
    {
        if (polynomial == DEFAULT_POLYNOMIAL && _defaultTable != null)
        {
            return _defaultTable;
        }

        var createTable = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var entry = (uint)i;
            for (var j = 0; j < 8; j++)
            {
                if ((entry & 1) == 1)
                {
                    entry = (entry >> 1) ^ polynomial;
                }
                else
                {
                    entry >>= 1;
                }
            }

            createTable[i] = entry;
        }

        if (polynomial == DEFAULT_POLYNOMIAL)
        {
            _defaultTable = createTable;
        }

        return createTable;
    }

    private static uint CalculateCrc(uint[] table, uint crc, ReadOnlySpan<byte> buffer)
    {
        unchecked
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                crc = CalculateCrc(table, crc, buffer[i]);
            }
        }
        return crc;
    }

    private static uint CalculateCrc(uint[] table, uint crc, byte b) =>
        (crc >> 8) ^ table[(crc ^ b) & 0xFF];
}
