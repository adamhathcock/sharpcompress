namespace SharpCompress.Compressor.PPMd.I1
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryNode
    {
        public const int Size = 12;
        public uint Address;
        public byte[] Memory;
        public static readonly MemoryNode Zero;
        public MemoryNode(uint address, byte[] memory)
        {
            this.Address = address;
            this.Memory = memory;
        }

        public uint Stamp
        {
            get
            {
                return (uint) (((this.Memory[this.Address] | (this.Memory[(int) ((IntPtr) (this.Address + 1))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 2))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 3))] << 0x18));
            }
            set
            {
                this.Memory[this.Address] = (byte) value;
                this.Memory[(int) ((IntPtr) (this.Address + 1))] = (byte) (value >> 8);
                this.Memory[(int) ((IntPtr) (this.Address + 2))] = (byte) (value >> 0x10);
                this.Memory[(int) ((IntPtr) (this.Address + 3))] = (byte) (value >> 0x18);
            }
        }
        public MemoryNode Next
        {
            get
            {
                return new MemoryNode((uint) (((this.Memory[(int) ((IntPtr) (this.Address + 4))] | (this.Memory[(int) ((IntPtr) (this.Address + 5))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 6))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 7))] << 0x18)), this.Memory);
            }
            set
            {
                this.Memory[(int) ((IntPtr) (this.Address + 4))] = (byte) value.Address;
                this.Memory[(int) ((IntPtr) (this.Address + 5))] = (byte) (value.Address >> 8);
                this.Memory[(int) ((IntPtr) (this.Address + 6))] = (byte) (value.Address >> 0x10);
                this.Memory[(int) ((IntPtr) (this.Address + 7))] = (byte) (value.Address >> 0x18);
            }
        }
        public uint UnitCount
        {
            get
            {
                return (uint) (((this.Memory[(int) ((IntPtr) (this.Address + 8))] | (this.Memory[(int) ((IntPtr) (this.Address + 9))] << 8)) | (this.Memory[(int) ((IntPtr) (this.Address + 10))] << 0x10)) | (this.Memory[(int) ((IntPtr) (this.Address + 11))] << 0x18));
            }
            set
            {
                this.Memory[(int) ((IntPtr) (this.Address + 8))] = (byte) value;
                this.Memory[(int) ((IntPtr) (this.Address + 9))] = (byte) (value >> 8);
                this.Memory[(int) ((IntPtr) (this.Address + 10))] = (byte) (value >> 0x10);
                this.Memory[(int) ((IntPtr) (this.Address + 11))] = (byte) (value >> 0x18);
            }
        }
        public bool Available
        {
            get
            {
                return (this.Next.Address != 0);
            }
        }
        public void Link(MemoryNode memoryNode)
        {
            memoryNode.Next = this.Next;
            this.Next = memoryNode;
        }

        public void Unlink()
        {
            this.Next = this.Next.Next;
        }

        public void Insert(MemoryNode memoryNode, uint unitCount)
        {
            this.Link(memoryNode);
            memoryNode.Stamp = uint.MaxValue;
            memoryNode.UnitCount = unitCount;
            this.Stamp++;
        }

        public MemoryNode Remove()
        {
            MemoryNode next = this.Next;
            this.Unlink();
            this.Stamp--;
            return next;
        }

        public static implicit operator MemoryNode(Pointer pointer)
        {
            return new MemoryNode(pointer.Address, pointer.Memory);
        }

        public static MemoryNode operator +(MemoryNode memoryNode, int offset)
        {
            memoryNode.Address += (uint) (offset * 12);
            return memoryNode;
        }

        public static MemoryNode operator +(MemoryNode memoryNode, uint offset)
        {
            memoryNode.Address += offset * 12;
            return memoryNode;
        }

        public static MemoryNode operator -(MemoryNode memoryNode, int offset)
        {
            memoryNode.Address -= (uint) (offset * 12);
            return memoryNode;
        }

        public static MemoryNode operator -(MemoryNode memoryNode, uint offset)
        {
            memoryNode.Address -= offset * 12;
            return memoryNode;
        }

        public static bool operator ==(MemoryNode memoryNode1, MemoryNode memoryNode2)
        {
            return (memoryNode1.Address == memoryNode2.Address);
        }

        public static bool operator !=(MemoryNode memoryNode1, MemoryNode memoryNode2)
        {
            return (memoryNode1.Address != memoryNode2.Address);
        }

        public override bool Equals(object obj)
        {
            if (obj is MemoryNode)
            {
                MemoryNode node = (MemoryNode) obj;
                return (node.Address == this.Address);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }

        static MemoryNode()
        {
            Zero = new MemoryNode(0, null);
        }
    }
}

