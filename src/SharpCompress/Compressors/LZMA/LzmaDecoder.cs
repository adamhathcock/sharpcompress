using System;
using System.IO;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.Compressors.LZMA.RangeCoder;

namespace SharpCompress.Compressors.LZMA
{
    internal class Decoder : ICoder, ISetDecoderProperties // ,System.IO.Stream
    {
        private class LenDecoder
        {
            private BitDecoder _Choice = new BitDecoder();
            private BitDecoder _Choice2 = new BitDecoder();
            private readonly BitTreeDecoder[] _LowCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            private readonly BitTreeDecoder[] _MidCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            private BitTreeDecoder _HighCoder = new BitTreeDecoder(Base.kNumHighLenBits);
            private uint _NumPosStates;

            public void Create(uint numPosStates)
            {
                for (uint posState = _NumPosStates; posState < numPosStates; posState++)
                {
                    _LowCoder[posState] = new BitTreeDecoder(Base.kNumLowLenBits);
                    _MidCoder[posState] = new BitTreeDecoder(Base.kNumMidLenBits);
                }
                _NumPosStates = numPosStates;
            }

            public void Init()
            {
                _Choice.Init();
                for (uint posState = 0; posState < _NumPosStates; posState++)
                {
                    _LowCoder[posState].Init();
                    _MidCoder[posState].Init();
                }
                _Choice2.Init();
                _HighCoder.Init();
            }

            public uint Decode(RangeCoder.Decoder rangeDecoder, uint posState)
            {
                if (_Choice.Decode(rangeDecoder) == 0)
                {
                    return _LowCoder[posState].Decode(rangeDecoder);
                }
                uint symbol = Base.kNumLowLenSymbols;
                if (_Choice2.Decode(rangeDecoder) == 0)
                {
                    symbol += _MidCoder[posState].Decode(rangeDecoder);
                }
                else
                {
                    symbol += Base.kNumMidLenSymbols;
                    symbol += _HighCoder.Decode(rangeDecoder);
                }
                return symbol;
            }
        }

        private class LiteralDecoder
        {
            private struct Decoder2
            {
                private BitDecoder[] _Decoders;

                public void Create()
                {
                    _Decoders = new BitDecoder[0x300];
                }

                public void Init()
                {
                    for (int i = 0; i < 0x300; i++)
                    {
                        _Decoders[i].Init();
                    }
                }

                public byte DecodeNormal(RangeCoder.Decoder rangeDecoder)
                {
                    uint symbol = 1;
                    do
                    {
                        symbol = (symbol << 1) | _Decoders[symbol].Decode(rangeDecoder);
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }

                public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, byte matchByte)
                {
                    uint symbol = 1;
                    do
                    {
                        uint matchBit = (uint)(matchByte >> 7) & 1;
                        matchByte <<= 1;
                        uint bit = _Decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit)
                        {
                            while (symbol < 0x100)
                            {
                                symbol = (symbol << 1) | _Decoders[symbol].Decode(rangeDecoder);
                            }
                            break;
                        }
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }
            }

            private Decoder2[] _Coders;
            private int _NumPrevBits;
            private int _NumPosBits;
            private uint _PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (_Coders != null && _NumPrevBits == numPrevBits &&
                    _NumPosBits == numPosBits)
                {
                    return;
                }
                _NumPosBits = numPosBits;
                _PosMask = ((uint)1 << numPosBits) - 1;
                _NumPrevBits = numPrevBits;
                uint numStates = (uint)1 << (_NumPrevBits + _NumPosBits);
                _Coders = new Decoder2[numStates];
                for (uint i = 0; i < numStates; i++)
                {
                    _Coders[i].Create();
                }
            }

            public void Init()
            {
                uint numStates = (uint)1 << (_NumPrevBits + _NumPosBits);
                for (uint i = 0; i < numStates; i++)
                {
                    _Coders[i].Init();
                }
            }

            private uint GetState(uint pos, byte prevByte)
            {
                return ((pos & _PosMask) << _NumPrevBits) + (uint)(prevByte >> (8 - _NumPrevBits));
            }

            public byte DecodeNormal(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte)
            {
                return _Coders[GetState(pos, prevByte)].DecodeNormal(rangeDecoder);
            }

