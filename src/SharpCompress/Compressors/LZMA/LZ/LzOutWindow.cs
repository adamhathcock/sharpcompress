#nullable disable

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.LZ;

internal class OutWindow : IDisposable
{
    private byte[] _buffer;
    private int _windowSize;
    private int _pos;
    private int _streamPos;
    private int _pendingLen;
    private int _pendingDist;
    private Stream _stream;

    private long _total;
    private long _limit;

    public long Total => _total;

    public void Create(int windowSize)
    {
        if (_windowSize != windowSize)
        {
            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }
            _buffer = ArrayPool<byte>.Shared.Rent(windowSize);
        }
        _buffer[windowSize - 1] = 0;
        _windowSize = windowSize;
        _pos = 0;
        _streamPos = 0;
        _pendingLen = 0;
        _total = 0;
        _limit = 0;
    }

    public void Dispose()
    {
        ReleaseStream();
        if (_buffer is null)
        {
            return;
        }
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = null;
    }

    public void Reset()
    {
        ReleaseStream();
        Create(_windowSize);
    }

    public void Init(Stream stream)
    {
        ReleaseStream();
        _stream = stream;
    }

    public void Train(Stream stream)
    {
        var len = stream.Length;
        var size = (len < _windowSize) ? (int)len : _windowSize;
        stream.Position = len - size;
        _total = 0;
        _limit = size;
        _pos = _windowSize - size;
        CopyStream(stream, size);
        if (_pos == _windowSize)
        {
            _pos = 0;
        }
        _streamPos = _pos;
    }

    public void ReleaseStream()
    {
        Flush();
        _stream = null;
    }

    public async Task ReleaseStreamAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        _stream = null;
    }

    private void Flush()
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
        _stream.Write(_buffer, _streamPos, size);
        if (_pos >= _windowSize)
        {
            _pos = 0;
        }
        _streamPos = _pos;
    }

    private async Task FlushAsync(CancellationToken cancellationToken = default)
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

    public void CopyPending()
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
            PutByte(_buffer[pos++]);
            rem--;
        }
        _pendingLen = rem;
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

    public void CopyBlock(int distance, int len)
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
                Flush();
            }
            rem -= fastSize;
        }
        while (rem > 0 && HasSpace)
        {
            if (pos >= _windowSize)
            {
                pos = 0;
            }
            PutByte(_buffer[pos++]);
            rem--;
        }
        _pendingLen = rem;
        _pendingDist = distance;
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

    public void PutByte(byte b)
    {
        _buffer[_pos++] = b;
        _total++;
        if (_pos >= _windowSize)
        {
            Flush();
        }
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

    public byte GetByte(int distance)
    {
        var pos = _pos - distance - 1;
        if (pos < 0)
        {
            pos += _windowSize;
        }
        return _buffer[pos];
    }

    public int CopyStream(Stream stream, int len)
    {
        var size = len;
        while (size > 0 && _pos < _windowSize && _total < _limit)
        {
            var curSize = _windowSize - _pos;
            if (curSize > _limit - _total)
            {
                curSize = (int)(_limit - _total);
            }
            if (curSize > size)
            {
                curSize = size;
            }
            var numReadBytes = stream.Read(_buffer, _pos, curSize);
            if (numReadBytes == 0)
            {
                throw new DataErrorException();
            }
            size -= numReadBytes;
            _pos += numReadBytes;
            _total += numReadBytes;
            if (_pos >= _windowSize)
            {
                Flush();
            }
        }
        return len - size;
    }

    public async Task<int> CopyStreamAsync(
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

    public void SetLimit(long size) => _limit = _total + size;

    public bool HasSpace => _pos < _windowSize && _total < _limit;

    public bool HasPending => _pendingLen > 0;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_streamPos >= _pos)
        {
            return 0;
        }

        var size = _pos - _streamPos;
        if (size > count)
        {
            size = count;
        }
        Buffer.BlockCopy(_buffer, _streamPos, buffer, offset, size);
        _streamPos += size;
        if (_streamPos >= _windowSize)
        {
            _pos = 0;
            _streamPos = 0;
        }
        return size;
    }

    public int Read(Memory<byte> buffer, int offset, int count)
    {
        if (_streamPos >= _pos)
        {
            return 0;
        }

        var size = _pos - _streamPos;
        if (size > count)
        {
            size = count;
        }
        _buffer.AsMemory(_streamPos, size).CopyTo(buffer.Slice(offset, size));
        _streamPos += size;
        if (_streamPos >= _windowSize)
        {
            _pos = 0;
            _streamPos = 0;
        }
        return size;
    }

    public int ReadByte()
    {
        if (_streamPos >= _pos)
        {
            return -1;
        }

        int value = _buffer[_streamPos];

        _streamPos++;
        if (_streamPos >= _windowSize)
        {
            _pos = 0;
            _streamPos = 0;
        }

        return value;
    }

    public int AvailableBytes => _pos - _streamPos;
}
