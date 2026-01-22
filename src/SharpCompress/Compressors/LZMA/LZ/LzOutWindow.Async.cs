#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.LZ;

internal partial class OutWindow : IDisposable
{
    public async ValueTask ReleaseStreamAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        _stream = null;
    }

    private async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            return;
        }
        var size = _pos - _streamPos;
        if (size == 0)
        {
            return;
        }
        await _stream
            .WriteAsync(_buffer, _streamPos, size, cancellationToken)
            .ConfigureAwait(false);
        if (_pos >= _windowSize)
        {
            _pos = 0;
        }
        _streamPos = _pos;
    }

    public async ValueTask CopyPendingAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingLen < 1)
        {
            return;
        }
        var rem = _pendingLen;
        var pos = (_pendingDist < _pos ? _pos : _pos + _windowSize) - _pendingDist - 1;
        while (rem > 0 && HasSpace)
        {
            if (pos >= _windowSize)
            {
                pos = 0;
            }
            await PutByteAsync(_buffer[pos++], cancellationToken).ConfigureAwait(false);
            rem--;
        }
        _pendingLen = rem;
    }

    public async ValueTask CopyBlockAsync(
        int distance,
        int len,
        CancellationToken cancellationToken = default
    )
    {
        var rem = len;
        var pos = (distance < _pos ? _pos : _pos + _windowSize) - distance - 1;
        var targetSize = HasSpace ? (int)Math.Min(rem, _limit - _total) : 0;
        var sizeUntilWindowEnd = Math.Min(_windowSize - _pos, _windowSize - pos);
        var sizeUntilOverlap = Math.Abs(pos - _pos);
        var fastSize = Math.Min(Math.Min(sizeUntilWindowEnd, sizeUntilOverlap), targetSize);
        if (fastSize >= 2)
        {
            _buffer.AsSpan(pos, fastSize).CopyTo(_buffer.AsSpan(_pos, fastSize));
            _pos += fastSize;
            pos += fastSize;
            _total += fastSize;
            if (_pos >= _windowSize)
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            rem -= fastSize;
        }
        while (rem > 0 && HasSpace)
        {
            if (pos >= _windowSize)
            {
                pos = 0;
            }
            await PutByteAsync(_buffer[pos++], cancellationToken).ConfigureAwait(false);
            rem--;
        }
        _pendingLen = rem;
        _pendingDist = distance;
    }

    public async ValueTask PutByteAsync(byte b, CancellationToken cancellationToken = default)
    {
        _buffer[_pos++] = b;
        _total++;
        if (_pos >= _windowSize)
        {
            await FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<int> CopyStreamAsync(
        Stream stream,
        int len,
        CancellationToken cancellationToken = default
    )
    {
        var size = len;
        while (size > 0 && _pos < _windowSize && _total < _limit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var curSize = _windowSize - _pos;
            if (curSize > _limit - _total)
            {
                curSize = (int)(_limit - _total);
            }
            if (curSize > size)
            {
                curSize = size;
            }
            var numReadBytes = await stream
                .ReadAsync(_buffer, _pos, curSize, cancellationToken)
                .ConfigureAwait(false);
            if (numReadBytes == 0)
            {
                throw new DataErrorException();
            }
            size -= numReadBytes;
            _pos += numReadBytes;
            _total += numReadBytes;
            if (_pos >= _windowSize)
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        return len - size;
    }
}
