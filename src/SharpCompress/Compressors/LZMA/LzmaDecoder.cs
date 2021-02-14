using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.RangeCoder;

namespace SharpCompress.Compressors.LZMA
{
    internal class Decoder : ICoder, ISetDecoderProperties // ,System.IO.Stream
    {
        private class LenDecoder
        {
            private BitDecoder _choice = new();
            private BitDecoder _choice2 = new();
            private readonly BitTreeDecoder[] _lowCoder = new BitTreeDecoder[Base.K_NUM_POS_STATES_MAX];
            private readonly BitTreeDecoder[] _midCoder = new BitTreeDecoder[Base.K_NUM_POS_STATES_MAX];
            private BitTreeDecoder _highCoder = new(Base.K_NUM_HIGH_LEN_BITS);
            private uint _numPosStates;

            public void Create(uint numPosStates)
            {
                for (uint posState = _numPosStates; posState < numPosStates; posState++)
                {
                    _lowCoder[posState] = new BitTreeDecoder(Base.K_NUM_LOW_LEN_BITS);
                    _midCoder[posState] = new BitTreeDecoder(Base.K_NUM_MID_LEN_BITS);
                }
                _numPosStates = numPosStates;
            }

            public void Init()
            {
                _choice.Init();
                for (uint posState = 0; posState < _numPosStates; posState++)
                {
                    _lowCoder[posState].Init();
                    _midCoder[posState].Init();
                }
                _choice2.Init();
                _highCoder.Init();
            }

            public async ValueTask<uint> DecodeAsync(RangeCoder.Decoder rangeDecoder, uint posState, CancellationToken cancellationToken)
            {
                if (await _choice.DecodeAsync(rangeDecoder, cancellationToken) == 0)
                {
                    return await _lowCoder[posState].DecodeAsync(rangeDecoder, cancellationToken);
                }
                uint symbol = Base.K_NUM_LOW_LEN_SYMBOLS;
                if (await _choice2.DecodeAsync(rangeDecoder, cancellationToken) == 0)
                {
                    symbol += await _midCoder[posState].DecodeAsync(rangeDecoder, cancellationToken);
                }
                else
                {
                    symbol += Base.K_NUM_MID_LEN_SYMBOLS;
                    symbol += await _highCoder.DecodeAsync(rangeDecoder, cancellationToken);
                }
                return symbol;
            }
        }

        private class LiteralDecoder
        {
            private struct Decoder2
            {
                private BitDecoder[] _decoders;

                public void Create()
                {
                    _decoders = new BitDecoder[0x300];
                }

                public void Init()
                {
                    for (int i = 0; i < 0x300; i++)
                    {
                        _decoders[i].Init();
                    }
                }

                public async ValueTask<byte> DecodeNormalAsync(RangeCoder.Decoder rangeDecoder, CancellationToken cancellationToken)
                {
                    uint symbol = 1;
                    do
                    {
                        symbol = (symbol << 1) | await _decoders[symbol].DecodeAsync(rangeDecoder, cancellationToken);
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }

                public async ValueTask<byte> DecodeWithMatchByteAsync(RangeCoder.Decoder rangeDecoder, byte matchByte, CancellationToken cancellationToken)
                {
                    uint symbol = 1;
                    do
                    {
                        uint matchBit = (uint)(matchByte >> 7) & 1;
                        matchByte <<= 1;
                        uint bit = await _decoders[((1 + matchBit) << 8) + symbol].DecodeAsync(rangeDecoder, cancellationToken);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit)
                        {
                            while (symbol < 0x100)
                            {
                                symbol = (symbol << 1) | await _decoders[symbol].DecodeAsync(rangeDecoder, cancellationToken);
                            }
                            break;
                        }
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }
            }

            private readonly Decoder2[]_coders;
            private readonly int _numPrevBits;
            private readonly int _numPosBits;
            private readonly uint _posMask;
            
