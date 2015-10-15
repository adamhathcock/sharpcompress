namespace SharpCompress.Compressor.LZMA.RangeCoder
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitTreeDecoder
    {
        private BitDecoder[] Models;
        private int NumBitLevels;
        public BitTreeDecoder(int numBitLevels)
        {
            this.NumBitLevels = numBitLevels;
            this.Models = new BitDecoder[((int) 1) << numBitLevels];
        }

        public void Init()
        {
            for (uint i = 1; i < (((int) 1) << this.NumBitLevels); i++)
            {
                this.Models[i].Init();
            }
        }

        public uint Decode(Decoder rangeDecoder)
        {
            uint index = 1;
            for (int i = this.NumBitLevels; i > 0; i--)
            {
                index = (index << 1) + this.Models[index].Decode(rangeDecoder);
            }
            return (index - (((uint) 1) << this.NumBitLevels));
        }

        public uint ReverseDecode(Decoder rangeDecoder)
        {
            uint index = 1;
            uint num2 = 0;
            for (int i = 0; i < this.NumBitLevels; i++)
            {
                uint num4 = this.Models[index].Decode(rangeDecoder);
                index = index << 1;
                index += num4;
                num2 |= num4 << i;
            }
            return num2;
        }

        public static uint ReverseDecode(BitDecoder[] Models, uint startIndex, Decoder rangeDecoder, int NumBitLevels)
        {
            uint num = 1;
            uint num2 = 0;
            for (int i = 0; i < NumBitLevels; i++)
            {
                uint num4 = Models[startIndex + num].Decode(rangeDecoder);
                num = num << 1;
                num += num4;
                num2 |= num4 << i;
            }
            return num2;
        }
    }
}

