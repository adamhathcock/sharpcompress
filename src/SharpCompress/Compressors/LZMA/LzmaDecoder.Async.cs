#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.RangeCoder;

namespace SharpCompress.Compressors.LZMA;

public partial class Decoder : ICoder, ISetDecoderProperties
{
    public async Task CodeAsync(
        Stream inStream,
        Stream outStream,
        long inSize,
        long outSize,
        ICodeProgress progress,
        CancellationToken cancellationToken = default
    )
    {
        if (_outWindow is null)
        {
            CreateDictionary();
        }
        _outWindow.Init(outStream);
        if (outSize > 0)
        {
            _outWindow.SetLimit(outSize);
        }
        else
        {
            _outWindow.SetLimit(long.MaxValue - _outWindow.Total);
        }

        var rangeDecoder = new RangeCoder.Decoder();
        rangeDecoder.Init(inStream);

        await CodeAsync(_dictionarySize, _outWindow, rangeDecoder, cancellationToken)
            .ConfigureAwait(false);

        await _outWindow.ReleaseStreamAsync(cancellationToken).ConfigureAwait(false);
        rangeDecoder.ReleaseStream();

        _outWindow.Dispose();
        _outWindow = null;
    }

    internal async ValueTask<bool> CodeAsync(
        int dictionarySize,
        OutWindow outWindow,
        RangeCoder.Decoder rangeDecoder,
        CancellationToken cancellationToken = default
    )
    {
        var dictionarySizeCheck = Math.Max(dictionarySize, 1);

        await outWindow.CopyPendingAsync(cancellationToken).ConfigureAwait(false);

        while (outWindow.HasSpace)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var posState = (uint)outWindow.Total & _posStateMask;
            if (
                _isMatchDecoders[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState]
                    .Decode(rangeDecoder) == 0
            )
            {
                byte b;
                var prevByte = outWindow.GetByte(0);
                if (!_state.IsCharState())
                {
                    b = _literalDecoder.DecodeWithMatchByte(
                        rangeDecoder,
                        (uint)outWindow.Total,
                        prevByte,
                        outWindow.GetByte((int)_rep0)
                    );
                }
                else
                {
                    b = _literalDecoder.DecodeNormal(rangeDecoder, (uint)outWindow.Total, prevByte);
                }
                await outWindow.PutByteAsync(b, cancellationToken).ConfigureAwait(false);
                _state.UpdateChar();
            }
            else
            {
                uint len;
                if (_isRepDecoders[_state._index].Decode(rangeDecoder) == 1)
                {
                    if (_isRepG0Decoders[_state._index].Decode(rangeDecoder) == 0)
                    {
                        if (
                            _isRep0LongDecoders[
                                (_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState
                            ]
                                .Decode(rangeDecoder) == 0
                        )
                        {
                            _state.UpdateShortRep();
                            await outWindow
                                .PutByteAsync(outWindow.GetByte((int)_rep0), cancellationToken)
                                .ConfigureAwait(false);
                            continue;
                        }
                    }
                    else
                    {
                        uint distance;
                        if (_isRepG1Decoders[_state._index].Decode(rangeDecoder) == 0)
                        {
                            distance = _rep1;
                        }
                        else
                        {
                            if (_isRepG2Decoders[_state._index].Decode(rangeDecoder) == 0)
                            {
                                distance = _rep2;
                            }
                            else
                            {
                                distance = _rep3;
                                _rep3 = _rep2;
                            }
                            _rep2 = _rep1;
                        }
                        _rep1 = _rep0;
                        _rep0 = distance;
                    }
                    len = _repLenDecoder.Decode(rangeDecoder, posState) + Base.K_MATCH_MIN_LEN;
                    _state.UpdateRep();
                }
                else
                {
                    _rep3 = _rep2;
                    _rep2 = _rep1;
                    _rep1 = _rep0;
                    len = Base.K_MATCH_MIN_LEN + _lenDecoder.Decode(rangeDecoder, posState);
                    _state.UpdateMatch();
                    var posSlot = _posSlotDecoder[Base.GetLenToPosState(len)].Decode(rangeDecoder);
                    if (posSlot >= Base.K_START_POS_MODEL_INDEX)
                    {
                        var numDirectBits = (int)((posSlot >> 1) - 1);
                        _rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                        if (posSlot < Base.K_END_POS_MODEL_INDEX)
                        {
                            _rep0 += BitTreeDecoder.ReverseDecode(
                                _posDecoders,
                                _rep0 - posSlot - 1,
                                rangeDecoder,
                                numDirectBits
                            );
                        }
                        else
                        {
                            _rep0 += (
                                rangeDecoder.DecodeDirectBits(numDirectBits - Base.K_NUM_ALIGN_BITS)
                                << Base.K_NUM_ALIGN_BITS
                            );
                            _rep0 += _posAlignDecoder.ReverseDecode(rangeDecoder);
                        }
                    }
                    else
                    {
                        _rep0 = posSlot;
                    }
                }
                if (_rep0 >= outWindow.Total || _rep0 >= dictionarySizeCheck)
                {
                    if (_rep0 == 0xFFFFFFFF)
                    {
                        return true;
                    }
                    throw new DataErrorException();
                }
                await outWindow
                    .CopyBlockAsync((int)_rep0, (int)len, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        return false;
    }
}
