using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

public partial class SharpCompressStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ThrowIfDisposed();
        if (count == 0)
        {
            return 0;
        }

        // In passthrough mode, delegate directly to underlying stream
        if (_isPassthrough)
        {
            return await stream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }

        // If ring buffer is enabled, use ring buffer logic
        if (_ringBuffer is not null)
        {
            return await ReadWithRingBufferAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }

        // No buffering - read directly from stream
        int read = await stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        streamPosition += read;
        _logicalPosition = streamPosition;
        return read;
    }

    /// <summary>
    /// Async version of ReadWithRingBuffer.
    /// </summary>
    private async ValueTask<int> ReadWithRingBufferAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        int totalRead = 0;

        // If logical position is behind stream position, read from ring buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            long bytesFromEnd = streamPosition - _logicalPosition;

            // Verify data is available in ring buffer
            if (!_ringBuffer!.CanReadFromEnd(bytesFromEnd))
            {
                throw new ArchiveOperationException(
                    $"Ring buffer underflow: trying to read {bytesFromEnd} bytes back, "
                        + $"but buffer only holds {_ringBuffer.Length} bytes."
                );
            }

            int available = _ringBuffer.ReadFromEnd(bytesFromEnd, buffer, offset, count);
            totalRead += available;
            offset += available;
            count -= available;
            _logicalPosition += available;
        }

        // If more data needed and we're caught up, read from underlying stream
        if (count > 0 && _logicalPosition == streamPosition)
        {
            int read = await stream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            if (read > 0)
            {
                _ringBuffer!.Write(buffer, offset, read);
                streamPosition += read;
                _logicalPosition += read;
                totalRead += read;
            }
        }

        return totalRead;
    }

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        if (buffer.Length == 0)
        {
            return ValueTask.FromResult(0);
        }

        // In passthrough mode, delegate directly to underlying stream
        if (_isPassthrough)
        {
            return stream.ReadAsync(buffer, cancellationToken);
        }

        return ReadAsyncCore(buffer, cancellationToken);
    }

    private async ValueTask<int> ReadAsyncCore(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        // If ring buffer is enabled, use ring buffer logic
        if (_ringBuffer is not null)
        {
            return await ReadWithRingBufferAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        // No buffering - read directly from stream
        int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        streamPosition += read;
        _logicalPosition = streamPosition;
        return read;
    }

    /// <summary>
    /// Async version of ReadWithRingBuffer for Memory&lt;byte&gt;.
    /// </summary>
    private async ValueTask<int> ReadWithRingBufferAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        int totalRead = 0;
        int count = buffer.Length;
        int offset = 0;
        // Deflate refills use array-backed memory. Preserve the ValueTask input read
        // without allocating temporary arrays just to access the ring buffer.
        var arrayBacked = MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment);

        // If logical position is behind stream position, read from ring buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            long bytesFromEnd = streamPosition - _logicalPosition;

            // Verify data is available in ring buffer
            if (!_ringBuffer!.CanReadFromEnd(bytesFromEnd))
            {
                throw new ArchiveOperationException(
                    $"Ring buffer underflow: trying to read {bytesFromEnd} bytes back, "
                        + $"but buffer only holds {_ringBuffer.Length} bytes."
                );
            }

            int available;
            if (arrayBacked)
            {
                available = _ringBuffer.ReadFromEnd(
                    bytesFromEnd,
                    segment.Array!,
                    segment.Offset + offset,
                    count
                );
            }
            else
            {
                int replayCount = Math.Min(count, (int)bytesFromEnd);
                var tempBuffer = ArrayPool<byte>.Shared.Rent(replayCount);
                try
                {
                    available = _ringBuffer.ReadFromEnd(bytesFromEnd, tempBuffer, 0, replayCount);
                    tempBuffer.AsSpan(0, available).CopyTo(buffer.Span.Slice(offset));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempBuffer);
                }
            }

            totalRead += available;
            offset += available;
            count -= available;
            _logicalPosition += available;
        }

        // If more data needed and we're caught up, read from underlying stream
        if (count > 0 && _logicalPosition == streamPosition)
        {
            int read = await stream
                .ReadAsync(buffer.Slice(offset, count), cancellationToken)
                .ConfigureAwait(false);
            if (read > 0)
            {
                if (arrayBacked)
                {
                    _ringBuffer!.Write(segment.Array!, segment.Offset + offset, read);
                }
                else
                {
                    // RingBuffer.Write requires an array for non-array memory.
                    var tempBuffer = ArrayPool<byte>.Shared.Rent(read);
                    try
                    {
                        buffer.Slice(offset, read).Span.CopyTo(tempBuffer);
                        _ringBuffer!.Write(tempBuffer, 0, read);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(tempBuffer);
                    }
                }
                streamPosition += read;
                _logicalPosition += read;
                totalRead += read;
            }
        }

        return totalRead;
    }
#endif

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_isPassthrough)
        {
            return stream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        throw new NotSupportedException();
    }

#if !LEGACY_DOTNET
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_isPassthrough)
        {
            return stream.WriteAsync(buffer, cancellationToken);
        }
        throw new NotSupportedException();
    }
#endif

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_isPassthrough)
        {
            return stream.FlushAsync(cancellationToken);
        }
        throw new NotSupportedException();
    }

    public override async Task CopyToAsync(
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken
    )
    {
        byte[] buffer = new byte[bufferSize];
        int bytesRead;
        while (
            (
                bytesRead = await ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false)
            ) != 0
        )
        {
            await destination
                .WriteAsync(buffer, 0, bytesRead, cancellationToken)
                .ConfigureAwait(false);
        }
    }

#if STREAM_DISPOSEASYNC
    public override async ValueTask DisposeAsync()
#else
    public virtual async ValueTask DisposeAsync()
#endif
    {
        if (!isDisposed)
        {
            if (ThrowOnDispose)
            {
                throw new ArchiveOperationException(
                    $"Attempt to dispose of a {nameof(SharpCompressStream)} when {nameof(ThrowOnDispose)} is true"
                );
            }
            isDisposed = true;
            try
            {
                if (!LeaveStreamOpen)
                {
                    await stream.DisposeAsyncCompat().ConfigureAwait(false);
                }
            }
            finally
            {
                _ringBuffer?.Dispose();
                _ringBuffer = null;
            }
        }
#if STREAM_DISPOSEASYNC
        await base.DisposeAsync().ConfigureAwait(false);
#else
        base.Dispose();
        GC.SuppressFinalize(this);
#endif
    }
}
