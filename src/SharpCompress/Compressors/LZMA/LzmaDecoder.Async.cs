#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.RangeCoder;

namespace SharpCompress.Compressors.LZMA;

public partial class Decoder : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        if (_outWindow is not null)
        {
            await _outWindow.DisposeAsync().ConfigureAwait(false);
            _outWindow = null;
        }
    }

    partial class LenDecoder
    {
        public async ValueTask<uint> DecodeAsync(
            RangeCoder.Decoder rangeDecoder,
            uint posState,
            CancellationToken cancellationToken = default
        )
        {
            if (
                await _choice.DecodeAsync(rangeDecoder, cancellationToken).ConfigureAwait(false)
                == 0
            )
            {
                return await _lowCoder[posState]
                    .DecodeAsync(rangeDecoder, cancellationToken)
                    .ConfigureAwait(false);
            }
            var symbol = Base.K_NUM_LOW_LEN_SYMBOLS;
            if (
                await _choice2.DecodeAsync(rangeDecoder, cancellationToken).ConfigureAwait(false)
                == 0
            )
            {
                symbol += await _midCoder[posState]
                    .DecodeAsync(rangeDecoder, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                symbol += Base.K_NUM_MID_LEN_SYMBOLS;
                symbol += await _highCoder
                    .DecodeAsync(rangeDecoder, cancellationToken)
                    .ConfigureAwait(false);
            }
            return symbol;
        }
    }

    partial class LiteralDecoder
    {
        partial struct Decoder2
        {
            public async ValueTask<byte> DecodeNormalAsync(
                RangeCoder.Decoder rangeDecoder,
                CancellationToken cancellationToken = default
            )
            {
                uint symbol = 1;
                do
                {
                    symbol =
                        (symbol << 1)
                        | await _decoders[symbol]
                            .DecodeAsync(rangeDecoder, cancellationToken)
                            .ConfigureAwait(false);
                } while (symbol < 0x100);
                return (byte)symbol;
            }

            public async ValueTask<byte> DecodeWithMatchByteAsync(
                RangeCoder.Decoder rangeDecoder,
                byte matchByte,
                CancellationToken cancellationToken = default
            )
            {
                uint symbol = 1;
                do
                {
                    var matchBit = (uint)(matchByte >> 7) & 1;
                    matchByte <<= 1;
                    var bit = await _decoders[((1 + matchBit) << 8) + symbol]
                        .DecodeAsync(rangeDecoder, cancellationToken)
                        .ConfigureAwait(false);
                    symbol = (symbol << 1) | bit;
                    if (matchBit != bit)
                    {
                        while (symbol < 0x100)
                        {
                            symbol =
                                (symbol << 1)
                                | await _decoders[symbol]
                                    .DecodeAsync(rangeDecoder, cancellationToken)
                                    .ConfigureAwait(false);
                        }
                        break;
                    }
                } while (symbol < 0x100);
                return (byte)symbol;
            }
        }

        public async ValueTask<byte> DecodeNormalAsync(
            RangeCoder.Decoder rangeDecoder,
            uint pos,
            byte prevByte,
            CancellationToken cancellationToken = default
        ) =>
            await _coders[GetState(pos, prevByte)]
                .DecodeNormalAsync(rangeDecoder, cancellationToken)
                .ConfigureAwait(false);

        public async ValueTask<byte> DecodeWithMatchByteAsync(
            RangeCoder.Decoder rangeDecoder,
            uint pos,
            byte prevByte,
            byte matchByte,
            CancellationToken cancellationToken = default
        ) =>
            await _coders[GetState(pos, prevByte)]
                .DecodeWithMatchByteAsync(rangeDecoder, matchByte, cancellationToken)
                .ConfigureAwait(false);
    }

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
        await _outWindow.InitAsync(outStream).ConfigureAwait(false);
        if (outSize > 0)
        {
            _outWindow.SetLimit(outSize);
        }
        else
        {
            _outWindow.SetLimit(long.MaxValue - _outWindow.Total);
        }

        var rangeDecoder = new RangeCoder.Decoder();
        await rangeDecoder.InitAsync(inStream, cancellationToken).ConfigureAwait(false);

        await CodeAsync(_dictionarySize, _outWindow, rangeDecoder, cancellationToken)
            .ConfigureAwait(false);

        await _outWindow.ReleaseStreamAsync(cancellationToken).ConfigureAwait(false);
        rangeDecoder.ReleaseStream();

        await _outWindow.DisposeAsync().ConfigureAwait(false);
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
                await _isMatchDecoders[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState]
                    .DecodeAsync(rangeDecoder, cancellationToken)
                    .ConfigureAwait(false) == 0
            )
            {
                byte b;
                var prevByte = outWindow.GetByte(0);
                if (!_state.IsCharState())
                {
                    b = await _literalDecoder
                        .DecodeWithMatchByteAsync(
                            rangeDecoder,
                            (uint)outWindow.Total,
                            prevByte,
                            outWindow.GetByte((int)_rep0),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                else
                {
                    b = await _literalDecoder
                        .DecodeNormalAsync(
                            rangeDecoder,
                            (uint)outWindow.Total,
                            prevByte,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                await outWindow.PutByteAsync(b, cancellationToken).ConfigureAwait(false);
                _state.UpdateChar();
            }
            else
            {
                uint len;
                if (
                    await _isRepDecoders[_state._index]
                        .DecodeAsync(rangeDecoder, cancellationToken)
                        .ConfigureAwait(false) == 1
                )
                {
                    if (
                        await _isRepG0Decoders[_state._index]
                            .DecodeAsync(rangeDecoder, cancellationToken)
                            .ConfigureAwait(false) == 0
                    )
                    {
                        if (
                            await _isRep0LongDecoders[
                                (_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState
                            ]
                                .DecodeAsync(rangeDecoder, cancellationToken)
                                .ConfigureAwait(false) == 0
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
                        if (
                            await _isRepG1Decoders[_state._index]
                                .DecodeAsync(rangeDecoder, cancellationToken)
                                .ConfigureAwait(false) == 0
                        )
                        {
                            distance = _rep1;
                        }
                        else
                        {
                            if (
                                await _isRepG2Decoders[_state._index]
                                    .DecodeAsync(rangeDecoder, cancellationToken)
                                    .ConfigureAwait(false) == 0
                            )
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
                    len =
                        await _repLenDecoder
                            .DecodeAsync(rangeDecoder, posState, cancellationToken)
                            .ConfigureAwait(false) + Base.K_MATCH_MIN_LEN;
                    _state.UpdateRep();
                }
                else
                {
                    _rep3 = _rep2;
                    _rep2 = _rep1;
                    _rep1 = _rep0;
                    len =
                        Base.K_MATCH_MIN_LEN
                        + await _lenDecoder
                            .DecodeAsync(rangeDecoder, posState, cancellationToken)
                            .ConfigureAwait(false);
                    _state.UpdateMatch();
                    var posSlot = await _posSlotDecoder[Base.GetLenToPosState(len)]
                        .DecodeAsync(rangeDecoder, cancellationToken)
                        .ConfigureAwait(false);
                    if (posSlot >= Base.K_START_POS_MODEL_INDEX)
                    {
                        var numDirectBits = (int)((posSlot >> 1) - 1);
                        _rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                        if (posSlot < Base.K_END_POS_MODEL_INDEX)
                        {
                            _rep0 += await BitTreeDecoder
                                .ReverseDecodeAsync(
                                    _posDecoders,
                                    _rep0 - posSlot - 1,
                                    rangeDecoder,
                                    numDirectBits,
                                    cancellationToken
                                )
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            _rep0 += (
                                await rangeDecoder
                                    .DecodeDirectBitsAsync(
                                        numDirectBits - Base.K_NUM_ALIGN_BITS,
                                        cancellationToken
                                    )
                                    .ConfigureAwait(false) << Base.K_NUM_ALIGN_BITS
                            );
                            _rep0 += await _posAlignDecoder
                                .ReverseDecodeAsync(rangeDecoder, cancellationToken)
                                .ConfigureAwait(false);
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

    public async ValueTask TrainAsync(Stream stream)
    {
        if (_outWindow is null)
        {
            CreateDictionary();
        }
        await _outWindow.TrainAsync(stream).ConfigureAwait(false);
    }
}
