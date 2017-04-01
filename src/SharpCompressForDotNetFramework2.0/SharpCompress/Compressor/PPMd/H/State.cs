using System;
using System.Text;

namespace SharpCompress.Compressor.PPMd.H
{
    internal class State : Pointer
    {
        internal const int Size = 6;

        internal State(byte[] Memory)
            : base(Memory)
        {
        }

        internal int Symbol
        {
            get
            {
                return Memory[Address] & 0xff;
            }

            set
            {
                Memory[Address] = (byte)value;
            }

        }
        internal int Freq
        {
            get
            {
                return Memory[Address + 1] & 0xff;
            }

            set
            {
                Memory[Address + 1] = (byte)value;
            }

        }

        internal State Initialize(byte[] mem)
        {
            return base.Initialize<State>(mem);
        }

        internal void IncrementFreq(int dFreq)
        {
            Memory[Address + 1] = (byte)(Memory[Address + 1] + dFreq);
        }

        internal int GetSuccessor()
        {
            return Utility.readIntLittleEndian(Memory, Address + 2);
        }

        internal void SetSuccessor(PPMContext successor)
        {
            SetSuccessor(successor.Address);
        }

        internal void SetSuccessor(int successor)
        {
            Utility.WriteLittleEndian(Memory, Address + 2, successor);
        }

        internal void SetValues(StateRef state)
        {
            Symbol = state.Symbol;
            Freq = state.Freq;
            SetSuccessor(state.GetSuccessor());
        }

        internal void SetValues(State ptr)
        {
            Array.Copy(ptr.Memory, ptr.Address, Memory, Address, Size);
        }

        internal State DecrementAddress()
        {
            Address = Address - Size;
            return this;
        }

        internal State IncrementAddress()
        {
            Address = Address + Size;
            return this;
        }

        internal static void PPMDSwap(State ptr1, State ptr2)
        {
            byte[] mem1 = ptr1.Memory, mem2 = ptr2.Memory;
            for (int i = 0, pos1 = ptr1.Address, pos2 = ptr2.Address; i < Size; i++, pos1++, pos2++)
            {
                byte temp = mem1[pos1];
                mem1[pos1] = mem2[pos2];
                mem2[pos2] = temp;
            }
        }

        public override System.String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("State[");
            buffer.Append("\n  Address=");
            buffer.Append(Address);
            buffer.Append("\n  size=");
            buffer.Append(Size);
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