            public LiteralDecoder(int numPosBits, int numPrevBits)
            {
                if (_coders != null && _numPrevBits == numPrevBits &&
                    _numPosBits == numPosBits)
                {
                    return;
                }
                _numPosBits = numPosBits;
                _posMask = ((uint)1 << numPosBits) - 1;
                _numPrevBits = numPrevBits;
                uint numStates = (uint)1 << (_numPrevBits + _numPosBits);
                _coders = new Decoder2[numStates];
                for (uint i = 0; i < numStates; i++)
                {
                    _coders[i].Create();
                }
            }

            public void Init()
            {
                uint numStates = (uint)1 << (_numPrevBits + _numPosBits);
                for (uint i = 0; i < numStates; i++)
                {
                    _coders[i].Init();
                }
            }

            private uint GetState(uint pos, byte prevByte)
            {
                return ((pos & _posMask) << _numPrevBits) + (uint)(prevByte >> (8 - _numPrevBits));
            }

            public ValueTask<byte> DecodeNormalAsync(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, CancellationToken cancellationToken)
            {
                return _coders[GetState(pos, prevByte)].DecodeNormalAsync(rangeDecoder, cancellationToken);
            }

            public ValueTask<byte> DecodeWithMatchByteAsync(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte, CancellationToken cancellationToken)
            {
                return _coders[GetState(pos, prevByte)].DecodeWithMatchByteAsync(rangeDecoder, matchByte, cancellationToken);
            }
        }

        private OutWindow? _outWindow;

        private readonly BitDecoder[] _isMatchDecoders = new BitDecoder[Base.K_NUM_STATES << Base.K_NUM_POS_STATES_BITS_MAX];
        private readonly BitDecoder[] _isRepDecoders = new BitDecoder[Base.K_NUM_STATES];
        private readonly BitDecoder[] _isRepG0Decoders = new BitDecoder[Base.K_NUM_STATES];
        private readonly BitDecoder[] _isRepG1Decoders = new BitDecoder[Base.K_NUM_STATES];
        private readonly BitDecoder[] _isRepG2Decoders = new BitDecoder[Base.K_NUM_STATES];
        private readonly BitDecoder[] _isRep0LongDecoders = new BitDecoder[Base.K_NUM_STATES << Base.K_NUM_POS_STATES_BITS_MAX];

        private readonly BitTreeDecoder[] _posSlotDecoder = new BitTreeDecoder[Base.K_NUM_LEN_TO_POS_STATES];
        private readonly BitDecoder[] _posDecoders = new BitDecoder[Base.K_NUM_FULL_DISTANCES - Base.K_END_POS_MODEL_INDEX];

        private BitTreeDecoder _posAlignDecoder = new(Base.K_NUM_ALIGN_BITS);

        private readonly LenDecoder _lenDecoder = new();
        private readonly LenDecoder _repLenDecoder = new();

        private LiteralDecoder? _literalDecoder;

        private int _dictionarySize;

        private uint _posStateMask;

        private Base.State _state = new();
        private uint _rep0, _rep1, _rep2, _rep3;

        public Decoder()
        {
            _dictionarySize = -1;
            for (int i = 0; i < Base.K_NUM_LEN_TO_POS_STATES; i++)
            {
                _posSlotDecoder[i] = new BitTreeDecoder(Base.K_NUM_POS_SLOT_BITS);
            }
        }

        private OutWindow CreateDictionary()
        {
            if (_dictionarySize < 0)
            {
                throw new InvalidParamException();
            }
            var outWindow = new OutWindow();
            int blockSize = Math.Max(_dictionarySize, (1 << 12));
            outWindow.Create(blockSize);
            return outWindow;
        }

        private void SetLiteralProperties(int lp, int lc)
        {
            if (lp > 8)
            {
                throw new InvalidParamException();
            }
            if (lc > 8)
            {
                throw new InvalidParamException();
            }
            _literalDecoder = new(lp, lc);
        }

