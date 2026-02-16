using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal class AsyncMarkingBinaryReader : IDisposable
#if NET8_0_OR_GREATER
        , IAsyncDisposable
#endif
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
        await _reader.ReadBytesAsync(bytes, 0, count, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    public async ValueTask<ushort> ReadUInt16Async(CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(2, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public async ValueTask<uint> ReadUInt32Async(CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(4, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public virtual async ValueTask<ulong> ReadUInt64Async(
        CancellationToken cancellationToken = default
    )
    {
        var bytes = await ReadBytesAsync(8, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    public virtual async ValueTask<short> ReadInt16Async(
        CancellationToken cancellationToken = default
    )
    {
        var bytes = await ReadBytesAsync(2, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt16LittleEndian(bytes);
    }

    public virtual async ValueTask<int> ReadInt32Async(
        CancellationToken cancellationToken = default
    )
    {
        var bytes = await ReadBytesAsync(4, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    public virtual async ValueTask<long> ReadInt64Async(
        CancellationToken cancellationToken = default
    )
    {
        var bytes = await ReadBytesAsync(8, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    public async ValueTask<ulong> ReadRarVIntAsync(
        int maxBytes = 10,
        CancellationToken cancellationToken = default
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
            var b0 = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var b1 = ((uint)b0) & 0x7f;
            ulong n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                // overflow
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new InvalidFormatException("malformed vint");
    }

    public async ValueTask<uint> ReadRarVIntUInt32Async(
        int maxBytes = 5,
        CancellationToken cancellationToken = default
    ) =>
        // hopefully this gets inlined
        await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, cancellationToken).ConfigureAwait(false);

    public async ValueTask<ushort> ReadRarVIntUInt16Async(
        int maxBytes = 3,
        CancellationToken cancellationToken = default
    ) =>
        // hopefully this gets inlined
        checked(
            (ushort)
                await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, cancellationToken)
                    .ConfigureAwait(false)
        );

    public async ValueTask<byte> ReadRarVIntByteAsync(
        int maxBytes = 2,
        CancellationToken cancellationToken = default
    ) =>
        // hopefully this gets inlined
        checked(
            (byte)
                await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, cancellationToken)
                    .ConfigureAwait(false)
        );

    public async ValueTask SkipAsync(int count, CancellationToken cancellationToken = default)
    {
        CurrentReadByteCount += count;
        await _reader.SkipAsync(count, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<uint> DoReadRarVIntUInt32Async(
        int maxShift,
        CancellationToken cancellationToken = default
    )
    {
        var shift = 0;
        uint result = 0;
        do
        {
            var b0 = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var b1 = ((uint)b0) & 0x7f;
            var n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                // overflow
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new InvalidFormatException("malformed vint");
    }

    public virtual void Dispose() => _reader.Dispose();

#if NET8_0_OR_GREATER
    public virtual ValueTask DisposeAsync() => _reader.DisposeAsync();
#endif
}
