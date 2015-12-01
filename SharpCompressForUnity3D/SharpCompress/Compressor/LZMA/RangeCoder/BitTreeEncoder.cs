namespace SharpCompress.Compressor.LZMA.RangeCoder
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitTreeEncoder
    {
        private BitEncoder[] Models;
        private int NumBitLevels;
        public BitTreeEncoder(int numBitLevels)
        {
            this.NumBitLevels = numBitLevels;
            this.Models = new BitEncoder[((int) 1) << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (((int) 1) << this.NumBitLevels); i++)
            {
                this.Models[i].Init();
            }
        }

        public void Encode(Encoder rangeEncoder, uint symbol)
        {
            uint index = 1;
            int numBitLevels = this.NumBitLevels;
            while (numBitLevels > 0)
            {
                numBitLevels--;
                uint num3 = (symbol >> numBitLevels) & 1;
                this.Models[index].Encode(rangeEncoder, num3);
                index = (index << 1) | num3;
            }
        }

        public void ReverseEncode(Encoder rangeEncoder, uint symbol)
        {
            uint index = 1;
            for (uint i = 0; i < this.NumBitLevels; i++)
            {
                uint num3 = symbol & 1;
                this.Models[index].Encode(rangeEncoder, num3);
                index = (index << 1) | num3;
                symbol = symbol >> 1;
            }
        }

        public uint GetPrice(uint symbol)
        {
            uint num = 0;
            uint index = 1;
            int numBitLevels = this.NumBitLevels;
            while (numBitLevels > 0)
            {
                numBitLevels--;
                uint num4 = (symbol >> numBitLevels) & 1;
                num += this.Models[index].GetPrice(num4);
                index = (index << 1) + num4;
            }
            return num;
        }

        public uint ReverseGetPrice(uint symbol)
        {
            uint num = 0;
            uint index = 1;
            for (int i = this.NumBitLevels; i > 0; i--)
            {
                uint num4 = symbol & 1;
                symbol = symbol >> 1;
                num += this.Models[index].GetPrice(num4);
                index = (index << 1) | num4;
            }
            return num;
        }

        public static uint ReverseGetPrice(BitEncoder[] Models, uint startIndex, int NumBitLevels, uint symbol)
        {
            uint num = 0;
            uint num2 = 1;
            for (int i = NumBitLevels; i > 0; i--)
            {
                uint num4 = symbol & 1;
                symbol = symbol >> 1;
                num += Models[startIndex + num2].GetPrice(num4);
                num2 = (num2 << 1) | num4;
            }
            return num;
        }

        public static void ReverseEncode(BitEncoder[] Models, uint startIndex, Encoder rangeEncoder, int NumBitLevels, uint symbol)
        {
            uint num = 1;
            for (int i = 0; i < NumBitLevels; i++)
            {
                uint num3 = symbol & 1;
                Models[startIndex + num].Encode(rangeEncoder, num3);
                num = (num << 1) | num3;
                symbol = symbol >> 1;
            }
        }
    }
}

