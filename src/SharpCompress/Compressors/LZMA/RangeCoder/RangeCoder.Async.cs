#nullable disable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.LZMA.RangeCoder;

internal partial class Encoder
{
    public async ValueTask ShiftLowAsync(CancellationToken cancellationToken = default)
    {
        if ((uint)_low < 0xFF000000 || (uint)(_low >> 32) == 1)
        {
            var temp = _cache;
            do
            {
                var b = (byte)(temp + (_low >> 32));
                var buffer = new[] { b };
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
    public async ValueTask InitAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        _stream = stream;

        _code = 0;
        _range = 0xFFFFFFFF;
        var buffer = new byte[1];
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
            var buffer = new byte[1];
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
            var buffer = new byte[1];
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
        var buffer = new byte[1];
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
