namespace SharpCompress.Compressor.PPMd.H
{
    using System;
    using System.Text;

    internal class SubRange
    {
        private long highCount;
        private long lowCount;
        private long scale;

        internal void incScale(int dScale)
        {
            this.Scale += dScale;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("SubRange[");
            builder.Append("\n  lowCount=");
            builder.Append(this.lowCount);
            builder.Append("\n  highCount=");
            builder.Append(this.highCount);
            builder.Append("\n  scale=");
            builder.Append(this.scale);
            builder.Append("]");
            return builder.ToString();
        }

        internal long HighCount
        {
            get
            {
                return this.highCount;
            }
            set
            {
                this.highCount = value & ((long) 0xffffffffL);
            }
        }

        internal long LowCount
        {
            get
            {
                return (this.lowCount & ((long) 0xffffffffL));
            }
            set
            {
                this.lowCount = value & ((long) 0xffffffffL);
            }
        }

        internal long Scale
        {
            get
            {
                return this.scale;
            }
            set
            {
                this.scale = value & ((long) 0xffffffffL);
            }
        }
    }
}

