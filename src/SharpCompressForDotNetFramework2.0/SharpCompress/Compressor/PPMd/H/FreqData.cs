using System.Text;
namespace SharpCompress.Compressor.PPMd.H
{
    internal class FreqData : Pointer
    {
        internal const int Size = 6;

        //    struct FreqData
        //    {
        //        ushort SummFreq;
        //        STATE _PACK_ATTR * Stats;
        //    };

        internal FreqData(byte[] Memory)
            : base(Memory)
        {
        }

        internal int SummFreq
        {
            get
            {
                return Utility.readShortLittleEndian(Memory, Address) & 0xffff;
            }

            set
            {
                Utility.WriteLittleEndian(Memory, Address, (short)value);
            }

        }

        internal FreqData Initialize(byte[] mem)
        {
            return base.Initialize<FreqData>(mem);
        }

        internal void IncrementSummFreq(int dSummFreq)
        {
            Utility.incShortLittleEndian(Memory, Address, (short)dSummFreq);
        }

        internal int GetStats()
        {
            return Utility.readIntLittleEndian(Memory, Address + 2);
        }

        internal virtual void SetStats(State state)
        {
            SetStats(state.Address);
        }

        internal void SetStats(int state)
        {
            Utility.WriteLittleEndian(Memory, Address + 2, state);
        }

        public override System.String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("FreqData[");
            buffer.Append("\n  Address=");
            buffer.Append(Address);
            buffer.Append("\n  size=");
            buffer.Append(Size);
            buffer.Append("\n  summFreq=");
            buffer.Append(SummFreq);
            buffer.Append("\n  stats=");
            buffer.Append(GetStats());
            buffer.Append("\n]");
            return buffer.ToString();
        }
    }
}