using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Crypto;

[CLSCompliant(false)]
public sealed class Crc32Stream : Stream
{
    private readonly Stream stream;
    private readonly uint[] _table;
    private uint seed;

    public const uint DEFAULT_POLYNOMIAL = 0xedb88320u;
    public const uint DEFAULT_SEED = 0xffffffffu;

    private static uint[]? _defaultTable;

    public Crc32Stream(Stream stream)
        : this(stream, DEFAULT_POLYNOMIAL, DEFAULT_SEED) { }

    public Crc32Stream(Stream stream, uint polynomial, uint seed)
    {
        this.stream = stream;
        _table = InitializeTable(polynomial);
        this.seed = seed;
    }

    public Stream WrappedStream => stream;

    public override void Flush() => stream.Flush();

    public override async Task FlushAsync(CancellationToken cancellationToken) =>
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

#if !LEGACY_DOTNET

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

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        seed = CalculateCrc(_table, seed, buffer.AsSpan(offset, count));
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        seed = CalculateCrc(_table, seed, buffer.Span);
    }
#endif

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

    // Number of "slice-by-N" tables generated per polynomial (see InitializeTable). 16 gives a
    // good throughput/table-size tradeoff (16 * 256 * 4 bytes = 16KB per polynomial, generated
    // once and cached for the default polynomial) - a well-known technique (zlib-ng, Chromium,
    // etc.) for speeding up table-driven CRC32 by processing 16 bytes/iteration through 16
    // independent (parallelizable) table lookups instead of 1 byte/iteration through a single
    // lookup on the CPU's dependency chain.
    private const int SliceTableCount = 16;

    internal static uint[] InitializeTable(uint polynomial)
    {
        if (polynomial == DEFAULT_POLYNOMIAL && _defaultTable != null)
        {
            return _defaultTable;
        }

        // table[0..255] is the ordinary byte-wise CRC table (unchanged layout, so existing
        // single-byte callers - CalculateCrc(table, crc, byte) - keep working unmodified).
        // table[256*k .. 256*k+255] for k=1..SliceTableCount-1 are the derived "slice" tables:
        // table[k][i] = (table[k-1][i] >> 8) ^ table[0][table[k-1][i] & 0xFF], i.e. each table
        // folds in the effect of a byte that is k positions further back in the stream than
        // the byte table[0] would have processed - letting CalculateCrc(span) below combine 4
        // bytes' worth of independent lookups per 32-bit word with no cross-lookup dependency.
        var table = new uint[256 * SliceTableCount];
        for (var i = 0; i < 256; i++)
        {
            var entry = (uint)i;
            for (var j = 0; j < 8; j++)
            {
                entry = (entry & 1) == 1 ? (entry >> 1) ^ polynomial : entry >> 1;
            }
            table[i] = entry;
        }

        for (var k = 1; k < SliceTableCount; k++)
        {
            var prevBase = (k - 1) * 256;
            var curBase = k * 256;
            for (var i = 0; i < 256; i++)
            {
                var prev = table[prevBase + i];
                table[curBase + i] = (prev >> 8) ^ table[prev & 0xFF];
            }
        }

        if (polynomial == DEFAULT_POLYNOMIAL)
        {
            _defaultTable = table;
        }

        return table;
    }

    internal static uint CalculateCrc(uint[] table, uint crc, ReadOnlySpan<byte> buffer)
    {
        unchecked
        {
            var i = 0;
            if (table.Length >= 256 * SliceTableCount)
            {
                while (buffer.Length - i >= 16)
                {
                    var word0 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i)) ^ crc;
                    var word1 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i + 4));
                    var word2 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i + 8));
                    var word3 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i + 12));

                    crc =
                        table[15 * 256 + (byte)word0]
                        ^ table[14 * 256 + (byte)(word0 >> 8)]
                        ^ table[13 * 256 + (byte)(word0 >> 16)]
                        ^ table[12 * 256 + (word0 >> 24)]
                        ^ table[11 * 256 + (byte)word1]
                        ^ table[10 * 256 + (byte)(word1 >> 8)]
                        ^ table[9 * 256 + (byte)(word1 >> 16)]
                        ^ table[8 * 256 + (word1 >> 24)]
                        ^ table[7 * 256 + (byte)word2]
                        ^ table[6 * 256 + (byte)(word2 >> 8)]
                        ^ table[5 * 256 + (byte)(word2 >> 16)]
                        ^ table[4 * 256 + (word2 >> 24)]
                        ^ table[3 * 256 + (byte)word3]
                        ^ table[2 * 256 + (byte)(word3 >> 8)]
                        ^ table[1 * 256 + (byte)(word3 >> 16)]
                        ^ table[word3 >> 24];

                    i += 16;
                }
            }

            for (; i < buffer.Length; i++)
            {
                crc = CalculateCrc(table, crc, buffer[i]);
            }
        }
        return crc;
    }

    internal static uint CalculateCrc(uint[] table, uint crc, byte b) =>
        (crc >> 8) ^ table[(crc ^ b) & 0xFF];
}
