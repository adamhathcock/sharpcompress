namespace SharpCompress.Compressor.PPMd.I1
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Pointer
    {
        public const int Size = 1;
        public uint Address;
        public byte[] Memory;
        public static readonly Pointer Zero = new Pointer(0, null);
        public Pointer(uint address, byte[] memory)
        {
            this.Address = address;
            this.Memory = memory;
        }

        public byte this[int offset]
        {
            get
            {
                if (this.Address == 0)
                {
                    throw new InvalidOperationException("The pointer being indexed is a null pointer.");
                }
                return this.Memory[(int) ((IntPtr) (this.Address + offset))];
            }
            set
            {
                if (this.Address == 0)
                {
                    throw new InvalidOperationException("The pointer being indexed is a null pointer.");
                }
                this.Memory[(int) ((IntPtr) (this.Address + offset))] = value;
            }
        }
        public static implicit operator Pointer(MemoryNode memoryNode)
        {
            return new Pointer(memoryNode.Address, memoryNode.Memory);
        }

        public static implicit operator Pointer(Model.PpmContext context)
        {
            return new Pointer(context.Address, context.Memory);
        }

        public static implicit operator Pointer(PpmState state)
        {
            return new Pointer(state.Address, state.Memory);
        }

        public static Pointer operator +(Pointer pointer, int offset)
        {
            if (pointer.Address == 0)
            {
                throw new InvalidOperationException("The pointer is a null pointer.");
            }
            pointer.Address += (uint) offset;
            return pointer;
        }

        public static Pointer operator +(Pointer pointer, uint offset)
        {
            if (pointer.Address == 0)
            {
                throw new InvalidOperationException("The pointer is a null pointer.");
            }
            pointer.Address += offset;
            return pointer;
        }

        public static Pointer operator ++(Pointer pointer)
        {
            if (pointer.Address == 0)
            {
                throw new InvalidOperationException("The pointer being incremented is a null pointer.");
            }
            pointer.Address++;
            return pointer;
        }

        public static Pointer operator -(Pointer pointer, int offset)
        {
            if (pointer.Address == 0)
            {
                throw new InvalidOperationException("The pointer is a null pointer.");
            }
            pointer.Address -= (uint) offset;
            return pointer;
        }

        public static Pointer operator -(Pointer pointer, uint offset)
        {
            if (pointer.Address == 0)
            {
                throw new InvalidOperationException("The pointer is a null pointer.");
            }
            pointer.Address -= offset;
            return pointer;
        }

        public static Pointer operator --(Pointer pointer)
        {
            if (pointer.Address == 0)
            {
                throw new InvalidOperationException("The pointer being decremented is a null pointer.");
            }
            pointer.Address--;
            return pointer;
        }

        public static uint operator -(Pointer pointer1, Pointer pointer2)
        {
            if (pointer1.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the left of the subtraction operator is a null pointer.");
            }
            if (pointer2.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the right of the subtraction operator is a null pointer.");
            }
            return (pointer1.Address - pointer2.Address);
        }

        public static bool operator <(Pointer pointer1, Pointer pointer2)
        {
            if (pointer1.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the left of the less than operator is a null pointer.");
            }
            if (pointer2.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the right of the less than operator is a null pointer.");
            }
            return (pointer1.Address < pointer2.Address);
        }

        public static bool operator <=(Pointer pointer1, Pointer pointer2)
        {
            if (pointer1.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the left of the less than or equal to operator is a null pointer.");
            }
            if (pointer2.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the right of the less than or equal to operator is a null pointer.");
            }
            return (pointer1.Address <= pointer2.Address);
        }

        public static bool operator >( Pointer pointer1, Pointer pointer2)
        {
            if (pointer1.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the left of the greater than operator is a null pointer.");
            }
            if (pointer2.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the right of the greater than operator is a null pointer.");
            }
            return (pointer1.Address > pointer2.Address);
        }

        public static bool operator >=( Pointer pointer1,  Pointer pointer2)
        {
            if (pointer1.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the left of the greater than or equal to operator is a null pointer.");
            }
            if (pointer2.Address == 0)
            {
                throw new InvalidOperationException("The pointer to the right of the greater than or equal to operator is a null pointer.");
            }
            return (pointer1.Address >= pointer2.Address);
        }

        public static bool operator ==( Pointer pointer1,  Pointer pointer2)
        {
            return (pointer1.Address == pointer2.Address);
        }

        public static bool operator !=( Pointer pointer1,  Pointer pointer2)
        {
            return (pointer1.Address != pointer2.Address);
        }

        public override bool Equals(object obj)
        {
            if (obj is  Pointer)
            {
                Pointer pointer = ( Pointer) obj;
                return (pointer.Address == this.Address);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }

        static Pointer()
        {
            Zero = new Pointer(0, null);
        }
    }
}

