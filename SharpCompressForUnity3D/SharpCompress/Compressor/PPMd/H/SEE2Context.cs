namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;
    using System.Text;

    internal class SEE2Context
    {
        private int count;
        private int shift;
        public const int size = 4;
        private int summ;

        public virtual void incSumm(int dSumm)
        {
            this.Summ += dSumm;
        }

        public void Initialize(int initVal)
        {
            this.shift = 3;
            this.summ = (initVal << this.shift) & 0xffff;
            this.count = 4;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("SEE2Context[");
            builder.Append("\n  size=");
            builder.Append(4);
            builder.Append("\n  summ=");
            builder.Append(this.summ);
            builder.Append("\n  shift=");
            builder.Append(this.shift);
            builder.Append("\n  count=");
            builder.Append(this.count);
            builder.Append("\n]");
            return builder.ToString();
        }

        public virtual void update()
        {
            if ((this.shift < 7) && (--this.count == 0))
            {
                this.summ += this.summ;
                this.count = ((int) 3) << this.shift++;
            }
            this.summ &= 0xffff;
            this.count &= 0xff;
            this.shift &= 0xff;
        }

        public virtual int Count
        {
            get
            {
                return this.count;
            }
            set
            {
                this.count = value & 0xff;
            }
        }

        public virtual int Mean
        {
            get
            {
                int num = Utility.URShift(this.summ, this.shift);
                this.summ -= num;
                return (num + ((num == 0) ? 1 : 0));
            }
        }

        public virtual int Shift
        {
            get
            {
                return this.shift;
            }
            set
            {
                this.shift = value & 0xff;
            }
        }

        public virtual int Summ
        {
            get
            {
                return this.summ;
            }
            set
            {
                this.summ = value & 0xffff;
            }
        }
    }
}

