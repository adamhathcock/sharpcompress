namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;
    using System.Text;

    internal class FreqData : Pointer
    {
        internal const int Size = 6;

        internal FreqData(byte[] Memory) : base(Memory)
        {
        }

        internal int GetStats()
        {
            return Utility.readIntLittleEndian(base.Memory, this.Address + 2);
        }

        internal void IncrementSummFreq(int dSummFreq)
        {
            Utility.incShortLittleEndian(base.Memory, this.Address, (short) dSummFreq);
        }

        internal FreqData Initialize(byte[] mem)
        {
            return base.Initialize<FreqData>(mem);
        }

        internal virtual void SetStats(SharpCompress.Compressor.PPMd.H.State state)
        {
            this.SetStats(state.Address);
        }

        internal void SetStats(int state)
        {
            Utility.WriteLittleEndian(base.Memory, this.Address + 2, state);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("FreqData[");
            builder.Append("\n  Address=");
            builder.Append(this.Address);
            builder.Append("\n  size=");
            builder.Append(6);
            builder.Append("\n  summFreq=");
            builder.Append(this.SummFreq);
            builder.Append("\n  stats=");
            builder.Append(this.GetStats());
            builder.Append("\n]");
            return builder.ToString();
        }

        internal int SummFreq
        {
            get
            {
                return (Utility.readShortLittleEndian(base.Memory, this.Address) & 0xffff);
            }
            set
            {
                Utility.WriteLittleEndian(base.Memory, this.Address, (short) value);
            }
        }
    }
}

