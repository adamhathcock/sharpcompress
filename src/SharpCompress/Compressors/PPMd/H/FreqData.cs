using System;
using System.Buffers.Binary;
using System.Text;

namespace SharpCompress.Compressors.PPMd.H
{
    internal class FreqData : Pointer
    {
        internal const int SIZE = 6;

        //    struct FreqData
        //    {
        //        ushort SummFreq;
        //        STATE _PACK_ATTR * Stats;
        //    };

        internal FreqData(byte[] memory)
            : base(memory)
        {
        }

        internal int SummFreq
        {
            get => BinaryPrimitives.ReadInt16LittleEndian(Memory.AsSpan(Address)) & 0xffff;
            set => BinaryPrimitives.WriteInt16LittleEndian(Memory.AsSpan(Address), (short)value);
        }

        internal FreqData Initialize(byte[] mem)
        {
            return base.Initialize<FreqData>(mem);
        }

        internal void IncrementSummFreq(int dSummFreq)
        {
            SummFreq += (short)dSummFreq;
        }

        internal int GetStats()
        {
            return BinaryPrimitives.ReadInt32LittleEndian(Memory.AsSpan(Address + 2));
        }

        internal virtual void SetStats(State state)
        {
            SetStats(state.Address);
        }

        internal void SetStats(int state)
        {
            BinaryPrimitives.WriteInt32LittleEndian(Memory.AsSpan(Address + 2), state);
        }

        public override String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("FreqData[");
            buffer.Append("\n  Address=");
            buffer.Append(Address);
            buffer.Append("\n  size=");
            buffer.Append(SIZE);
            buffer.Append("\n  summFreq=");
            buffer.Append(SummFreq);
            buffer.Append("\n  stats=");
            buffer.Append(GetStats());
            buffer.Append("\n]");
            return buffer.ToString();
        }
    }
}
