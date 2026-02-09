using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.RangeCoder;

internal readonly partial struct BitTreeEncoder
{
    public async ValueTask EncodeAsync(
        Encoder rangeEncoder,
        uint symbol,
        CancellationToken cancellationToken = default
    )
    {
        uint m = 1;
        for (var bitIndex = _numBitLevels; bitIndex > 0; )
        {
            bitIndex--;
            var bit = (symbol >> bitIndex) & 1;
            await _models[m]
                .EncodeAsync(rangeEncoder, bit, cancellationToken)
                .ConfigureAwait(false);
            m = (m << 1) | bit;
        }
    }

    public async ValueTask ReverseEncodeAsync(
        Encoder rangeEncoder,
        uint symbol,
        CancellationToken cancellationToken = default
    )
    {
        uint m = 1;
        for (uint i = 0; i < _numBitLevels; i++)
        {
            var bit = symbol & 1;
            await _models[m]
                .EncodeAsync(rangeEncoder, bit, cancellationToken)
                .ConfigureAwait(false);
            m = (m << 1) | bit;
            symbol >>= 1;
        }
    }

    public static async ValueTask ReverseEncodeAsync(
        BitEncoder[] models,
        uint startIndex,
        Encoder rangeEncoder,
        int numBitLevels,
        uint symbol,
        CancellationToken cancellationToken = default
    )
    {
        uint m = 1;
        for (var i = 0; i < numBitLevels; i++)
        {
            var bit = symbol & 1;
            await models[startIndex + m]
                .EncodeAsync(rangeEncoder, bit, cancellationToken)
                .ConfigureAwait(false);
            m = (m << 1) | bit;
            symbol >>= 1;
        }
    }
}

internal readonly partial struct BitTreeDecoder
{
    public async ValueTask<uint> DecodeAsync(
        Decoder rangeDecoder,
        CancellationToken cancellationToken = default
    )
    {
        uint m = 1;
        for (var bitIndex = _numBitLevels; bitIndex > 0; bitIndex--)
        {
            m =
                (m << 1)
                + await _models[m]
                    .DecodeAsync(rangeDecoder, cancellationToken)
                    .ConfigureAwait(false);
        }
        return m - ((uint)1 << _numBitLevels);
    }

    public async ValueTask<uint> ReverseDecodeAsync(
        Decoder rangeDecoder,
        CancellationToken cancellationToken = default
    )
    {
        uint m = 1;
        uint symbol = 0;
        for (var bitIndex = 0; bitIndex < _numBitLevels; bitIndex++)
        {
            var bit = await _models[m]
                .DecodeAsync(rangeDecoder, cancellationToken)
                .ConfigureAwait(false);
            m <<= 1;
            m += bit;
            symbol |= (bit << bitIndex);
        }
        return symbol;
    }

    public static async ValueTask<uint> ReverseDecodeAsync(
        BitDecoder[] models,
        uint startIndex,
        Decoder rangeDecoder,
        int numBitLevels,
        CancellationToken cancellationToken = default
    )
    {
        uint m = 1;
        uint symbol = 0;
        for (var bitIndex = 0; bitIndex < numBitLevels; bitIndex++)
        {
            var bit = await models[startIndex + m]
                .DecodeAsync(rangeDecoder, cancellationToken)
                .ConfigureAwait(false);
            m <<= 1;
            m += bit;
            symbol |= (bit << bitIndex);
        }
        return symbol;
    }
}
