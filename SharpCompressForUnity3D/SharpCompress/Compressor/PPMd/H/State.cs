namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;
    using System.Text;

    internal class State : Pointer
    {
        internal const int Size = 6;

        internal State(byte[] Memory) : base(Memory)
        {
        }

        internal SharpCompress.Compressor.PPMd.H.State DecrementAddress()
        {
            this.Address -= 6;
            return this;
        }

        internal int GetSuccessor()
        {
            return Utility.readIntLittleEndian(base.Memory, this.Address + 2);
        }

        internal SharpCompress.Compressor.PPMd.H.State IncrementAddress()
        {
            this.Address += 6;
            return this;
        }

        internal void IncrementFreq(int dFreq)
        {
            base.Memory[this.Address + 1] = (byte) (base.Memory[this.Address + 1] + dFreq);
        }

        internal SharpCompress.Compressor.PPMd.H.State Initialize(byte[] mem)
        {
            return base.Initialize<SharpCompress.Compressor.PPMd.H.State>(mem);
        }

        internal static void PPMDSwap(SharpCompress.Compressor.PPMd.H.State ptr1, SharpCompress.Compressor.PPMd.H.State ptr2)
        {
            byte[] memory = ptr1.Memory;
            byte[] buffer2 = ptr2.Memory;
            int num = 0;
            int address = ptr1.Address;
            for (int i = ptr2.Address; num < 6; i++)
            {
                byte num4 = memory[address];
                memory[address] = buffer2[i];
                buffer2[i] = num4;
                num++;
                address++;
            }
        }

        internal void SetSuccessor(PPMContext successor)
        {
            this.SetSuccessor(successor.Address);
        }

        internal void SetSuccessor(int successor)
        {
            Utility.WriteLittleEndian(base.Memory, this.Address + 2, successor);
        }

        internal void SetValues(SharpCompress.Compressor.PPMd.H.State ptr)
        {
            Array.Copy(ptr.Memory, ptr.Address, base.Memory, this.Address, 6);
        }

        internal void SetValues(StateRef state)
        {
            this.Symbol = state.Symbol;
            this.Freq = state.Freq;
            this.SetSuccessor(state.GetSuccessor());
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("State[");
            builder.Append("\n  Address=");
            builder.Append(this.Address);
            builder.Append("\n  size=");
            builder.Append(6);
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
                return (base.Memory[this.Address + 1] & 0xff);
            }
            set
            {
                base.Memory[this.Address + 1] = (byte) value;
            }
        }

        internal int Symbol
        {
            get
            {
                return (base.Memory[this.Address] & 0xff);
            }
            set
            {
                base.Memory[this.Address] = (byte) value;
            }
        }
    }
}

