using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Filters;

internal abstract partial class Filter
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var size = 0;

        if (_transformed > 0)
        {
            var copySize = _transformed;
            if (copySize > count)
            {
                copySize = count;
            }
            Buffer.BlockCopy(_tail, 0, buffer, offset, copySize);
            _transformed -= copySize;
            _read -= copySize;
            offset += copySize;
            count -= copySize;
            size += copySize;
            Buffer.BlockCopy(_tail, copySize, _tail, 0, _read);
        }
        if (count == 0)
        {
            return size;
        }

        var inSize = _read;
        if (inSize > count)
        {
            inSize = count;
        }
        Buffer.BlockCopy(_tail, 0, buffer, offset, inSize);
        _read -= inSize;
        Buffer.BlockCopy(_tail, inSize, _tail, 0, _read);
        while (!_endReached && inSize < count)
        {
            var baseRead = await _baseStream
                .ReadAsync(buffer, offset + inSize, count - inSize, cancellationToken)
                .ConfigureAwait(false);
            inSize += baseRead;
            if (baseRead == 0)
            {
                _endReached = true;
            }
        }
        while (!_endReached && _read < _tail.Length)
        {
            var baseRead = await _baseStream
                .ReadAsync(_tail, _read, _tail.Length - _read, cancellationToken)
                .ConfigureAwait(false);
            _read += baseRead;
            if (baseRead == 0)
            {
                _endReached = true;
            }
        }

        if (inSize > _tail.Length)
        {
            _transformed = Transform(buffer, offset, inSize);
            offset += _transformed;
            count -= _transformed;
            size += _transformed;
            inSize -= _transformed;
            _transformed = 0;
        }

        if (count == 0)
        {
            return size;
        }

        Buffer.BlockCopy(buffer, offset, _window, 0, inSize);
        Buffer.BlockCopy(_tail, 0, _window, inSize, _read);
        if (inSize + _read > _tail.Length)
        {
            _transformed = Transform(_window, 0, inSize + _read);
        }
        else
        {
            _transformed = inSize + _read;
        }
        Buffer.BlockCopy(_window, 0, buffer, offset, inSize);
        Buffer.BlockCopy(_window, inSize, _tail, 0, _read);
        size += inSize;
        _transformed -= inSize;

        return size;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var size = 0;
        var offset = 0;
        var count = buffer.Length;

        if (_transformed > 0)
        {
            var copySize = _transformed;
            if (copySize > count)
            {
                copySize = count;
            }
            _tail.AsSpan(0, copySize).CopyTo(buffer.Span.Slice(offset, copySize));
            _transformed -= copySize;
            _read -= copySize;
            offset += copySize;
            count -= copySize;
            size += copySize;
            Buffer.BlockCopy(_tail, copySize, _tail, 0, _read);
        }
        if (count == 0)
        {
            return size;
        }

        var inSize = _read;
        if (inSize > count)
        {
            inSize = count;
        }
        _tail.AsSpan(0, inSize).CopyTo(buffer.Span.Slice(offset, inSize));
        _read -= inSize;
        Buffer.BlockCopy(_tail, inSize, _tail, 0, _read);
        while (!_endReached && inSize < count)
        {
            var baseRead = await _baseStream
                .ReadAsync(buffer.Slice(offset + inSize, count - inSize), cancellationToken)
                .ConfigureAwait(false);
            inSize += baseRead;
            if (baseRead == 0)
            {
                _endReached = true;
            }
        }
        while (!_endReached && _read < _tail.Length)
        {
            var baseRead = await _baseStream
                .ReadAsync(_tail.AsMemory(_read, _tail.Length - _read), cancellationToken)
                .ConfigureAwait(false);
            _read += baseRead;
            if (baseRead == 0)
            {
                _endReached = true;
            }
        }

        if (inSize > _tail.Length)
        {
            // Transform operates in-place on a temporary array
            var arrayBuffer = buffer.Slice(offset, inSize).ToArray();
            _transformed = Transform(arrayBuffer, 0, inSize);
            // Copy transformed bytes back to the original buffer
            arrayBuffer.AsSpan(0, inSize).CopyTo(buffer.Span.Slice(offset, inSize));
            offset += _transformed;
            count -= _transformed;
            size += _transformed;
            inSize -= _transformed;
            _transformed = 0;
        }

        if (count == 0)
        {
            return size;
        }

        var inputBytes = buffer.Slice(offset, inSize).ToArray();
        Buffer.BlockCopy(inputBytes, 0, _window, 0, inSize);
        Buffer.BlockCopy(_tail, 0, _window, inSize, _read);
        if (inSize + _read > _tail.Length)
        {
            _transformed = Transform(_window, 0, inSize + _read);
        }
        else
        {
            _transformed = inSize + _read;
        }
        _window.AsSpan(0, inSize).CopyTo(buffer.Span.Slice(offset, inSize));
        _window.AsSpan(inSize, _read).CopyTo(_tail.AsSpan());
        size += inSize;
        _transformed -= inSize;

        return size;
    }
#endif

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        Transform(buffer, offset, count);
        await _baseStream
            .WriteAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        // Transform is synchronous and requires byte[]
        var array = buffer.ToArray();
        Transform(array, 0, array.Length);
        await _baseStream.WriteAsync(array, cancellationToken).ConfigureAwait(false);
    }
#endif
}
