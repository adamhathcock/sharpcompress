namespace SharpCompress.Compressor.LZMA.RangeCoder
{
    using System;
    using System.IO;

    internal class Decoder
    {
        public uint Code = 0;
        public const uint kTopValue = 0x1000000;
        public uint Range;
        public System.IO.Stream Stream;
        public long Total;

        public void CloseStream()
        {
            this.Stream.Dispose();
        }

        public void Decode(uint start, uint size)
        {
            this.Code -= start * this.Range;
            this.Range *= size;
            this.Normalize();
        }

        public uint DecodeBit(uint size0, int numTotalBits)
        {
            uint num2;
            uint num = (this.Range >> numTotalBits) * size0;
            if (this.Code < num)
            {
                num2 = 0;
                this.Range = num;
            }
            else
            {
                num2 = 1;
                this.Code -= num;
                this.Range -= num;
            }
            this.Normalize();
            return num2;
        }

        public uint DecodeDirectBits(int numTotalBits)
        {
            uint range = this.Range;
            uint code = this.Code;
            uint num3 = 0;
            for (int i = numTotalBits; i > 0; i--)
            {
                range = range >> 1;
                uint num5 = (code - range) >> 0x1f;
                code -= range & (num5 - 1);
                num3 = (num3 << 1) | (1 - num5);
                if (range < 0x1000000)
                {
                    code = (code << 8) | ((byte) this.Stream.ReadByte());
                    range = range << 8;
                    this.Total += 1L;
                }
            }
            this.Range = range;
            this.Code = code;
            return num3;
        }

        public uint GetThreshold(uint total)
        {
            return (this.Code / (this.Range /= total));
        }

        public void Init(System.IO.Stream stream)
        {
            this.Stream = stream;
            this.Code = 0;
            this.Range = uint.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                this.Code = (this.Code << 8) | ((byte) this.Stream.ReadByte());
            }
            this.Total = 5L;
        }

        public void Normalize()
        {
            while (this.Range < 0x1000000)
            {
                this.Code = (this.Code << 8) | ((byte) this.Stream.ReadByte());
                this.Range = this.Range << 8;
                this.Total += 1L;
            }
        }

        public void Normalize2()
        {
            if (this.Range < 0x1000000)
            {
                this.Code = (this.Code << 8) | ((byte) this.Stream.ReadByte());
                this.Range = this.Range << 8;
                this.Total += 1L;
            }
        }

        public void ReleaseStream()
        {
            this.Stream = null;
        }

        public bool IsFinished
        {
            get
            {
                return (this.Code == 0);
            }
        }
    }
}