        private void SetPosBitsProperties(int pb)
        {
            if (pb > Base.K_NUM_POS_STATES_BITS_MAX)
            {
                throw new InvalidParamException();
            }
            uint numPosStates = (uint)1 << pb;
            _lenDecoder.Create(numPosStates);
            _repLenDecoder.Create(numPosStates);
            _posStateMask = numPosStates - 1;
        }

        private void Init()
        {
            uint i;
            for (i = 0; i < Base.K_NUM_STATES; i++)
            {
                for (uint j = 0; j <= _posStateMask; j++)
                {
                    uint index = (i << Base.K_NUM_POS_STATES_BITS_MAX) + j;
                    _isMatchDecoders[index].Init();
                    _isRep0LongDecoders[index].Init();
                }
                _isRepDecoders[i].Init();
                _isRepG0Decoders[i].Init();
                _isRepG1Decoders[i].Init();
                _isRepG2Decoders[i].Init();
            }

            _literalDecoder!.Init();
            for (i = 0; i < Base.K_NUM_LEN_TO_POS_STATES; i++)
            {
                _posSlotDecoder[i].Init();
            }

            // _PosSpecDecoder.Init();
            for (i = 0; i < Base.K_NUM_FULL_DISTANCES - Base.K_END_POS_MODEL_INDEX; i++)
            {
                _posDecoders[i].Init();
            }

            _lenDecoder.Init();
            _repLenDecoder.Init();
            _posAlignDecoder.Init();

            _state.Init();
            _rep0 = 0;
            _rep1 = 0;
            _rep2 = 0;
            _rep3 = 0;
        }

        public async ValueTask CodeAsync(Stream inStream, Stream outStream,
                         Int64 inSize, Int64 outSize, ICodeProgress progress, CancellationToken cancellationToken)
        {
            if (_outWindow is null)
            {
                _outWindow = CreateDictionary();
            }
            _outWindow.Init(outStream);
            if (outSize > 0)
            {
                _outWindow.SetLimit(outSize);
            }
            else
            {
                _outWindow.SetLimit(Int64.MaxValue - _outWindow._total);
            }

            RangeCoder.Decoder rangeDecoder = new RangeCoder.Decoder();
            await rangeDecoder.InitAsync(inStream, cancellationToken);

            await CodeAsync(_dictionarySize, _outWindow, rangeDecoder, cancellationToken);

            _outWindow.ReleaseStream();
            rangeDecoder.ReleaseStream();

            if (!rangeDecoder.IsFinished || (inSize > 0 && rangeDecoder._total != inSize))
            {
                throw new DataErrorException();
            }
            if (_outWindow.HasPending)
            {
                throw new DataErrorException();
            }
            _outWindow = null;
        }

