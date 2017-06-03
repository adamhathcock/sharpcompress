using System.Text;

namespace SharpCompress.Compressor.PPMd.H
{
    internal class StateRef
    {
        private int symbol;

        private int freq;

        private int successor; // pointer ppmcontext

        internal int Symbol
        {
            get
            {
                return symbol;
            }

            set
            {
                this.symbol = value & 0xff;
            }

        }
        internal int Freq
        {
            get
            {
                return freq;
            }

            set
            {
                this.freq = value & 0xff;
            }

        }

        internal State Values
        {
            set
            {
                Freq = value.Freq;
                SetSuccessor(value.GetSuccessor());
                Symbol = value.Symbol;
            }
        }


        public virtual void IncrementFreq(int dFreq)
        {
            freq = (freq + dFreq) & 0xff;
        }

        public virtual void DecrementFreq(int dFreq)
        {
            freq = (freq - dFreq) & 0xff;
        }

        public virtual int GetSuccessor()
        {
            return successor;
        }

        public virtual void SetSuccessor(PPMContext successor)
        {
            SetSuccessor(successor.Address);
        }

        public virtual void SetSuccessor(int successor)
        {
            this.successor = successor;
        }

        public override System.String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("State[");
            buffer.Append("\n  symbol=");
            buffer.Append(Symbol);
            buffer.Append("\n  freq=");
            buffer.Append(Freq);
            buffer.Append("\n  successor=");
            buffer.Append(GetSuccessor());
            buffer.Append("\n]");
            return buffer.ToString();
        }
    }
}