            public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
            {
                return _Coders[GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte);
            }
        }

        private OutWindow _OutWindow;

        private readonly BitDecoder[] _IsMatchDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
        private readonly BitDecoder[] _IsRepDecoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] _IsRepG0Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] _IsRepG1Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] _IsRepG2Decoders = new BitDecoder[Base.kNumStates];
        private readonly BitDecoder[] _IsRep0LongDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];

        private readonly BitTreeDecoder[] _PosSlotDecoder = new BitTreeDecoder[Base.kNumLenToPosStates];
        private readonly BitDecoder[] _PosDecoders = new BitDecoder[Base.kNumFullDistances - Base.kEndPosModelIndex];

        private BitTreeDecoder _PosAlignDecoder = new BitTreeDecoder(Base.kNumAlignBits);

        private readonly LenDecoder _LenDecoder = new LenDecoder();
        private readonly LenDecoder _RepLenDecoder = new LenDecoder();

        private readonly LiteralDecoder _LiteralDecoder = new LiteralDecoder();

        private int _DictionarySize;

        private uint _PosStateMask;

        private Base.State state = new Base.State();
        private uint rep0, rep1, rep2, rep3;

        public Decoder()
        {
            _DictionarySize = -1;
            for (int i = 0; i < Base.kNumLenToPosStates; i++)
            {
                _PosSlotDecoder[i] = new BitTreeDecoder(Base.kNumPosSlotBits);
            }
        }

        private void CreateDictionary()
        {
            if (_DictionarySize < 0)
            {
                throw new InvalidParamException();
            }
            _OutWindow = new OutWindow();
            int blockSize = Math.Max(_DictionarySize, (1 << 12));
            _OutWindow.Create(blockSize);
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
            _LiteralDecoder.Create(lp, lc);
        }

        private void SetPosBitsProperties(int pb)
        {
            if (pb > Base.kNumPosStatesBitsMax)
            {
                throw new InvalidParamException();
            }
            uint numPosStates = (uint)1 << pb;
            _LenDecoder.Create(numPosStates);
            _RepLenDecoder.Create(numPosStates);
            _PosStateMask = numPosStates - 1;
        }

        private void Init()
        {
            uint i;
            for (i = 0; i < Base.kNumStates; i++)
            {
                for (uint j = 0; j <= _PosStateMask; j++)
                {
                    uint index = (i << Base.kNumPosStatesBitsMax) + j;
                    _IsMatchDecoders[index].Init();
                    _IsRep0LongDecoders[index].Init();
                }
                _IsRepDecoders[i].Init();
                _IsRepG0Decoders[i].Init();
                _IsRepG1Decoders[i].Init();
                _IsRepG2Decoders[i].Init();
            }

            _LiteralDecoder.Init();
            for (i = 0; i < Base.kNumLenToPosStates; i++)
            {
                _PosSlotDecoder[i].Init();
            }

            // _PosSpecDecoder.Init();
            for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
            {
                _PosDecoders[i].Init();
            }

            _LenDecoder.Init();
            _RepLenDecoder.Init();
            _PosAlignDecoder.Init();

            state.Init();
            rep0 = 0;
            rep1 = 0;
            rep2 = 0;
            rep3 = 0;
        }

        public void Code(Stream inStream, Stream outStream,
                         Int64 inSize, Int64 outSize, ICodeProgress progress)
        {
            if (_OutWindow == null)
            {
                CreateDictionary();
            }
            _OutWindow.Init(outStream);
            if (outSize > 0)
            {
                _OutWindow.SetLimit(outSize);
            }
            else
            {
                _OutWindow.SetLimit(Int64.MaxValue - _OutWindow.Total);
            }

            RangeCoder.Decoder rangeDecoder = new RangeCoder.Decoder();
            rangeDecoder.Init(inStream);

            Code(_DictionarySize, _OutWindow, rangeDecoder);

            _OutWindow.ReleaseStream();
            rangeDecoder.ReleaseStream();

            if (!rangeDecoder.IsFinished || (inSize > 0 && rangeDecoder.Total != inSize))
            {
                throw new DataErrorException();
            }
            if (_OutWindow.HasPending)
            {
                throw new DataErrorException();
            }
            _OutWindow = null;
        }

        internal bool Code(int dictionarySize, OutWindow outWindow, RangeCoder.Decoder rangeDecoder)
        {
            int dictionarySizeCheck = Math.Max(dictionarySize, 1);

            outWindow.CopyPending();

            while (outWindow.HasSpace)
            {
                uint posState = (uint)outWindow.Total & _PosStateMask;
                if (_IsMatchDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(rangeDecoder) == 0)
                {
                    byte b;
                    byte prevByte = outWindow.GetByte(0);
                    if (!state.IsCharState())
                    {
                        b = _LiteralDecoder.DecodeWithMatchByte(rangeDecoder,
                                                                 (uint)outWindow.Total, prevByte,
                                                                 outWindow.GetByte((int)rep0));
                    }
                    else
                    {
                        b = _LiteralDecoder.DecodeNormal(rangeDecoder, (uint)outWindow.Total, prevByte);
                    }
                    outWindow.PutByte(b);
                    state.UpdateChar();
                }
                else
                {
                    uint len;
                    if (_IsRepDecoders[state.Index].Decode(rangeDecoder) == 1)
                    {
                        if (_IsRepG0Decoders[state.Index].Decode(rangeDecoder) == 0)
                        {
                            if (
                                _IsRep0LongDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(
                                                                                                                   rangeDecoder) == 0)
                            {
                                state.UpdateShortRep();
                                outWindow.PutByte(outWindow.GetByte((int)rep0));
                                continue;
                            }
                        }
                        else
                        {
                            UInt32 distance;
                            if (_IsRepG1Decoders[state.Index].Decode(rangeDecoder) == 0)
                            {
                                distance = rep1;
                            }
                            else
                            {
                                if (_IsRepG2Decoders[state.Index].Decode(rangeDecoder) == 0)
                                {
                                    distance = rep2;
                                }
                                else
                                {
                                    distance = rep3;
                                    rep3 = rep2;
                                }
                                rep2 = rep1;
                            }
                            rep1 = rep0;
                            rep0 = distance;
                        }
                        len = _RepLenDecoder.Decode(rangeDecoder, posState) + Base.kMatchMinLen;
                        state.UpdateRep();
                    }
                    else
                    {
                        rep3 = rep2;
                        rep2 = rep1;
                        rep1 = rep0;
                        len = Base.kMatchMinLen + _LenDecoder.Decode(rangeDecoder, posState);
                        state.UpdateMatch();
                        uint posSlot = _PosSlotDecoder[Base.GetLenToPosState(len)].Decode(rangeDecoder);
                        if (posSlot >= Base.kStartPosModelIndex)
                        {
                            int numDirectBits = (int)((posSlot >> 1) - 1);
                            rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                            if (posSlot < Base.kEndPosModelIndex)
                            {
                                rep0 += BitTreeDecoder.ReverseDecode(_PosDecoders,
                                                                     rep0 - posSlot - 1, rangeDecoder, numDirectBits);
                            }
                            else
                            {
                                rep0 += (rangeDecoder.DecodeDirectBits(
                                                                       numDirectBits - Base.kNumAlignBits) << Base.kNumAlignBits);
                                rep0 += _PosAlignDecoder.ReverseDecode(rangeDecoder);
                            }
                        }
                        else
                        {
                            rep0 = posSlot;
                        }
                    }
                    if (rep0 >= outWindow.Total || rep0 >= dictionarySizeCheck)
                    {
                        if (rep0 == 0xFFFFFFFF)
                        {
                            return true;
                        }
                        throw new DataErrorException();
                    }
                    outWindow.CopyBlock((int)rep0, (int)len);
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
            if (pb > Base.kNumPosStatesBitsMax)
            {
                throw new InvalidParamException();
            }
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
            Init();
            if (properties.Length >= 5)
            {
                _DictionarySize = 0;
                for (int i = 0; i < 4; i++)
                {
                    _DictionarySize += properties[1 + i] << (i * 8);
                }
            }
        }

        public void Train(Stream stream)
        {
            if (_OutWindow == null)
            {
                CreateDictionary();
            }
            _OutWindow.Train(stream);
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