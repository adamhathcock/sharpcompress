using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        if (count == 0)
        {
            return 0;
        }

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed, handling short reads from underlying stream
            if (_bufferedLength == 0)
            {
                _bufferPosition = 0;
                _bufferedLength = await FillBufferAsync(_buffer!, 0, _bufferSize, cancellationToken)
                    .ConfigureAwait(false);
            }
            int available = _bufferedLength - _bufferPosition;
            int toRead = Math.Min(count, available);
            if (toRead > 0)
            {
                Array.Copy(_buffer!, _bufferPosition, buffer, offset, toRead);
                _bufferPosition += toRead;
                _internalPosition += toRead;
                return toRead;
            }
            // If buffer exhausted, refill
            _bufferPosition = 0;
            _bufferedLength = await FillBufferAsync(_buffer!, 0, _bufferSize, cancellationToken)
                .ConfigureAwait(false);
            if (_bufferedLength == 0)
            {
                return 0;
            }
            toRead = Math.Min(count, _bufferedLength);
            Array.Copy(_buffer!, 0, buffer, offset, toRead);
            _bufferPosition = toRead;
            _internalPosition += toRead;
            return toRead;
        }
        else
        {
            int read = await Stream
                             .ReadAsync(buffer, offset, count, cancellationToken)
                             .ConfigureAwait(false);
            _internalPosition += read;
            return read;
        }
    }


    /// <summary>
    /// Async version of FillBuffer. Implements the ReadFullyAsync pattern.
    /// Reads in a loop until buffer is full or EOF is reached.
    /// </summary>
    private async Task<int> FillBufferAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        // Implement ReadFullyAsync pattern but return the actual count read
        // This is the same logic as Utility.ReadFullyAsync but returns count instead of bool
        var total = 0;
        int read;
        while (
            (
                read = await Stream
                             .ReadAsync(buffer, offset + total, count - total, cancellationToken)
                             .ConfigureAwait(false)
            ) > 0
        )
        {
            total += read;
            if (total >= count)
            {
                return total;
            }
        }
        return total;
    }


    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        await Stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _internalPosition += count;
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed, handling short reads from underlying stream
            if (_bufferedLength == 0)
            {
                _bufferPosition = 0;
                _bufferedLength = await FillBufferMemoryAsync(
                        _buffer.AsMemory(0, _bufferSize),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            int available = _bufferedLength - _bufferPosition;
            int toRead = Math.Min(buffer.Length, available);
            if (toRead > 0)
            {
                _buffer.AsSpan(_bufferPosition, toRead).CopyTo(buffer.Span);
                _bufferPosition += toRead;
                _internalPosition += toRead;
                return toRead;
            }
            // If buffer exhausted, refill
            _bufferPosition = 0;
            _bufferedLength = await FillBufferMemoryAsync(
                    _buffer.AsMemory(0, _bufferSize),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (_bufferedLength == 0)
            {
                return 0;
            }
            toRead = Math.Min(buffer.Length, _bufferedLength);
            _buffer.AsSpan(0, toRead).CopyTo(buffer.Span);
            _bufferPosition = toRead;
            _internalPosition += toRead;
            return toRead;
        }
        else
        {
            int read = await Stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _internalPosition += read;
            return read;
        }
    }

    /// <summary>
    /// Async version of FillBuffer for Memory{byte}. Implements the ReadFullyAsync pattern.
    /// Reads in a loop until buffer is full or EOF is reached.
    /// </summary>
    private async ValueTask<int> FillBufferMemoryAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        // Implement ReadFullyAsync pattern but return the actual count read
        var total = 0;
        int read;
        while (
            (
                read = await Stream
                             .ReadAsync(buffer.Slice(total), cancellationToken)
                             .ConfigureAwait(false)
            ) > 0
        )
        {
            total += read;
            if (total >= buffer.Length)
            {
                return total;
            }
        }
        return total;
    }


    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await Stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _internalPosition += buffer.Length;
    }

    public override async ValueTask DisposeAsync()
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(SharpCompressStream));
#endif
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        if (LeaveOpen)
        {
            return;
        }
        if (ThrowOnDispose)
        {
            throw new InvalidOperationException(
                $"Attempt to dispose of a {nameof(SharpCompressStream)} when {nameof(ThrowOnDispose)} is {ThrowOnDispose}"
            );
        }
        await base.DisposeAsync();
        await Stream.DisposeAsync();
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

#endif
}
