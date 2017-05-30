using System;
using System.Text;

namespace SharpCompress.Compressors.PPMd.H
{
    internal class SEE2Context
    {
        public virtual int Mean
        {
            get
            {
                int retVal = Utility.URShift(summ, shift);
                summ -= retVal;
                return retVal + ((retVal == 0) ? 1 : 0);
            }
        }

        public virtual int Count { get => count; set => count = value & 0xff; }

        public virtual int Shift { get => shift; set => shift = value & 0xff; }

        public virtual int Summ { get => summ; set => summ = value & 0xffff; }

        public const int size = 4;

        // ushort Summ;
        private int summ;

        // byte Shift;
        private int shift;

        // byte Count;
        private int count;

        public void Initialize(int initVal)
        {
            shift = (ModelPPM.PERIOD_BITS - 4) & 0xff;
            summ = (initVal << shift) & 0xffff;
            count = 4;
        }

        public virtual void update()
        {
            if (shift < ModelPPM.PERIOD_BITS && --count == 0)
            {
                summ += summ;
                count = (3 << shift++);
            }
            summ &= 0xffff;
            count &= 0xff;
            shift &= 0xff;
        }

        public virtual void incSumm(int dSumm)
        {
            Summ = Summ + dSumm;
        }

        public override String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("SEE2Context[");
            buffer.Append("\n  size=");
            buffer.Append(size);
            buffer.Append("\n  summ=");
            buffer.Append(summ);
            buffer.Append("\n  shift=");
            buffer.Append(shift);
            buffer.Append("\n  count=");
            buffer.Append(count);
            buffer.Append("\n]");
            return buffer.ToString();
        }
    }
}