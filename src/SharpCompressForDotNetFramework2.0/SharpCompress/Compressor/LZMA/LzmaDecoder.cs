using System;
using SharpCompress.Compressor.LZMA.RangeCoder;

namespace SharpCompress.Compressor.LZMA
{
    internal class Decoder : ICoder, ISetDecoderProperties // ,System.IO.Stream
    {
        class LenDecoder
        {
            BitDecoder m_Choice = new BitDecoder();
            BitDecoder m_Choice2 = new BitDecoder();
            BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            BitTreeDecoder[] m_MidCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
            BitTreeDecoder m_HighCoder = new BitTreeDecoder(Base.kNumHighLenBits);
            uint m_NumPosStates = 0;

            public void Create(uint numPosStates)
            {
                for (uint posState = m_NumPosStates; posState < numPosStates; posState++)
                {
                    m_LowCoder[posState] = new BitTreeDecoder(Base.kNumLowLenBits);
                    m_MidCoder[posState] = new BitTreeDecoder(Base.kNumMidLenBits);
                }
                m_NumPosStates = numPosStates;
            }

            public void Init()
            {
                m_Choice.Init();
                for (uint posState = 0; posState < m_NumPosStates; posState++)
                {
                    m_LowCoder[posState].Init();
                    m_MidCoder[posState].Init();
                }
                m_Choice2.Init();
                m_HighCoder.Init();
            }

            public uint Decode(RangeCoder.Decoder rangeDecoder, uint posState)
            {
                if (m_Choice.Decode(rangeDecoder) == 0)
                    return m_LowCoder[posState].Decode(rangeDecoder);
                else
                {
                    uint symbol = Base.kNumLowLenSymbols;
                    if (m_Choice2.Decode(rangeDecoder) == 0)
                        symbol += m_MidCoder[posState].Decode(rangeDecoder);
                    else
                    {
                        symbol += Base.kNumMidLenSymbols;
                        symbol += m_HighCoder.Decode(rangeDecoder);
                    }
                    return symbol;
                }
            }
        }

        class LiteralDecoder
        {
            struct Decoder2
            {
                BitDecoder[] m_Decoders;
                public void Create() { m_Decoders = new BitDecoder[0x300]; }
                public void Init() { for (int i = 0; i < 0x300; i++) m_Decoders[i].Init(); }

                public byte DecodeNormal(RangeCoder.Decoder rangeDecoder)
                {
                    uint symbol = 1;
                    do
                        symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
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
                        uint bit = m_Decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit)
                        {
                            while (symbol < 0x100)
                                symbol = (symbol << 1) | m_Decoders[symbol].Decode(rangeDecoder);
                            break;
                        }
                    }
                    while (symbol < 0x100);
                    return (byte)symbol;
                }
            }

