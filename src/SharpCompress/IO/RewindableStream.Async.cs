using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal partial class RewindableStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count == 0)
        {
            return 0;
        }

        // If recording is active or we're reading from the recording buffer, use legacy behavior
        if (IsRecording || (isRewound && bufferStream.Position != bufferStream.Length))
        {
            return await ReadWithRecordingAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }

        // If rolling buffer is enabled (and not recording), use rolling buffer logic
        if (_rollingBuffer is not null)
        {
            return await ReadWithRollingBufferAsync(buffer, offset, count, cancellationToken)
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
                else if (_rollingBuffer is not null && tempRead > 0)
                {
                    // When transitioning out of recording mode, add to rolling buffer
                    // so that future rewinds will work
                    AddToRollingBuffer(buffer, offset + read, tempRead);
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
    /// Async version of ReadWithRollingBuffer.
    /// </summary>
    private async Task<int> ReadWithRollingBufferAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        int totalRead = 0;

        // If logical position is behind stream position, read from rolling buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            long bytesFromEnd = streamPosition - _logicalPosition;
            if (bytesFromEnd > _rollingBufferLength)
            {
                throw new InvalidOperationException(
                    "Logical position is outside rolling buffer range."
                );
            }

            int bufferIndex = (int)(
                (_rollingBufferWritePos - bytesFromEnd + _rollingBufferSize) % _rollingBufferSize
            );
            int availableFromBuffer = (int)Math.Min(bytesFromEnd, count);

            int firstPart = Math.Min(availableFromBuffer, _rollingBufferSize - bufferIndex);
            Array.Copy(_rollingBuffer!, bufferIndex, buffer, offset, firstPart);
            if (firstPart < availableFromBuffer)
            {
                Array.Copy(
                    _rollingBuffer!,
                    0,
                    buffer,
                    offset + firstPart,
                    availableFromBuffer - firstPart
                );
            }

            totalRead += availableFromBuffer;
            offset += availableFromBuffer;
            count -= availableFromBuffer;
            _logicalPosition += availableFromBuffer;
        }

        // If more data needed, read from underlying stream
        if (count > 0)
        {
            int read = await stream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            if (read > 0)
            {
                AddToRollingBuffer(buffer, offset, read);
                streamPosition += read;
                _logicalPosition += read;
                totalRead += read;
            }
        }

        return totalRead;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        // If recording is active or we're reading from the recording buffer, use legacy behavior
        if (IsRecording || (isRewound && bufferStream.Position != bufferStream.Length))
        {
            return await ReadWithRecordingAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        // If rolling buffer is enabled (and not recording), use rolling buffer logic
        if (_rollingBuffer is not null)
        {
            return await ReadWithRollingBufferAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
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
                else if (_rollingBuffer is not null && tempRead > 0)
                {
                    // When transitioning out of recording mode, add to rolling buffer
                    // so that future rewinds will work
                    var tempBuffer = buffer.Slice(read, tempRead).ToArray();
                    AddToRollingBuffer(tempBuffer, 0, tempRead);
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
    /// Async version of ReadWithRollingBuffer for Memory&lt;byte&gt;.
    /// </summary>
    private async ValueTask<int> ReadWithRollingBufferAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        int totalRead = 0;
        int count = buffer.Length;
        int offset = 0;

        // If logical position is behind stream position, read from rolling buffer first
        while (count > 0 && _logicalPosition < streamPosition)
        {
            long bytesFromEnd = streamPosition - _logicalPosition;
            if (bytesFromEnd > _rollingBufferLength)
            {
                throw new InvalidOperationException(
                    "Logical position is outside rolling buffer range."
                );
            }

            int bufferIndex = (int)(
                (_rollingBufferWritePos - bytesFromEnd + _rollingBufferSize) % _rollingBufferSize
            );
            int availableFromBuffer = (int)Math.Min(bytesFromEnd, count);

            int firstPart = Math.Min(availableFromBuffer, _rollingBufferSize - bufferIndex);
            _rollingBuffer.AsSpan(bufferIndex, firstPart).CopyTo(buffer.Span.Slice(offset));
            if (firstPart < availableFromBuffer)
            {
                _rollingBuffer
                    .AsSpan(0, availableFromBuffer - firstPart)
                    .CopyTo(buffer.Span.Slice(offset + firstPart));
            }

            totalRead += availableFromBuffer;
            offset += availableFromBuffer;
            count -= availableFromBuffer;
            _logicalPosition += availableFromBuffer;
        }

        // If more data needed, read from underlying stream
        if (count > 0)
        {
            int read = await stream
                .ReadAsync(buffer.Slice(offset, count), cancellationToken)
                .ConfigureAwait(false);
            if (read > 0)
            {
                // AddToRollingBuffer expects byte[], so we need to copy
                var tempBuffer = buffer.Slice(offset, read).ToArray();
                AddToRollingBuffer(tempBuffer, 0, read);
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
    ) => throw new NotSupportedException();

#if !LEGACY_DOTNET
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();
#endif

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

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
            isDisposed = true;
            await stream.DisposeAsync();
            if (_rollingBuffer is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(_rollingBuffer);
                _rollingBuffer = null;
            }
        }
    }
#endif
}
