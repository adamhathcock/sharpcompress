using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip;

internal partial class WinzipAesCryptoStream
{
#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            await base.DisposeAsync().ConfigureAwait(false);
            return;
        }
        _isDisposed = true;
        // Read out last 10 auth bytes asynchronously
        byte[] authBytes = ArrayPool<byte>.Shared.Rent(10);
        try
        {
            await _stream.ReadFullyAsync(authBytes, 0, 10).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(authBytes);
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_totalBytesLeftToRead == 0)
        {
            return 0;
        }
        var bytesToRead = count;
        if (count > _totalBytesLeftToRead)
        {
            bytesToRead = (int)_totalBytesLeftToRead;
        }
        var read = await _stream
            .ReadAsync(buffer, offset, bytesToRead, cancellationToken)
            .ConfigureAwait(false);
        _totalBytesLeftToRead -= read;

        ReadTransformBlocks(buffer, offset, read);

        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_totalBytesLeftToRead == 0)
        {
            return 0;
        }
        var bytesToRead = buffer.Length;
        if (buffer.Length > _totalBytesLeftToRead)
        {
            bytesToRead = (int)_totalBytesLeftToRead;
        }
        var read = await _stream
            .ReadAsync(buffer.Slice(0, bytesToRead), cancellationToken)
            .ConfigureAwait(false);
        _totalBytesLeftToRead -= read;

        ReadTransformBlocks(buffer.Span, read);

        return read;
    }

    private void ReadTransformBlocks(Span<byte> buffer, int count)
    {
        var posn = 0;
        var remaining = count;

        while (posn < buffer.Length && remaining > 0)
        {
            var n = ReadTransformOneBlock(buffer, posn, remaining);
            posn += n;
            remaining -= n;
        }
    }

    private int ReadTransformOneBlock(Span<byte> buffer, int offset, int remaining)
    {
        if (_counterOutOffset == BLOCK_SIZE_IN_BYTES)
        {
            FillCounterOut();
        }

        var bytesToXor = Math.Min(BLOCK_SIZE_IN_BYTES - _counterOutOffset, remaining);
        XorInPlace(buffer, offset, bytesToXor, _counterOutOffset);
        _counterOutOffset += bytesToXor;
        return bytesToXor;
    }

    private void XorInPlace(Span<byte> buffer, int offset, int count, int counterOffset)
    {
        for (var i = 0; i < count; i++)
        {
            buffer[offset + i] = (byte)(_counterOut[counterOffset + i] ^ buffer[offset + i]);
        }
    }
#endif
}