            Decoder2[] m_Coders;
            int m_NumPrevBits;
            int m_NumPosBits;
            uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (m_Coders != null && m_NumPrevBits == numPrevBits &&
                    m_NumPosBits == numPosBits)
                    return;
                m_NumPosBits = numPosBits;
                m_PosMask = ((uint)1 << numPosBits) - 1;
                m_NumPrevBits = numPrevBits;
                uint numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                m_Coders = new Decoder2[numStates];
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Create();
            }

            public void Init()
            {
                uint numStates = (uint)1 << (m_NumPrevBits + m_NumPosBits);
                for (uint i = 0; i < numStates; i++)
                    m_Coders[i].Init();
            }

            uint GetState(uint pos, byte prevByte)
            { return ((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> (8 - m_NumPrevBits)); }

            public byte DecodeNormal(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte)
            { return m_Coders[GetState(pos, prevByte)].DecodeNormal(rangeDecoder); }

            public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
            { return m_Coders[GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte); }
        };

        LZ.OutWindow m_OutWindow;

        BitDecoder[] m_IsMatchDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
        BitDecoder[] m_IsRepDecoders = new BitDecoder[Base.kNumStates];
        BitDecoder[] m_IsRepG0Decoders = new BitDecoder[Base.kNumStates];
        BitDecoder[] m_IsRepG1Decoders = new BitDecoder[Base.kNumStates];
        BitDecoder[] m_IsRepG2Decoders = new BitDecoder[Base.kNumStates];
        BitDecoder[] m_IsRep0LongDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];

        BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[Base.kNumLenToPosStates];
        BitDecoder[] m_PosDecoders = new BitDecoder[Base.kNumFullDistances - Base.kEndPosModelIndex];

        BitTreeDecoder m_PosAlignDecoder = new BitTreeDecoder(Base.kNumAlignBits);

        LenDecoder m_LenDecoder = new LenDecoder();
        LenDecoder m_RepLenDecoder = new LenDecoder();

        LiteralDecoder m_LiteralDecoder = new LiteralDecoder();

        int m_DictionarySize;

        uint m_PosStateMask;

        Base.State state = new Base.State();
        uint rep0, rep1, rep2, rep3;

        public Decoder()
        {
            m_DictionarySize = -1;
            for (int i = 0; i < Base.kNumLenToPosStates; i++)
                m_PosSlotDecoder[i] = new BitTreeDecoder(Base.kNumPosSlotBits);
        }

        void CreateDictionary()
        {
            if (m_DictionarySize < 0)
                throw new InvalidParamException();
            m_OutWindow = new LZ.OutWindow();
            int blockSize = Math.Max(m_DictionarySize, (1 << 12));
            m_OutWindow.Create(blockSize);
        }

        void SetLiteralProperties(int lp, int lc)
        {
            if (lp > 8)
                throw new InvalidParamException();
            if (lc > 8)
                throw new InvalidParamException();
            m_LiteralDecoder.Create(lp, lc);
        }

        void SetPosBitsProperties(int pb)
        {
            if (pb > Base.kNumPosStatesBitsMax)
                throw new InvalidParamException();
            uint numPosStates = (uint)1 << pb;
            m_LenDecoder.Create(numPosStates);
            m_RepLenDecoder.Create(numPosStates);
            m_PosStateMask = numPosStates - 1;
        }

        void Init()
        {
            uint i;
            for (i = 0; i < Base.kNumStates; i++)
            {
                for (uint j = 0; j <= m_PosStateMask; j++)
                {
                    uint index = (i << Base.kNumPosStatesBitsMax) + j;
                    m_IsMatchDecoders[index].Init();
                    m_IsRep0LongDecoders[index].Init();
                }
                m_IsRepDecoders[i].Init();
                m_IsRepG0Decoders[i].Init();
                m_IsRepG1Decoders[i].Init();
                m_IsRepG2Decoders[i].Init();
            }

            m_LiteralDecoder.Init();
            for (i = 0; i < Base.kNumLenToPosStates; i++)
                m_PosSlotDecoder[i].Init();
            // m_PosSpecDecoder.Init();
            for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
                m_PosDecoders[i].Init();

            m_LenDecoder.Init();
            m_RepLenDecoder.Init();
            m_PosAlignDecoder.Init();

            state.Init();
            rep0 = 0;
            rep1 = 0;
            rep2 = 0;
            rep3 = 0;
        }

        public void Code(System.IO.Stream inStream, System.IO.Stream outStream,
            Int64 inSize, Int64 outSize, ICodeProgress progress)
        {
            if (m_OutWindow == null)
                CreateDictionary();
            m_OutWindow.Init(outStream);
            if (outSize > 0)
                m_OutWindow.SetLimit(outSize);
            else
                m_OutWindow.SetLimit(Int64.MaxValue - m_OutWindow.Total);

            RangeCoder.Decoder rangeDecoder = new RangeCoder.Decoder();
            rangeDecoder.Init(inStream);

            Code(m_DictionarySize, m_OutWindow, rangeDecoder);

            m_OutWindow.ReleaseStream();
            rangeDecoder.ReleaseStream();

            if (!rangeDecoder.IsFinished || (inSize > 0 && rangeDecoder.Total != inSize))
                throw new DataErrorException();
            if (m_OutWindow.HasPending)
                throw new DataErrorException();
            m_OutWindow = null;
        }

        internal bool Code(int dictionarySize, LZ.OutWindow outWindow, RangeCoder.Decoder rangeDecoder)
        {
            int dictionarySizeCheck = Math.Max(dictionarySize, 1);

            outWindow.CopyPending();

            while (outWindow.HasSpace)
            {
                uint posState = (uint)outWindow.Total & m_PosStateMask;
                if (m_IsMatchDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(rangeDecoder) == 0)
                {
                    byte b;
                    byte prevByte = outWindow.GetByte(0);
                    if (!state.IsCharState())
                        b = m_LiteralDecoder.DecodeWithMatchByte(rangeDecoder,
                            (uint)outWindow.Total, prevByte, outWindow.GetByte((int)rep0));
                    else
                        b = m_LiteralDecoder.DecodeNormal(rangeDecoder, (uint)outWindow.Total, prevByte);
                    outWindow.PutByte(b);
                    state.UpdateChar();
                }
                else
                {
                    uint len;
                    if (m_IsRepDecoders[state.Index].Decode(rangeDecoder) == 1)
                    {
                        if (m_IsRepG0Decoders[state.Index].Decode(rangeDecoder) == 0)
                        {
                            if (m_IsRep0LongDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(rangeDecoder) == 0)
                            {
                                state.UpdateShortRep();
                                outWindow.PutByte(outWindow.GetByte((int)rep0));
                                continue;
                            }
                        }
                        else
                        {
                            UInt32 distance;
                            if (m_IsRepG1Decoders[state.Index].Decode(rangeDecoder) == 0)
                            {
                                distance = rep1;
                            }
                            else
                            {
                                if (m_IsRepG2Decoders[state.Index].Decode(rangeDecoder) == 0)
                                    distance = rep2;
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
                        len = m_RepLenDecoder.Decode(rangeDecoder, posState) + Base.kMatchMinLen;
                        state.UpdateRep();
                    }
                    else
                    {
                        rep3 = rep2;
                        rep2 = rep1;
                        rep1 = rep0;
                        len = Base.kMatchMinLen + m_LenDecoder.Decode(rangeDecoder, posState);
                        state.UpdateMatch();
                        uint posSlot = m_PosSlotDecoder[Base.GetLenToPosState(len)].Decode(rangeDecoder);
                        if (posSlot >= Base.kStartPosModelIndex)
                        {
                            int numDirectBits = (int)((posSlot >> 1) - 1);
                            rep0 = ((2 | (posSlot & 1)) << numDirectBits);
                            if (posSlot < Base.kEndPosModelIndex)
                                rep0 += BitTreeDecoder.ReverseDecode(m_PosDecoders,
                                        rep0 - posSlot - 1, rangeDecoder, numDirectBits);
                            else
                            {
                                rep0 += (rangeDecoder.DecodeDirectBits(
                                    numDirectBits - Base.kNumAlignBits) << Base.kNumAlignBits);
                                rep0 += m_PosAlignDecoder.ReverseDecode(rangeDecoder);
                            }
                        }
                        else
                            rep0 = posSlot;
                    }
                    if (rep0 >= outWindow.Total || rep0 >= dictionarySizeCheck)
                    {
                        if (rep0 == 0xFFFFFFFF)
                            return true;
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
                throw new InvalidParamException();
            int lc = properties[0] % 9;
            int remainder = properties[0] / 9;
            int lp = remainder % 5;
            int pb = remainder / 5;
            if (pb > Base.kNumPosStatesBitsMax)
                throw new InvalidParamException();
            SetLiteralProperties(lp, lc);
            SetPosBitsProperties(pb);
            Init();
            if (properties.Length >= 5)
            {
                m_DictionarySize = 0;
                for (int i = 0; i < 4; i++)
                    m_DictionarySize += properties[1 + i] << (i * 8);
            }
        }

        public void Train(System.IO.Stream stream)
        {
            if (m_OutWindow == null)
                CreateDictionary();
            m_OutWindow.Train(stream);
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
