namespace SharpCompress.Compressor.LZMA.RangeCoder
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitDecoder
    {
        public const int kNumBitModelTotalBits = 11;
        public const uint kBitModelTotal = 0x800;
        private const int kNumMoveBits = 5;
        private uint Prob;
        public void UpdateModel(int numMoveBits, uint symbol)
        {
            if (symbol == 0)
            {
                this.Prob += (uint) ((0x800 - this.Prob) >> (numMoveBits & 0x1f));
            }
            else
            {
                this.Prob -= this.Prob >> numMoveBits;
            }
        }

        public void Init()
        {
            this.Prob = 0x400;
        }

        public uint Decode(Decoder rangeDecoder)
        {
            uint num = (rangeDecoder.Range >> 11) * this.Prob;
            if (rangeDecoder.Code < num)
            {
                rangeDecoder.Range = num;
                this.Prob += (uint) ((0x800 - this.Prob) >> 5);
                if (rangeDecoder.Range < 0x1000000)
                {
                    rangeDecoder.Code = (rangeDecoder.Code << 8) | ((byte) rangeDecoder.Stream.ReadByte());
                    rangeDecoder.Range = rangeDecoder.Range << 8;
                    rangeDecoder.Total += 1L;
                }
                return 0;
            }
            rangeDecoder.Range -= num;
            rangeDecoder.Code -= num;
            this.Prob -= this.Prob >> 5;
            if (rangeDecoder.Range < 0x1000000)
            {
                rangeDecoder.Code = (rangeDecoder.Code << 8) | ((byte) rangeDecoder.Stream.ReadByte());
                rangeDecoder.Range = rangeDecoder.Range << 8;
                rangeDecoder.Total += 1L;
            }
            return 1;
        }
    }
}

