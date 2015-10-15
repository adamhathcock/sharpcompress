namespace SharpCompress.Compressor.PPMd.I1
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PpmState
    {
        public const int Size = 6;
        public uint Address;
        public byte[] Memory;
        public static readonly PpmState Zero;
        public PpmState(uint address, byte[] memory)
        {
            this.Address = address;
            this.Memory = memory;
        }

        public byte Symbol
        {
            get
            {
                return this.Memory[this.Address];
            }
            set
            {
                this.Memory[this.Address] = value;
            }
        }
        public byte Frequency
        {
            get
            {
                return this.Memory[(int) ((IntPtr) (this.Address + 1))];
            }
            set
            {
                this.Memory[(int) ((IntPtr) (this.Address + 1))] = value;
            }
        }
        public Model.PpmContext Successor
        {
            get
            {
                return new Model.PpmContext((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 2))] | (this.Memory[(int) ((IntPtr) (this.Address + 3))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 4))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 5))] << 0x18)), this.Memory);
            }
            set
            {
                this.Memory[(int) ((IntPtr) (this.Address + 2))] = (byte) value.Address;
                this.Memory[(int) ((IntPtr) (this.Address + 3))] = (byte) (value.Address >> 8);
                this.Memory[(int) ((IntPtr) (this.Address + 4))] = (byte) (value.Address >> 0x10);
                this.Memory[(int) ((IntPtr) (this.Address + 5))] = (byte) (value.Address >> 0x18);
            }
        }
        public PpmState this[int offset]
        {
            get
            {
                return new PpmState(this.Address + ((uint) (offset * 6)), this.Memory);
            }
        }
        public static implicit operator PpmState(SharpCompress.Compressor.PPMd.I1.Pointer pointer)
        {
            return new PpmState(pointer.Address, pointer.Memory);
        }

        public static PpmState operator +(PpmState state, int offset)
        {
            state.Address += (uint) (offset * 6);
            return state;
        }

        public static PpmState operator ++(PpmState state)
        {
            state.Address += 6;
            return state;
        }

        public static PpmState operator -(PpmState state, int offset)
        {
            state.Address -= (uint) (offset * 6);
            return state;
        }

        public static PpmState operator --(PpmState state)
        {
            state.Address -= 6;
            return state;
        }

        public static bool operator <=(PpmState state1, PpmState state2)
        {
            return (state1.Address <= state2.Address);
        }

        public static bool operator >=(PpmState state1, PpmState state2)
        {
            return (state1.Address >= state2.Address);
        }

        public static bool operator ==(PpmState state1, PpmState state2)
        {
            return (state1.Address == state2.Address);
        }

        public static bool operator !=(PpmState state1, PpmState state2)
        {
            return (state1.Address != state2.Address);
        }

        public override bool Equals(object obj)
        {
            if (obj is PpmState)
            {
                PpmState state = (PpmState) obj;
                return (state.Address == this.Address);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }

        static PpmState()
        {
            Zero = new PpmState(0, null);
        }
    }
}

