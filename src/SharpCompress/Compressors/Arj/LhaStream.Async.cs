using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Arj;

public sealed partial class LhaStream<TDecoderConfig>
{
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

        int bytesRead = await FillBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
        return bytesRead;
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

    private async ValueTask BeginNewBlockAsync(CancellationToken cancellationToken)
    {
        await ReadTempTreeAsync(cancellationToken).ConfigureAwait(false);
        await ReadCommandTreeAsync(cancellationToken).ConfigureAwait(false);
        await ReadOffsetTreeAsync(cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<int> ReadCommandAsync(CancellationToken cancellationToken) =>
        _commandTree.ReadEntryAsync(_bitReader, cancellationToken);

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

    public async ValueTask<int> FillBufferAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        int bufLen = buffer.Length;
        int bufIndex = 0;

        // stop when we reached original size
        if (_producedBytes >= _originalSize)
        {
            return 0;
        }

        // calculate limit, so that we don't go over the original size
        int remaining = (int)Math.Min(bufLen, _originalSize - _producedBytes);

        while (bufIndex < remaining)
        {
            if (_copyProgress.HasValue)
            {
                var (offset, count) = _copyProgress.Value;
                int copied = CopyFromHistory(
                    buffer,
                    bufIndex,
                    offset,
                    (int)Math.Min(count, remaining - bufIndex)
                );
                bufIndex += copied;
                _copyProgress = null;
            }

            if (_remainingCommands == 0)
            {
                _remainingCommands = await _bitReader
                    .ReadBitsAsync(16, cancellationToken)
                    .ConfigureAwait(false);
                if (bufIndex + _remainingCommands > remaining)
                {
                    break;
                }
                await BeginNewBlockAsync(cancellationToken).ConfigureAwait(false);
            }

            _remainingCommands--;

            int command = await ReadCommandAsync(cancellationToken).ConfigureAwait(false);

            if (command >= 0 && command <= 0xFF)
            {
                byte value = (byte)command;
                buffer[bufIndex++] = value;
                _ringBuffer.Push(value);
            }
            else
            {
                int count = command - 0x100 + 3;
                int offset = await ReadOffsetAsync(cancellationToken).ConfigureAwait(false);
                int copyCount = (int)Math.Min(count, remaining - bufIndex);
                bufIndex += CopyFromHistory(buffer, bufIndex, offset, copyCount);
            }
        }

        _producedBytes += bufIndex;
        return bufIndex;
    }

#if !LEGACY_DOTNET
    public async ValueTask<int> FillBufferAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        int bufLen = buffer.Length;
        int bufIndex = 0;

        // stop when we reached original size
        if (_producedBytes >= _originalSize)
        {
            return 0;
        }

        // calculate limit, so that we don't go over the original size
        int remaining = (int)Math.Min(bufLen, _originalSize - _producedBytes);

        while (bufIndex < remaining)
        {
            if (_copyProgress.HasValue)
            {
                var (offset, count) = _copyProgress.Value;
                int copied = CopyFromHistory(
                    buffer.Span,
                    bufIndex,
                    offset,
                    (int)Math.Min(count, remaining - bufIndex)
                );
                bufIndex += copied;
                _copyProgress = null;
            }

            if (_remainingCommands == 0)
            {
                _remainingCommands = await _bitReader
                    .ReadBitsAsync(16, cancellationToken)
                    .ConfigureAwait(false);
                if (bufIndex + _remainingCommands > remaining)
                {
                    break;
                }
                await BeginNewBlockAsync(cancellationToken).ConfigureAwait(false);
            }

            _remainingCommands--;

            int command = await ReadCommandAsync(cancellationToken).ConfigureAwait(false);

            if (command >= 0 && command <= 0xFF)
            {
                byte value = (byte)command;
                buffer.Span[bufIndex++] = value;
                _ringBuffer.Push(value);
            }
            else
            {
                int count = command - 0x100 + 3;
                int offset = await ReadOffsetAsync(cancellationToken).ConfigureAwait(false);
                int copyCount = (int)Math.Min(count, remaining - bufIndex);
                bufIndex += CopyFromHistory(buffer.Span, bufIndex, offset, copyCount);
            }
        }

        _producedBytes += bufIndex;
        return bufIndex;
    }

    private int CopyFromHistory(Span<byte> target, int targetIndex, int offset, int count)
    {
        var historyIter = _ringBuffer.IterFromOffset(offset);
        int copied = 0;

        while (copied < count && historyIter.MoveNext() && (targetIndex + copied) < target.Length)
        {
            target[targetIndex + copied] = historyIter.Current;
            copied++;
        }

        if (copied < count)
        {
            _copyProgress = (offset, count - copied);
        }

        return copied;
    }
#endif
}
