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
            return 0;

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed
            if (_bufferedLength == 0)
            {
                _bufferedLength = await Stream
                    .ReadAsync(_buffer!, 0, _bufferSize, cancellationToken)
                    .ConfigureAwait(false);
                _bufferPosition = 0;
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
            int r = await Stream
                .ReadAsync(_buffer!, 0, _bufferSize, cancellationToken)
                .ConfigureAwait(false);
            if (r == 0)
                return 0;
            _bufferedLength = r;
            _bufferPosition = 0;
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
            return 0;

        if (_bufferingEnabled)
        {
            ValidateBufferState();

            // Fill buffer if needed
            if (_bufferedLength == 0)
            {
                _bufferedLength = await Stream
                    .ReadAsync(_buffer.AsMemory(0, _bufferSize), cancellationToken)
                    .ConfigureAwait(false);
                _bufferPosition = 0;
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
            int r = await Stream
                .ReadAsync(_buffer.AsMemory(0, _bufferSize), cancellationToken)
                .ConfigureAwait(false);
            if (r == 0)
                return 0;
            _bufferedLength = r;
            _bufferPosition = 0;
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
        if (ThrowOnDispose)
        {
            throw new InvalidOperationException(
                $"Attempt to dispose of a {nameof(SharpCompressStream)} when {nameof(ThrowOnDispose)} is {ThrowOnDispose}"
            );
        }
        _isDisposed = true;
        await base.DisposeAsync();

        if (LeaveOpen)
        {
            return;
        }

        await Stream.DisposeAsync();
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

#endif
}
