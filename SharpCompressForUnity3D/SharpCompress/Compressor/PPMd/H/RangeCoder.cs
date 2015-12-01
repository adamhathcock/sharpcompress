namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using SharpCompress.Compressor.Rar;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;

    internal class RangeCoder
    {
        [CompilerGenerated]
        private SharpCompress.Compressor.PPMd.H.SubRange _SubRange_k__BackingField;
        internal const int BOT = 0x8000;
        private long code;
        private long low;
        private long range;
        private Stream stream;
        internal const int TOP = 0x1000000;
        internal const long UintMask = 0xffffffffL;
        private Unpack unpackRead;

        internal RangeCoder(Unpack unpackRead)
        {
            this.unpackRead = unpackRead;
            this.Init();
        }

        internal RangeCoder(Stream stream)
        {
            this.stream = stream;
            this.Init();
        }

        internal void AriDecNormalize()
        {
            bool flag = false;
            while (((this.low ^ (this.low + this.range)) < 0x1000000L) || (flag = this.range < 0x8000L))
            {
                if (flag)
                {
                    this.range = (-this.low & 0x7fffL) & ((long) 0xffffffffL);
                    flag = false;
                }
                this.code = ((this.code << 8) | this.Char) & ((long) 0xffffffffL);
                this.range = (this.range << 8) & ((long) 0xffffffffL);
                this.low = (this.low << 8) & ((long) 0xffffffffL);
            }
        }

        internal void Decode()
        {
            this.low = (this.low + (this.range * this.SubRange.LowCount)) & ((long) 0xffffffffL);
            this.range = (this.range * (this.SubRange.HighCount - this.SubRange.LowCount)) & ((long) 0xffffffffL);
        }

        internal long GetCurrentShiftCount(int SHIFT)
        {
            this.range = Utility.URShift(this.range, SHIFT);
            return (((this.code - this.low) / this.range) & ((long) 0xffffffffL));
        }

        private void Init()
        {
            this.SubRange = new SharpCompress.Compressor.PPMd.H.SubRange();
            this.low = this.code = 0L;
            this.range = 0xffffffffL;
            for (int i = 0; i < 4; i++)
            {
                this.code = ((this.code << 8) | this.Char) & ((long) 0xffffffffL);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RangeCoder[");
            builder.Append("\n  low=");
            builder.Append(this.low);
            builder.Append("\n  code=");
            builder.Append(this.code);
            builder.Append("\n  range=");
            builder.Append(this.range);
            builder.Append("\n  subrange=");
            builder.Append(this.SubRange);
            builder.Append("]");
            return builder.ToString();
        }

        private long Char
        {
            get
            {
                if (this.unpackRead != null)
                {
                    return (long) this.unpackRead.Char;
                }
                if (this.stream != null)
                {
                    return (long) this.stream.ReadByte();
                }
                return -1L;
            }
        }

        internal int CurrentCount
        {
            get
            {
                this.range = (this.range / this.SubRange.Scale) & ((long) 0xffffffffL);
                return (int) ((this.code - this.low) / this.range);
            }
        }

        internal SharpCompress.Compressor.PPMd.H.SubRange SubRange
        {
            [CompilerGenerated]
            get
            {
                return this._SubRange_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._SubRange_k__BackingField = value;
            }
        }
    }
}

