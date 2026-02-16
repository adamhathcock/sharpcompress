using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

public partial class SharpCompressStream
{
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count == 0)
        {
            return Task.FromResult(0);
        }

        // In passthrough mode, delegate directly to underlying stream
        if (_isPassthrough)
        {
            return stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        return ReadAsyncCore(buffer, offset, count, cancellationToken);
    }

    private async Task<int> ReadAsyncCore(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
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
    private async Task<int> ReadWithRingBufferAsync(
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

        // If logical position is behind stream position, read from ring buffer first
        // Note: We need to use a temporary byte array because RingBuffer.ReadFromEnd expects byte[]
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

            var tempBuffer = new byte[Math.Min(count, (int)bytesFromEnd)];
            int available = _ringBuffer.ReadFromEnd(bytesFromEnd, tempBuffer, 0, tempBuffer.Length);
            tempBuffer.AsSpan(0, available).CopyTo(buffer.Span.Slice(offset));

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
                // RingBuffer.Write expects byte[], so we need to copy
                var tempBuffer = buffer.Slice(offset, read).ToArray();
                _ringBuffer!.Write(tempBuffer, 0, read);
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

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
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
            if (!LeaveStreamOpen)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            _ringBuffer?.Dispose();
            _ringBuffer = null;
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif
}
