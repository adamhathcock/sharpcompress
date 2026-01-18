using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

internal class AsyncMarkingBinaryReader : IDisposable
{
    private readonly AsyncBinaryReader _asyncReader;
    private readonly bool _leaveOpen;
    private bool _disposed;

    public AsyncMarkingBinaryReader(Stream stream, CancellationToken cancellationToken = default)
    {
        _asyncReader = new AsyncBinaryReader(stream, leaveOpen: false);
    }

    public Stream BaseStream => _asyncReader.BaseStream;

    public virtual long CurrentReadByteCount { get; protected set; }

    public virtual void Mark() => CurrentReadByteCount = 0;

    public virtual async ValueTask<bool> ReadBooleanAsync(CancellationToken ct = default)
    {
        var b = await ReadByteAsync(ct).ConfigureAwait(false);
        return b != 0;
    }

    public virtual async ValueTask<byte> ReadByteAsync(CancellationToken ct = default)
    {
        CurrentReadByteCount++;
        return await _asyncReader.ReadByteAsync(ct).ConfigureAwait(false);
    }

    public virtual async ValueTask<byte[]> ReadBytesAsync(int count, CancellationToken ct = default)
    {
        CurrentReadByteCount += count;
        var buffer = new byte[count];
        await _asyncReader.ReadBytesAsync(buffer, 0, count, ct).ConfigureAwait(false);
        return buffer;
    }

    public virtual async ValueTask<short> ReadInt16Async(CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(2, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt16LittleEndian(bytes);
    }

    public virtual async ValueTask<int> ReadInt32Async(CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(4, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    public virtual async ValueTask<long> ReadInt64Async(CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(8, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    public virtual async ValueTask<sbyte> ReadSByteAsync(CancellationToken ct = default)
    {
        var b = await ReadByteAsync(ct).ConfigureAwait(false);
        return (sbyte)b;
    }

    public virtual async ValueTask<ushort> ReadUInt16Async(CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(2, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public virtual async ValueTask<uint> ReadUInt32Async(CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(4, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public virtual async ValueTask<ulong> ReadUInt64Async(CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(8, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    public virtual async ValueTask<ulong> ReadRarVIntAsync(
        int maxBytes = 10,
        CancellationToken ct = default
    ) => await DoReadRarVIntAsync((maxBytes - 1) * 7, ct).ConfigureAwait(false);

    private async ValueTask<ulong> DoReadRarVIntAsync(int maxShift, CancellationToken ct)
    {
        var shift = 0;
        ulong result = 0;
        do
        {
            var b0 = await ReadByteAsync(ct).ConfigureAwait(false);
            var b1 = ((uint)b0) & 0x7f;
            ulong n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new FormatException("malformed vint");
    }

    public virtual async ValueTask<uint> ReadRarVIntUInt32Async(
        int maxBytes = 5,
        CancellationToken ct = default
    ) => await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, ct).ConfigureAwait(false);

    private async ValueTask<uint> DoReadRarVIntUInt32Async(int maxShift, CancellationToken ct)
    {
        var shift = 0;
        uint result = 0;
        do
        {
            var b0 = await ReadByteAsync(ct).ConfigureAwait(false);
            var b1 = ((uint)b0) & 0x7f;
            var n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new FormatException("malformed vint");
    }

    public virtual async ValueTask<ushort> ReadRarVIntUInt16Async(
        int maxBytes = 3,
        CancellationToken ct = default
    ) =>
        checked(
            (ushort)await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, ct).ConfigureAwait(false)
        );

    public virtual async ValueTask<byte> ReadRarVIntByteAsync(
        int maxBytes = 2,
        CancellationToken ct = default
    ) =>
        checked((byte)await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, ct).ConfigureAwait(false));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _asyncReader.Dispose();
    }

#if NET6_0_OR_GREATER
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _asyncReader.DisposeAsync().ConfigureAwait(false);
    }
#endif
}
