namespace SharpCompress.Compressor.PPMd.H
{
    using System;
    using System.Text;

    internal class StateRef
    {
        private int freq;
        private int successor;
        private int symbol;

        public virtual void DecrementFreq(int dFreq)
        {
            this.freq = (this.freq - dFreq) & 0xff;
        }

        public virtual int GetSuccessor()
        {
            return this.successor;
        }

        public virtual void IncrementFreq(int dFreq)
        {
            this.freq = (this.freq + dFreq) & 0xff;
        }

        public virtual void SetSuccessor(PPMContext successor)
        {
            this.SetSuccessor(successor.Address);
        }

        public virtual void SetSuccessor(int successor)
        {
            this.successor = successor;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("State[");
            builder.Append("\n  symbol=");
            builder.Append(this.Symbol);
            builder.Append("\n  freq=");
            builder.Append(this.Freq);
            builder.Append("\n  successor=");
            builder.Append(this.GetSuccessor());
            builder.Append("\n]");
            return builder.ToString();
        }

        internal int Freq
        {
            get
            {
                return this.freq;
            }
            set
            {
                this.freq = value & 0xff;
            }
        }

        internal int Symbol
        {
            get
            {
                return this.symbol;
            }
            set
            {
                this.symbol = value & 0xff;
            }
        }

        internal SharpCompress.Compressor.PPMd.H.State Values
        {
            set
            {
                this.Freq = value.Freq;
                this.SetSuccessor(value.GetSuccessor());
                this.Symbol = value.Symbol;
            }
        }
    }
}

