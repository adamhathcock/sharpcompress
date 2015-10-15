namespace SharpCompress.Compressor.LZMA
{
    using SharpCompress.Compressor.LZMA.LZ;
    using SharpCompress.Compressor.LZMA.RangeCoder;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    internal class Decoder : ICoder, ISetDecoderProperties
    {
        private int m_DictionarySize = -1;
        private BitDecoder[] m_IsMatchDecoders = new BitDecoder[0xc0];
        private BitDecoder[] m_IsRep0LongDecoders = new BitDecoder[0xc0];
        private BitDecoder[] m_IsRepDecoders = new BitDecoder[12];
        private BitDecoder[] m_IsRepG0Decoders = new BitDecoder[12];
        private BitDecoder[] m_IsRepG1Decoders = new BitDecoder[12];
        private BitDecoder[] m_IsRepG2Decoders = new BitDecoder[12];
        private LenDecoder m_LenDecoder = new LenDecoder();
        private LiteralDecoder m_LiteralDecoder = new LiteralDecoder();
        private OutWindow m_OutWindow;
        private BitTreeDecoder m_PosAlignDecoder = new BitTreeDecoder(4);
        private BitDecoder[] m_PosDecoders = new BitDecoder[0x72];
        private BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[4];
        private uint m_PosStateMask;
        private LenDecoder m_RepLenDecoder = new LenDecoder();
        private uint rep0;
        private uint rep1;
        private uint rep2;
        private uint rep3;
        private Base.State state = new Base.State();

        public Decoder()
        {
            for (int i = 0; i < 4L; i++)
            {
                this.m_PosSlotDecoder[i] = new BitTreeDecoder(6);
            }
        }

        internal bool Code(int dictionarySize, OutWindow outWindow, SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder)
        {
            int num = Math.Max(dictionarySize, 1);
            outWindow.CopyPending();
            while (outWindow.HasSpace)
            {
                uint posState = ((uint) outWindow.Total) & this.m_PosStateMask;
                if (this.m_IsMatchDecoders[(this.state.Index << 4) + posState].Decode(rangeDecoder) == 0)
                {
                    byte num3;
                    byte @byte = outWindow.GetByte(0);
                    if (!this.state.IsCharState())
                    {
                        num3 = this.m_LiteralDecoder.DecodeWithMatchByte(rangeDecoder, (uint) outWindow.Total, @byte, outWindow.GetByte((int) this.rep0));
                    }
                    else
                    {
                        num3 = this.m_LiteralDecoder.DecodeNormal(rangeDecoder, (uint) outWindow.Total, @byte);
                    }
                    outWindow.PutByte(num3);
                    this.state.UpdateChar();
                }
                else
                {
                    uint num5;
                    if (this.m_IsRepDecoders[this.state.Index].Decode(rangeDecoder) == 1)
                    {
                        if (this.m_IsRepG0Decoders[this.state.Index].Decode(rangeDecoder) == 0)
                        {
                            if (this.m_IsRep0LongDecoders[(this.state.Index << 4) + posState].Decode(rangeDecoder) == 0)
                            {
                                this.state.UpdateShortRep();
                                outWindow.PutByte(outWindow.GetByte((int) this.rep0));
                                continue;
                            }
                        }
                        else
                        {
                            uint num6;
                            if (this.m_IsRepG1Decoders[this.state.Index].Decode(rangeDecoder) == 0)
                            {
                                num6 = this.rep1;
                            }
                            else
                            {
                                if (this.m_IsRepG2Decoders[this.state.Index].Decode(rangeDecoder) == 0)
                                {
                                    num6 = this.rep2;
                                }
                                else
                                {
                                    num6 = this.rep3;
                                    this.rep3 = this.rep2;
                                }
                                this.rep2 = this.rep1;
                            }
                            this.rep1 = this.rep0;
                            this.rep0 = num6;
                        }
                        num5 = this.m_RepLenDecoder.Decode(rangeDecoder, posState) + 2;
                        this.state.UpdateRep();
                    }
                    else
                    {
                        this.rep3 = this.rep2;
                        this.rep2 = this.rep1;
                        this.rep1 = this.rep0;
                        num5 = 2 + this.m_LenDecoder.Decode(rangeDecoder, posState);
                        this.state.UpdateMatch();
                        uint num7 = this.m_PosSlotDecoder[Base.GetLenToPosState(num5)].Decode(rangeDecoder);
                        if (num7 >= 4)
                        {
                            int numBitLevels = ((int) (num7 >> 1)) - 1;
                            this.rep0 = (uint) ((2 | (num7 & 1)) << (numBitLevels & 0x1f));
                            if (num7 < 14)
                            {
                                this.rep0 += BitTreeDecoder.ReverseDecode(this.m_PosDecoders, (this.rep0 - num7) - 1, rangeDecoder, numBitLevels);
                            }
                            else
                            {
                                this.rep0 += rangeDecoder.DecodeDirectBits(numBitLevels - 4) << 4;
                                this.rep0 += this.m_PosAlignDecoder.ReverseDecode(rangeDecoder);
                            }
                        }
                        else
                        {
                            this.rep0 = num7;
                        }
                    }
                    if ((this.rep0 >= outWindow.Total) || (this.rep0 >= num))
                    {
                        if (this.rep0 != uint.MaxValue)
                        {
                            throw new DataErrorException();
                        }
                        return true;
                    }
                    outWindow.CopyBlock((int) this.rep0, (int) num5);
                }
            }
            return false;
        }

        public void Code(Stream inStream, Stream outStream, long inSize, long outSize, ICodeProgress progress)
        {
            if (this.m_OutWindow == null)
            {
                this.CreateDictionary();
            }
            this.m_OutWindow.Init(outStream);
            if (outSize > 0L)
            {
                this.m_OutWindow.SetLimit(outSize);
            }
            else
            {
                this.m_OutWindow.SetLimit(0x7fffffffffffffffL - this.m_OutWindow.Total);
            }
            SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder = new SharpCompress.Compressor.LZMA.RangeCoder.Decoder();
            rangeDecoder.Init(inStream);
            this.Code(this.m_DictionarySize, this.m_OutWindow, rangeDecoder);
            this.m_OutWindow.ReleaseStream();
            rangeDecoder.ReleaseStream();
            if (!(rangeDecoder.IsFinished && ((inSize <= 0L) || (rangeDecoder.Total == inSize))))
            {
                throw new DataErrorException();
            }
            if (this.m_OutWindow.HasPending)
            {
                throw new DataErrorException();
            }
            this.m_OutWindow = null;
        }

        private void CreateDictionary()
        {
            if (this.m_DictionarySize < 0)
            {
                throw new InvalidParamException();
            }
            this.m_OutWindow = new OutWindow();
            int windowSize = Math.Max(this.m_DictionarySize, 0x1000);
            this.m_OutWindow.Create(windowSize);
        }

        private void Init()
        {
            uint num;
            for (num = 0; num < 12; num++)
            {
                for (uint i = 0; i <= this.m_PosStateMask; i++)
                {
                    uint index = (num << 4) + i;
                    this.m_IsMatchDecoders[index].Init();
                    this.m_IsRep0LongDecoders[index].Init();
                }
                this.m_IsRepDecoders[num].Init();
                this.m_IsRepG0Decoders[num].Init();
                this.m_IsRepG1Decoders[num].Init();
                this.m_IsRepG2Decoders[num].Init();
            }
            this.m_LiteralDecoder.Init();
            for (num = 0; num < 4; num++)
            {
                this.m_PosSlotDecoder[num].Init();
            }
            for (num = 0; num < 0x72; num++)
            {
                this.m_PosDecoders[num].Init();
            }
            this.m_LenDecoder.Init();
            this.m_RepLenDecoder.Init();
            this.m_PosAlignDecoder.Init();
            this.state.Init();
            this.rep0 = 0;
            this.rep1 = 0;
            this.rep2 = 0;
            this.rep3 = 0;
        }

        public void SetDecoderProperties(byte[] properties)
        {
            if (properties.Length < 1)
            {
                throw new InvalidParamException();
            }
            int lc = properties[0] % 9;
            int num2 = properties[0] / 9;
            int lp = num2 % 5;
            int pb = num2 / 5;
            if (pb > 4)
            {
                throw new InvalidParamException();
            }
            this.SetLiteralProperties(lp, lc);
            this.SetPosBitsProperties(pb);
            this.Init();
            if (properties.Length >= 5)
            {
                this.m_DictionarySize = 0;
                for (int i = 0; i < 4; i++)
                {
                    this.m_DictionarySize += properties[1 + i] << (i * 8);
                }
            }
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
            this.m_LiteralDecoder.Create(lp, lc);
        }

        private void SetPosBitsProperties(int pb)
        {
            if (pb > 4)
            {
                throw new InvalidParamException();
            }
            uint numPosStates = ((uint) 1) << pb;
            this.m_LenDecoder.Create(numPosStates);
            this.m_RepLenDecoder.Create(numPosStates);
            this.m_PosStateMask = numPosStates - 1;
        }

        public void Train(Stream stream)
        {
            if (this.m_OutWindow == null)
            {
                this.CreateDictionary();
            }
            this.m_OutWindow.Train(stream);
        }

        private class LenDecoder
        {
            private BitDecoder m_Choice = new BitDecoder();
            private BitDecoder m_Choice2 = new BitDecoder();
            private BitTreeDecoder m_HighCoder = new BitTreeDecoder(8);
            private BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[0x10];
            private BitTreeDecoder[] m_MidCoder = new BitTreeDecoder[0x10];
            private uint m_NumPosStates = 0;

            public void Create(uint numPosStates)
            {
                for (uint i = this.m_NumPosStates; i < numPosStates; i++)
                {
                    this.m_LowCoder[i] = new BitTreeDecoder(3);
                    this.m_MidCoder[i] = new BitTreeDecoder(3);
                }
                this.m_NumPosStates = numPosStates;
            }

            public uint Decode(SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder, uint posState)
            {
                if (this.m_Choice.Decode(rangeDecoder) == 0)
                {
                    return this.m_LowCoder[posState].Decode(rangeDecoder);
                }
                uint num = 8;
                if (this.m_Choice2.Decode(rangeDecoder) == 0)
                {
                    num += this.m_MidCoder[posState].Decode(rangeDecoder);
                }
                else
                {
                    num += 8;
                    num += this.m_HighCoder.Decode(rangeDecoder);
                }
                return num;
            }

            public void Init()
            {
                this.m_Choice.Init();
                for (uint i = 0; i < this.m_NumPosStates; i++)
                {
                    this.m_LowCoder[i].Init();
                    this.m_MidCoder[i].Init();
                }
                this.m_Choice2.Init();
                this.m_HighCoder.Init();
            }
        }

        private class LiteralDecoder
        {
            private Decoder2[] m_Coders;
            private int m_NumPosBits;
            private int m_NumPrevBits;
            private uint m_PosMask;

            public void Create(int numPosBits, int numPrevBits)
            {
                if (((this.m_Coders == null) || (this.m_NumPrevBits != numPrevBits)) || (this.m_NumPosBits != numPosBits))
                {
                    this.m_NumPosBits = numPosBits;
                    this.m_PosMask = (uint) ((((int) 1) << numPosBits) - 1);
                    this.m_NumPrevBits = numPrevBits;
                    uint num = ((uint) 1) << (this.m_NumPrevBits + this.m_NumPosBits);
                    this.m_Coders = new Decoder2[num];
                    for (uint i = 0; i < num; i++)
                    {
                        this.m_Coders[i].Create();
                    }
                }
            }

            public byte DecodeNormal(SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte)
            {
                return this.m_Coders[this.GetState(pos, prevByte)].DecodeNormal(rangeDecoder);
            }

            public byte DecodeWithMatchByte(SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
            {
                return this.m_Coders[this.GetState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte);
            }

            private uint GetState(uint pos, byte prevByte)
            {
                return (((pos & this.m_PosMask) << this.m_NumPrevBits) + ((uint) (prevByte >> (8 - this.m_NumPrevBits))));
            }

            public void Init()
            {
                uint num = ((uint) 1) << (this.m_NumPrevBits + this.m_NumPosBits);
                for (uint i = 0; i < num; i++)
                {
                    this.m_Coders[i].Init();
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Decoder2
            {
                private BitDecoder[] m_Decoders;
                public void Create()
                {
                    this.m_Decoders = new BitDecoder[0x300];
                }

                public void Init()
                {
                    for (int i = 0; i < 0x300; i++)
                    {
                        this.m_Decoders[i].Init();
                    }
                }

                public byte DecodeNormal(SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder)
                {
                    uint index = 1;
                    do
                    {
                        index = (index << 1) | this.m_Decoders[index].Decode(rangeDecoder);
                    }
                    while (index < 0x100);
                    return (byte) index;
                }

                public byte DecodeWithMatchByte(SharpCompress.Compressor.LZMA.RangeCoder.Decoder rangeDecoder, byte matchByte)
                {
                    uint index = 1;
                    do
                    {
                        uint num2 = (uint) ((matchByte >> 7) & 1);
                        matchByte = (byte) (matchByte << 1);
                        uint num3 = this.m_Decoders[(int) ((IntPtr) (((1 + num2) << 8) + index))].Decode(rangeDecoder);
                        index = (index << 1) | num3;
                        if (num2 != num3)
                        {
                            while (index < 0x100)
                            {
                                index = (index << 1) | this.m_Decoders[index].Decode(rangeDecoder);
                            }
                            break;
                        }
                    }
                    while (index < 0x100);
                    return (byte) index;
                }
            }
        }
    }
}