        internal async ValueTask<bool> CodeAsync(int dictionarySize, OutWindow outWindow, RangeCoder.Decoder rangeDecoder, CancellationToken cancellationToken)
        {
            _literalDecoder ??= _literalDecoder.CheckNotNull(nameof(_literalDecoder));
            int dictionarySizeCheck = Math.Max(dictionarySize, 1);

            outWindow.CopyPending();

            while (outWindow.HasSpace)
            {
                uint posState = (uint)outWindow._total & _posStateMask;
                if (await _isMatchDecoders[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState].DecodeAsync(rangeDecoder, cancellationToken) == 0)
                {
                    byte b;
                    byte prevByte = outWindow.GetByte(0);
                    if (!_state.IsCharState())
                    {
                        b = await _literalDecoder.DecodeWithMatchByteAsync(rangeDecoder,
                                                                 (uint)outWindow._total, prevByte,
                                                                 outWindow.GetByte((int)_rep0), cancellationToken);
                    }
                    else
                    {
                        b = await _literalDecoder.DecodeNormalAsync(rangeDecoder, (uint)outWindow._total, prevByte, cancellationToken);
                    }
                    outWindow.PutByte(b);
                    _state.UpdateChar();
                }
                else
                {
                    uint len;
                    if (await _isRepDecoders[_state._index].DecodeAsync(rangeDecoder, cancellationToken) == 1)
                    {
                        if (await _isRepG0Decoders[_state._index].DecodeAsync(rangeDecoder, cancellationToken) == 0)
                        {
                            if (
                                await _isRep0LongDecoders[(_state._index << Base.K_NUM_POS_STATES_BITS_MAX) + posState].DecodeAsync(
                                                                                                                              rangeDecoder, cancellationToken) == 0)
                            {
                                _state.UpdateShortRep();
                                outWindow.PutByte(outWindow.GetByte((int)_rep0));
                                continue;
                            }
                        }
                        else
                        {
                            UInt32 distance;
                            if (await _isRepG1Decoders[_state._index].DecodeAsync(rangeDecoder, cancellationToken) == 0)
                            {
                                distance = _rep1;
                            }
                            else
                            {
                                if (await _isRepG2Decoders[_state._index].DecodeAsync(rangeDecoder, cancellationToken) == 0)
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
                        len = await _repLenDecoder.DecodeAsync(rangeDecoder, posState, cancellationToken) + Base.K_MATCH_MIN_LEN;
                        _state.UpdateRep();
                    }
                    else
                    {
                        _rep3 = _rep2;
                        _rep2 = _rep1;
                        _rep1 = _rep0;
                        len = Base.K_MATCH_MIN_LEN + await _lenDecoder.DecodeAsync(rangeDecoder, posState, cancellationToken);
                        _state.UpdateMatch();
                        uint posSlot = await _posSlotDecoder[Base.GetLenToPosState(len)].DecodeAsync(rangeDecoder, cancellationToken);
                        if (posSlot >= Base.K_START_POS_MODEL_INDEX)
                        {
                            int numDirectBits = (int)((posSlot >> 1) - 1);
                            _rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                            if (posSlot < Base.K_END_POS_MODEL_INDEX)
                            {
                                _rep0 += await BitTreeDecoder.ReverseDecode(_posDecoders,
                                                                     _rep0 - posSlot - 1, rangeDecoder, numDirectBits, cancellationToken);
                            }
                            else
                            {
                                _rep0 += (await rangeDecoder.DecodeDirectBitsAsync(numDirectBits - Base.K_NUM_ALIGN_BITS, cancellationToken) << Base.K_NUM_ALIGN_BITS);
                                _rep0 += await _posAlignDecoder.ReverseDecode(rangeDecoder, cancellationToken);
                            }
                        }
                        else
                        {
                            _rep0 = posSlot;
                        }
                    }
                    if (_rep0 >= outWindow._total || _rep0 >= dictionarySizeCheck)
                    {
                        if (_rep0 == 0xFFFFFFFF)
                        {
                            return true;
                        }
                        throw new DataErrorException();
                    }
                    outWindow.CopyBlock((int)_rep0, (int)len);
                }
            }
            return false;
        }

        public void SetDecoderProperties(byte[] properties)
        {
            if (properties.Length < 1)
            {
                throw new InvalidParamException();
            }
            int lc = properties[0] % 9;
            int remainder = properties[0] / 9;
            int lp = remainder % 5;
            int pb = remainder / 5;
            if (pb > Base.K_NUM_POS_STATES_BITS_MAX)
            {
                throw new InvalidParamException();
            }
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
            Init();
            if (properties.Length >= 5)
            {
                _dictionarySize = 0;
                for (int i = 0; i < 4; i++)
                {
                    _dictionarySize += properties[1 + i] << (i * 8);
                }
            }
        }

        public void Train(Stream stream)
        {
            if (_outWindow is null)
            {
                _outWindow = CreateDictionary();
            }
            _outWindow.Train(stream);
        }

        /*
        public override bool CanRead { get { return true; }}
        public override bool CanWrite { get { return true; }}
        public override bool CanSeek { get { return true; }}
        public override long Length { get { return 0; }}
        public override long Position
        {
            get { return 0;	}
            set { }
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) 
        {
            return 0;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
        }
        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            return 0;
        }
        public override void SetLength(long value) {}
        */
    }
}