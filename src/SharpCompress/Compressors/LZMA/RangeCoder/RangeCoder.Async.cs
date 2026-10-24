#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.LZMA.RangeCoder;

internal partial class Encoder
{
    private byte[] SingleByteBuffer => _singleByteBuffer ??= new byte[1];

    public async ValueTask ShiftLowAsync(CancellationToken cancellationToken = default)
    {
        if ((uint)_low < 0xFF000000 || (uint)(_low >> 32) == 1)
        {
            var temp = _cache;
            do
            {
                var b = (byte)(temp + (_low >> 32));
                var buffer = SingleByteBuffer;
                buffer[0] = b;
                await _stream.WriteAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
                temp = 0xFF;
            } while (--_cacheSize != 0);
            _cache = (byte)(((uint)_low) >> 24);
        }
        _cacheSize++;
        _low = ((uint)_low) << 8;
    }

    public async ValueTask EncodeBitAsync(
        uint size0,
        int numTotalBits,
        uint symbol,
        CancellationToken cancellationToken = default
    )
    {
        var newBound = (_range >> numTotalBits) * size0;
        if (symbol == 0)
        {
            _range = newBound;
        }
        else
        {
            _low += newBound;
            _range -= newBound;
        }
        while (_range < K_TOP_VALUE)
        {
            _range <<= 8;
            await ShiftLowAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask EncodeDirectBitsAsync(
        uint v,
        int numTotalBits,
        CancellationToken cancellationToken = default
    )
    {
        for (var i = numTotalBits - 1; i >= 0; i--)
        {
            _range >>= 1;
            if (((v >> i) & 1) == 1)
            {
                _low += _range;
            }
            if (_range < K_TOP_VALUE)
            {
                _range <<= 8;
                await ShiftLowAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask FlushStreamAsync(CancellationToken cancellationToken = default) =>
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
}

internal partial class Decoder
{
#if !LEGACY_DOTNET
    internal async ValueTask InitLzma2ChunkAsync(
        Stream stream,
        int compressedSize,
        CancellationToken cancellationToken
    )
    {
        // LZMA2 explicitly bounds each compressed payload to at most 64 KiB. Fill the
        // existing fast input lease asynchronously; decoding can then be CPU-only,
        // without a synchronous read from the caller's stream or the next chunk header.
        if (compressedSize < 5 || compressedSize > FastBufferSize)
        {
            throw new DataErrorException();
        }
        cancellationToken.ThrowIfCancellationRequested();
        _fastBuffer ??= System.Buffers.ArrayPool<byte>.Shared.Rent(FastBufferSize);
        var offset = 0;
        while (offset < compressedSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream
                .ReadAsync(_fastBuffer.AsMemory(offset, compressedSize - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                // A truncated compressed payload is LZMA2 data corruption, not an
                // incomplete outer header. Keep the decoder's existing error category.
                throw new DataErrorException();
            }
            offset += read;
        }
        _stream = null;
        _range = uint.MaxValue;
        _code = 0;
        for (var i = 0; i < 5; i++)
        {
            _code = (_code << 8) | _fastBuffer[i];
        }
        _total = 5;
        _fastLimit = compressedSize;
        _fastBufferPos = 5;
        _fastBufferLen = compressedSize;
        // A corrupt payload may request more range bytes. Preserve the fast decoder's
        // EOF sentinel behavior, but never fetch those bytes from a following chunk.
        _fastEndOfStream = true;
        _fastBufferSafeUnbounded = false;
    }
#endif

    private byte[] SingleByteBuffer => _singleByteBuffer ??= new byte[1];

    public async ValueTask InitAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        _stream = stream;

        _code = 0;
        _range = 0xFFFFFFFF;
        var buffer = SingleByteBuffer;
        for (var i = 0; i < 5; i++)
        {
            var read = await _stream
                .ReadAsync(buffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }
            _code = (_code << 8) | buffer[0];
        }
        _total = 5;
    }

    public async ValueTask NormalizeAsync(CancellationToken cancellationToken = default)
    {
        while (_range < K_TOP_VALUE)
        {
            var buffer = SingleByteBuffer;
            var read = await _stream
                .ReadAsync(buffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }
            _code = (_code << 8) | buffer[0];
            _range <<= 8;
            _total++;
        }
    }

    public async ValueTask Normalize2Async(CancellationToken cancellationToken = default)
    {
        if (_range < K_TOP_VALUE)
        {
            var buffer = SingleByteBuffer;
            var read = await _stream
                .ReadAsync(buffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }
            _code = (_code << 8) | buffer[0];
            _range <<= 8;
            _total++;
        }
    }

    public async ValueTask<uint> DecodeDirectBitsAsync(
        int numTotalBits,
        CancellationToken cancellationToken = default
    )
    {
        var range = _range;
        var code = _code;
        uint result = 0;
        var buffer = SingleByteBuffer;
        for (var i = numTotalBits; i > 0; i--)
        {
            range >>= 1;
            var t = (code - range) >> 31;
            code -= range & (t - 1);
            result = (result << 1) | (1 - t);

            if (range < K_TOP_VALUE)
            {
                var read = await _stream
                    .ReadAsync(buffer, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IncompleteArchiveException("Unexpected end of stream.");
                }
                code = (code << 8) | buffer[0];
                range <<= 8;
                _total++;
            }
        }
        _range = range;
        _code = code;
        return result;
    }

    public async ValueTask<uint> DecodeBitAsync(
        uint size0,
        int numTotalBits,
        CancellationToken cancellationToken = default
    )
    {
        var newBound = (_range >> numTotalBits) * size0;
        uint symbol;
        if (_code < newBound)
        {
            symbol = 0;
            _range = newBound;
        }
        else
        {
            symbol = 1;
            _code -= newBound;
            _range -= newBound;
        }
        await NormalizeAsync(cancellationToken).ConfigureAwait(false);
        return symbol;
    }

    public async ValueTask DecodeAsync(
        uint start,
        uint size,
        CancellationToken cancellationToken = default
    )
    {
        _code -= start * _range;
        _range *= size;
        await NormalizeAsync(cancellationToken).ConfigureAwait(false);
    }
}
