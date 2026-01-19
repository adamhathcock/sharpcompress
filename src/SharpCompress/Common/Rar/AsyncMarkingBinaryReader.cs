using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Common.Rar;

internal class AsyncMarkingBinaryReader
{
    private readonly AsyncBinaryReader _reader;

    public AsyncMarkingBinaryReader(Stream stream)
    {
        _reader = new AsyncBinaryReader(stream, leaveOpen: true);
    }

    public Stream BaseStream => _reader.BaseStream;

    public virtual long CurrentReadByteCount { get; protected set; }

    public virtual void Mark() => CurrentReadByteCount = 0;

    public virtual async ValueTask<bool> ReadBooleanAsync(
        CancellationToken cancellationToken = default
    ) => await ReadByteAsync(cancellationToken).ConfigureAwait(false) != 0;

    public virtual async ValueTask<byte> ReadByteAsync(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount++;
        return await _reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async ValueTask<byte[]> ReadBytesAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += count;
        var bytes = new byte[count];
        await _reader.ReadBytesAsync(bytes, 0, count,  cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    public async ValueTask<ushort> ReadUInt16Async(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += 2;
        var bytes = await ReadBytesAsync( 2, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public async ValueTask<uint> ReadUInt32Async(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += 4;
        var bytes =  await ReadBytesAsync( 4, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public virtual async ValueTask<ulong> ReadUInt64Async(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += 8;
        var bytes = await ReadBytesAsync( 8, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    public virtual async ValueTask<short> ReadInt16Async(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += 2;
        var bytes = await ReadBytesAsync(2, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt16LittleEndian(bytes);
    }

    public virtual async ValueTask<int> ReadInt32Async(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += 4;
        var bytes =  await ReadBytesAsync( 4, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    public virtual async ValueTask<long> ReadInt64Async(
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += 8;
        var bytes =  await ReadBytesAsync(8, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    public async ValueTask<ulong> ReadRarVIntAsync(
        CancellationToken cancellationToken = default,
        int maxBytes = 10
    ) => await DoReadRarVIntAsync((maxBytes - 1) * 7, cancellationToken).ConfigureAwait(false);

    private async ValueTask<ulong> DoReadRarVIntAsync(
        int maxShift,
        CancellationToken cancellationToken
    )
    {
        var shift = 0;
        ulong result = 0;
        do
        {
            var b = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            result |= (ulong)(b & 0x7f) << shift;
            shift += 7;
        } while (
            shift <= maxShift
            && (await ReadByteAsync(cancellationToken).ConfigureAwait(false) & 0x80) != 0
        );

        return result;
    }

    public async ValueTask<byte> ReadRarVIntByteAsync(CancellationToken cancellationToken = default)
    {
        var b = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        var flag = (b & 0x80) != 0;
        var result = (byte)(b & 0x7f);
        if (flag)
        {
            var b2 = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            result += (byte)(b2 << 7);
        }
        return result;
    }

    public async ValueTask<ushort> ReadRarVIntUInt16Async(
        CancellationToken cancellationToken = default,
        int maxBytes = 2
    )
    {
        var b = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
        var flag = (b & 0x80) != 0;
        var result = (ushort)(b & 0x7f);
        if (flag)
        {
            var b2 = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            result += (ushort)(b2 << 7);
        }
        return result;
    }

    public async ValueTask<uint> ReadRarVIntUInt32Async(
        CancellationToken cancellationToken = default,
        int maxBytes = 4
    )
    {
        var result = await ReadRarVIntAsync(cancellationToken, maxBytes).ConfigureAwait(false);
        return (uint)result;
    }

    public async ValueTask SkipAsync(int count, CancellationToken cancellationToken = default)
    {
        CurrentReadByteCount += count;
        await _reader.SkipAsync(count, cancellationToken).ConfigureAwait(false);
    }
}
