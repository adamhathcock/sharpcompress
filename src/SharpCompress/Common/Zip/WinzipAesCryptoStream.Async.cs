using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip;

internal partial class WinzipAesCryptoStream
{
    private readonly struct DiscardProcessor : Utility.IBufferProcessor<int>
    {
        public int Process(ReadOnlySpan<byte> buffer) => 0; // Just consume the bytes, don't need the result
    }

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        try
        {
            // Read out last 10 auth bytes asynchronously
            var processor = new DiscardProcessor();
            await _stream
                .ReadFullyRentedAsync<DiscardProcessor, int>(
                    10,
                    processor,
                    CancellationToken.None
                )
                .ConfigureAwait(false);
        }
        finally
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
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
        var last = count;

        while (posn < buffer.Length && posn < last)
        {
            var n = ReadTransformOneBlock(buffer, posn, last);
            posn += n;
        }
    }

    private int ReadTransformOneBlock(Span<byte> buffer, int offset, int last)
    {
        if (_isFinalBlock)
        {
            throw new InvalidOperationException();
        }

        var bytesRemaining = last - offset;
        var bytesToRead =
            (bytesRemaining > BLOCK_SIZE_IN_BYTES) ? BLOCK_SIZE_IN_BYTES : bytesRemaining;

        // update the counter
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(_counter, _nonce++);

        // Determine if this is the final block
        if ((bytesToRead == bytesRemaining) && (_totalBytesLeftToRead == 0))
        {
            _counterOut = _transform.TransformFinalBlock(_counter, 0, BLOCK_SIZE_IN_BYTES);
            _isFinalBlock = true;
        }
        else
        {
            _transform.TransformBlock(
                _counter,
                0, // offset
                BLOCK_SIZE_IN_BYTES,
                _counterOut,
                0
            ); // offset
        }

        XorInPlace(buffer, offset, bytesToRead);
        return bytesToRead;
    }

    private void XorInPlace(Span<byte> buffer, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            buffer[offset + i] = (byte)(_counterOut[i] ^ buffer[offset + i]);
        }
    }
#endif
}
