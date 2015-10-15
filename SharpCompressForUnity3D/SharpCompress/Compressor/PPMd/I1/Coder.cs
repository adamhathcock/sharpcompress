namespace SharpCompress.Compressor.PPMd.I1
{
    using System;
    using System.IO;

    internal class Coder
    {
        private uint code;
        public uint HighCount;
        private uint low;
        public uint LowCount;
        private uint range;
        private const uint RangeBottom = 0x8000;
        private const uint RangeTop = 0x1000000;
        public uint Scale;

        public void RangeDecoderInitialize(Stream stream)
        {
            this.low = 0;
            this.code = 0;
            this.range = uint.MaxValue;
            for (uint i = 0; i < 4; i++)
            {
                this.code = (this.code << 8) | ((byte) stream.ReadByte());
            }
        }

        public void RangeDecoderNormalize(Stream stream)
        {
            while ((((this.low ^ (this.low + this.range)) < 0x1000000) ? 1 : ((this.range >= 0x8000) ? 0 : 1)) != 0)
            {
                this.code = (this.code << 8) | ((byte) stream.ReadByte());
                this.range = this.range << 8;
                this.low = this.low << 8;
            }
        }

        public void RangeEncoderFlush(Stream stream)
        {
            for (uint i = 0; i < 4; i++)
            {
                stream.WriteByte((byte) (this.low >> 0x18));
                this.low = this.low << 8;
            }
        }

        public void RangeEncoderInitialize()
        {
            this.low = 0;
            this.range = uint.MaxValue;
        }

        public void RangeEncoderNormalize(Stream stream)
        {
            while ((((this.low ^ (this.low + this.range)) < 0x1000000) ? 1 : ((this.range >= 0x8000) ? 0 : 1)) != 0)
            {
                stream.WriteByte((byte) (this.low >> 0x18));
                this.range = this.range << 8;
                this.low = this.low << 8;
            }
        }

        public void RangeEncodeSymbol()
        {
            this.low += this.LowCount * (this.range /= this.Scale);
            this.range *= this.HighCount - this.LowCount;
        }

        public uint RangeGetCurrentCount()
        {
            return ((this.code - this.low) / (this.range /= this.Scale));
        }

        public uint RangeGetCurrentShiftCount(int rangeShift)
        {
            return ((this.code - this.low) / (this.range = this.range >> rangeShift));
        }

        public void RangeRemoveSubrange()
        {
            this.low += this.range * this.LowCount;
            this.range *= this.HighCount - this.LowCount;
        }

        public void RangeShiftEncodeSymbol(int rangeShift)
        {
            this.low += this.LowCount * (this.range = this.range >> rangeShift);
            this.range *= this.HighCount - this.LowCount;
        }
    }
}

