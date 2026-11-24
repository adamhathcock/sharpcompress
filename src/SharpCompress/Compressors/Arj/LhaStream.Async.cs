using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Arj;

public sealed partial class LhaStream<TDecoderConfig>
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ThrowHelper.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || (offset + count) > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (_producedBytes >= _originalSize)
        {
            return 0; // EOF
        }
        if (count == 0)
        {
            return 0;
        }

        return await FillBufferAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_producedBytes >= _originalSize)
        {
            return 0; // EOF
        }
        if (buffer.Length == 0)
        {
            return 0;
        }

        int bytesRead = await FillBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
        return bytesRead;
    }
#endif

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask<byte> ReadCodeLengthAsync(CancellationToken cancellationToken)
    {
        byte len = (byte)await _bitReader.ReadBitsAsync(3, cancellationToken).ConfigureAwait(false);
        if (len == 7)
        {
            while (await _bitReader.ReadBitAsync(cancellationToken).ConfigureAwait(false) != 0)
            {
                len++;
                if (len > 255)
                {
                    throw new ArchiveOperationException("Code length overflow");
                }
            }
        }
        return len;
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask<int> ReadCodeSkipAsync(
        int skipRange,
        CancellationToken cancellationToken
    )
    {
        int bits;
        int increment;

        switch (skipRange)
        {
            case 0:
                return 1;
            case 1:
                bits = 4;
                increment = 3; // 3..=18
                break;
            default:
                bits = 9;
                increment = 20; // 20..=531
                break;
        }

        int skip = await _bitReader.ReadBitsAsync(bits, cancellationToken).ConfigureAwait(false);
        return skip + increment;
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask ReadTempTreeAsync(CancellationToken cancellationToken)
    {
        byte[] codeLengths = new byte[NUM_TEMP_CODELEN];

        // number of codes to read (5 bits)
        int numCodes = await _bitReader.ReadBitsAsync(5, cancellationToken).ConfigureAwait(false);

        // single code only
        if (numCodes == 0)
        {
            int code = await _bitReader.ReadBitsAsync(5, cancellationToken).ConfigureAwait(false);
            _offsetTree.SetSingle((byte)code);
            return;
        }

        if (numCodes > NUM_TEMP_CODELEN)
        {
            throw new InvalidFormatException("temporary codelen table has invalid size");
        }

        // read actual lengths
        int count = Math.Min(3, numCodes);
        for (int i = 0; i < count; i++)
        {
            codeLengths[i] = (byte)
                await ReadCodeLengthAsync(cancellationToken).ConfigureAwait(false);
        }

        // 2-bit skip value follows
        int skip = await _bitReader.ReadBitsAsync(2, cancellationToken).ConfigureAwait(false);

        if (3 + skip > numCodes)
        {
            throw new InvalidFormatException("temporary codelen table has invalid size");
        }

        for (int i = 3 + skip; i < numCodes; i++)
        {
            codeLengths[i] = (byte)
                await ReadCodeLengthAsync(cancellationToken).ConfigureAwait(false);
        }

        _offsetTree.BuildTree(codeLengths, numCodes);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask ReadCommandTreeAsync(CancellationToken cancellationToken)
    {
        byte[] codeLengths = new byte[NUM_COMMANDS];

        // number of codes to read (9 bits)
        int numCodes = await _bitReader.ReadBitsAsync(9, cancellationToken).ConfigureAwait(false);

        // single code only
        if (numCodes == 0)
        {
            int code = await _bitReader.ReadBitsAsync(9, cancellationToken).ConfigureAwait(false);
            _commandTree.SetSingle((ushort)code);
            return;
        }

        if (numCodes > NUM_COMMANDS)
        {
            throw new InvalidFormatException("commands codelen table has invalid size");
        }

        int index = 0;
        while (index < numCodes)
        {
            for (int n = 0; n < numCodes - index; n++)
            {
                int code = await _offsetTree
                    .ReadEntryAsync(_bitReader, cancellationToken)
                    .ConfigureAwait(false);

                if (code >= 0 && code <= 2) // skip range
                {
                    int skipCount = await ReadCodeSkipAsync(code, cancellationToken)
                        .ConfigureAwait(false);
                    index += n + skipCount;
                    goto outerLoop;
                }
                else
                {
                    codeLengths[index + n] = (byte)(code - 2);
                }
            }
            break;

            outerLoop:
            ;
        }

        _commandTree.BuildTree(codeLengths, numCodes);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask ReadOffsetTreeAsync(CancellationToken cancellationToken)
    {
        int numCodes = await _bitReader
            .ReadBitsAsync(_config.OffsetBits, cancellationToken)
            .ConfigureAwait(false);
        if (numCodes == 0)
        {
            int code = await _bitReader
                .ReadBitsAsync(_config.OffsetBits, cancellationToken)
                .ConfigureAwait(false);
            _offsetTree.SetSingle(code);
            return;
        }

        if (numCodes > _config.HistoryBits)
        {
            throw new InvalidFormatException("Offset code table too large");
        }

        byte[] codeLengths = new byte[NUM_TEMP_CODELEN];
        for (int i = 0; i < numCodes; i++)
        {
            codeLengths[i] = (byte)
                await ReadCodeLengthAsync(cancellationToken).ConfigureAwait(false);
        }

        _offsetTree.BuildTree(codeLengths, numCodes);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask BeginNewBlockAsync(CancellationToken cancellationToken)
    {
        await ReadTempTreeAsync(cancellationToken).ConfigureAwait(false);
        await ReadCommandTreeAsync(cancellationToken).ConfigureAwait(false);
        await ReadOffsetTreeAsync(cancellationToken).ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private ValueTask<int> ReadCommandAsync(CancellationToken cancellationToken) =>
        _commandTree.ReadEntryAsync(_bitReader, cancellationToken);

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask<int> ReadOffsetAsync(CancellationToken cancellationToken)
    {
        int bits = await _offsetTree
            .ReadEntryAsync(_bitReader, cancellationToken)
            .ConfigureAwait(false);
        if (bits <= 1)
        {
            return bits;
        }

        int res = await _bitReader.ReadBitsAsync(bits - 1, cancellationToken).ConfigureAwait(false);
        return res | (1 << (bits - 1));
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public ValueTask<int> FillBufferAsync(byte[] buffer, CancellationToken cancellationToken) =>
        FillBufferAsync(buffer, 0, buffer.Length, cancellationToken);

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask<int> FillBufferAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_producedBytes >= _originalSize)
        {
            return 0;
        }

        int limit = Math.Min(count, _originalSize - _producedBytes);
        int produced = 0;

        while (produced < limit)
        {
            if (_pendingCopyCount > 0)
            {
                produced += CopyFromHistory(buffer, offset + produced, limit - produced);
                continue;
            }

            if (_remainingCommands == 0)
            {
                // A block header is the 16-bit command count followed by its Huffman tables;
                // both must be consumed together to keep the bit stream in sync.
                _remainingCommands = await _bitReader
                    .ReadBitsAsync(16, cancellationToken)
                    .ConfigureAwait(false);
                await BeginNewBlockAsync(cancellationToken).ConfigureAwait(false);
            }

            _remainingCommands--;

            int command = await ReadCommandAsync(cancellationToken).ConfigureAwait(false);

            if (command >= 0 && command <= 0xFF)
            {
                byte value = (byte)command;
                buffer[offset + produced++] = value;
                _ringBuffer.Push(value);
            }
            else
            {
                _pendingCopyCount = command - 0x100 + 3;
                _pendingCopyOffset = await ReadOffsetAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        _producedBytes += produced;
        return produced;
    }

#if !LEGACY_DOTNET
    public async ValueTask<int> FillBufferAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
        {
            return await FillBufferAsync(
                    segment.Array!,
                    segment.Offset,
                    segment.Count,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        // Spans cannot cross an await, so memory not backed by an array is filled via a pooled array.
        byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int read = await FillBufferAsync(rented, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            rented.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
#endif
}
