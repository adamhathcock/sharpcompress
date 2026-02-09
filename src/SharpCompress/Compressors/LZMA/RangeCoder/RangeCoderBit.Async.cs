using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.LZMA.RangeCoder;

internal partial struct BitEncoder
{
    public ValueTask EncodeAsync(
        Encoder encoder,
        uint symbol,
        CancellationToken cancellationToken = default
    )
    {
        var newBound = (encoder._range >> K_NUM_BIT_MODEL_TOTAL_BITS) * _prob;
        if (symbol == 0)
        {
            encoder._range = newBound;
            _prob += (K_BIT_MODEL_TOTAL - _prob) >> K_NUM_MOVE_BITS;
        }
        else
        {
            encoder._low += newBound;
            encoder._range -= newBound;
            _prob -= (_prob) >> K_NUM_MOVE_BITS;
        }
        if (encoder._range < Encoder.K_TOP_VALUE)
        {
            encoder._range <<= 8;
            return encoder.ShiftLowAsync(cancellationToken);
        }
        return default;
    }
}

internal partial struct BitDecoder
{
    public ValueTask<uint> DecodeAsync(
        Decoder decoder,
        CancellationToken cancellationToken = default
    )
    {
        var newBound = (decoder._range >> K_NUM_BIT_MODEL_TOTAL_BITS) * _prob;
        if (decoder._code < newBound)
        {
            decoder._range = newBound;
            _prob += (K_BIT_MODEL_TOTAL - _prob) >> K_NUM_MOVE_BITS;
            return DecodeAsyncHelper(decoder.Normalize2Async(cancellationToken), 0);
        }
        decoder._range -= newBound;
        decoder._code -= newBound;
        _prob -= (_prob) >> K_NUM_MOVE_BITS;
        return DecodeAsyncHelper(decoder.Normalize2Async(cancellationToken), 1);
    }

    private static async ValueTask<uint> DecodeAsyncHelper(ValueTask normalizeTask, uint result)
    {
        await normalizeTask.ConfigureAwait(false);
        return result;
    }
}
