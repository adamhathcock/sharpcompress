using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal partial class SharpCompressStream
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
        // If recording is active or we're reading from the recording buffer, use legacy behavior
        if (IsRecording || (isRewound && bufferStream.Position != bufferStream.Length))
        {
            return await ReadWithRecordingAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }

        // If ring buffer is enabled (and not recording), use ring buffer logic
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
    /// Async version of ReadWithRecording (legacy behavior for format detection).
    /// </summary>
    private async Task<int> ReadWithRecordingAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        int read;
        if (isRewound && bufferStream.Position != bufferStream.Length)
        {
            var readCount = Math.Min(count, (int)(bufferStream.Length - bufferStream.Position));
            read = await bufferStream
                .ReadAsync(buffer, offset, readCount, cancellationToken)
                .ConfigureAwait(false);
            if (read < count)
            {
                var tempRead = await stream
                    .ReadAsync(buffer, offset + read, count - read, cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    await bufferStream
                        .WriteAsync(buffer, offset + read, tempRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (_ringBuffer is not null && tempRead > 0)
                {
                    // When transitioning out of recording mode, add to ring buffer
                    // so that future rewinds will work
                    _ringBuffer.Write(buffer, offset + read, tempRead);
                }
                streamPosition += tempRead;
                _logicalPosition = streamPosition;
                read += tempRead;
            }
            if (bufferStream.Position == bufferStream.Length)
            {
                isRewound = false;
            }
            return read;
        }

        read = await stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (IsRecording)
        {
            await bufferStream
                .WriteAsync(buffer, offset, read, cancellationToken)
                .ConfigureAwait(false);
        }
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
            int available = _ringBuffer!.ReadFromEnd(bytesFromEnd, buffer, offset, count);
            totalRead += available;
            offset += available;
            count -= available;
            _logicalPosition += available;
        }

        // If more data needed, read from underlying stream
        if (count > 0)
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
        // If recording is active or we're reading from the recording buffer, use legacy behavior
        if (IsRecording || (isRewound && bufferStream.Position != bufferStream.Length))
        {
            return await ReadWithRecordingAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        // If ring buffer is enabled (and not recording), use ring buffer logic
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
    /// Async version of ReadWithRecording for Memory&lt;byte&gt; (legacy behavior for format detection).
    /// </summary>
    private async ValueTask<int> ReadWithRecordingAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        int read;
        if (isRewound && bufferStream.Position != bufferStream.Length)
        {
            var readCount = (int)
                Math.Min(buffer.Length, bufferStream.Length - bufferStream.Position);
            read = await bufferStream
                .ReadAsync(buffer.Slice(0, readCount), cancellationToken)
                .ConfigureAwait(false);
            if (read < buffer.Length)
            {
                var tempRead = await stream
                    .ReadAsync(buffer.Slice(read), cancellationToken)
                    .ConfigureAwait(false);
                if (IsRecording)
                {
                    await bufferStream
                        .WriteAsync(buffer.Slice(read, tempRead), cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (_ringBuffer is not null && tempRead > 0)
                {
                    // When transitioning out of recording mode, add to ring buffer
                    // so that future rewinds will work
                    var tempBuffer = buffer.Slice(read, tempRead).ToArray();
                    _ringBuffer.Write(tempBuffer, 0, tempRead);
                }
                streamPosition += tempRead;
                _logicalPosition = streamPosition;
                read += tempRead;
            }
            if (bufferStream.Position == bufferStream.Length)
            {
                isRewound = false;
            }
            return read;
        }

        read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (IsRecording)
        {
            await bufferStream
                .WriteAsync(buffer.Slice(0, read), cancellationToken)
                .ConfigureAwait(false);
        }
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
            var tempBuffer = new byte[Math.Min(count, (int)bytesFromEnd)];
            int available = _ringBuffer!.ReadFromEnd(
                bytesFromEnd,
                tempBuffer,
                0,
                tempBuffer.Length
            );
            tempBuffer.AsSpan(0, available).CopyTo(buffer.Span.Slice(offset));

            totalRead += available;
            offset += available;
            count -= available;
            _logicalPosition += available;
        }

        // If more data needed, read from underlying stream
        if (count > 0)
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
        while ((bytesRead = await ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
        }
    }

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (!isDisposed)
        {
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException(
                    $"Attempt to dispose of a {nameof(SharpCompressStream)} when {nameof(ThrowOnDispose)} is true"
                );
            }
            isDisposed = true;
            if (!LeaveStreamOpen)
            {
                await stream.DisposeAsync();
            }
            _ringBuffer?.Dispose();
            _ringBuffer = null;
        }
    }
#endif
}
