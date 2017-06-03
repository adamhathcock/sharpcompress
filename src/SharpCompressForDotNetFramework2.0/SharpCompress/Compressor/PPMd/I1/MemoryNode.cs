#region Using

using System;

#endregion

namespace SharpCompress.Compressor.PPMd.I1
{
    /// <summary>
    /// A structure containing a single address.  The address represents a location in the <see cref="Memory"/>
    /// array.  That location in the <see cref="Memory"/> array contains information itself describing a section
    /// of the <see cref="Memory"/> array (ie. a block of memory).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This must be a structure rather than a class because several places in the associated code assume that
    /// <see cref="MemoryNode"/> is a value type (meaning that assignment creates a completely new copy of
    /// the instance rather than just copying a reference to the same instance).
    /// </para>
    /// <para>
    /// MemoryNode
    ///     4 Stamp
    ///     4 Next
    ///     4 UnitCount
    /// </para>
    /// <para>
    /// Note that <see cref="Address"/> is a field rather than a property for performance reasons.
    /// </para>
    /// </remarks>
    internal struct MemoryNode
    {
        public uint Address;
        public byte[] Memory;
        public static readonly MemoryNode Zero = new MemoryNode(0, null);
        public const int Size = 12;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryNode"/> structure.
        /// </summary>
        public MemoryNode(uint address, byte[] memory)
        {
            Address = address;
            Memory = memory;
        }

        /// <summary>
        /// Gets or sets the stamp.
        /// </summary>
        public uint Stamp
        {
            get { return ((uint) Memory[Address]) | ((uint) Memory[Address + 1]) << 8 | ((uint) Memory[Address + 2]) << 16 | ((uint) Memory[Address + 3]) << 24; }
            set
            {
                Memory[Address] = (byte) value;
                Memory[Address + 1] = (byte) (value >> 8);
                Memory[Address + 2] = (byte) (value >> 16);
                Memory[Address + 3] = (byte) (value >> 24);
            }
        }

        /// <summary>
        /// Gets or sets the next memory node.
        /// </summary>
        public MemoryNode Next
        {
            get { return new MemoryNode(((uint) Memory[Address + 4]) | ((uint) Memory[Address + 5]) << 8 | ((uint) Memory[Address + 6]) << 16 | ((uint) Memory[Address + 7]) << 24, Memory); }
            set
            {
                Memory[Address + 4] = (byte) value.Address;
                Memory[Address + 5] = (byte) (value.Address >> 8);
                Memory[Address + 6] = (byte) (value.Address >> 16);
                Memory[Address + 7] = (byte) (value.Address >> 24);
            }
        }

        /// <summary>
        /// Gets or sets the unit count.
        /// </summary>
        public uint UnitCount
        {
            get { return ((uint) Memory[Address + 8]) | ((uint) Memory[Address + 9]) << 8 | ((uint) Memory[Address + 10]) << 16 | ((uint) Memory[Address + 11]) << 24; }
            set
            {
                Memory[Address + 8] = (byte) value;
                Memory[Address + 9] = (byte) (value >> 8);
                Memory[Address + 10] = (byte) (value >> 16);
                Memory[Address + 11] = (byte) (value >> 24);
            }
        }

        /// <summary>
        /// Gets whether there is a next memory node available.
        /// </summary>
        public bool Available
        {
            get { return Next.Address != 0; }
        }

        /// <summary>
        /// Link in the provided memory node.
        /// </summary>
        /// <param name="memoryNode"></param>
        public void Link(MemoryNode memoryNode)
        {
            memoryNode.Next = Next;
            Next = memoryNode;
        }

        /// <summary>
        /// Unlink this memory node.
        /// </summary>
        public void Unlink()
        {
            Next = Next.Next;
        }

        /// <summary>
        /// Insert the memory node into the linked list.
        /// </summary>
        /// <param name="memoryNode"></param>
        /// <param name="unitCount"></param>
        public void Insert(MemoryNode memoryNode, uint unitCount)
        {
            Link(memoryNode);
            memoryNode.Stamp = uint.MaxValue;
            memoryNode.UnitCount = unitCount;
            Stamp++;
        }

        /// <summary>
        /// Remove this memory node from the linked list.
        /// </summary>
        /// <returns></returns>
        public MemoryNode Remove()
        {
            MemoryNode next = Next;
            Unlink();
            Stamp--;
            return next;
        }

        /// <summary>
        /// Allow a pointer to be implicitly converted to a memory node.
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        public static implicit operator MemoryNode(Pointer pointer)
        {
            return new MemoryNode(pointer.Address, pointer.Memory);
        }

        /// <summary>
        /// Allow pointer-like addition on a memory node.
        /// </summary>
        /// <param name="memoryNode"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static MemoryNode operator +(MemoryNode memoryNode, int offset)
        {
            memoryNode.Address = (uint) (memoryNode.Address + offset * Size);
            return memoryNode;
        }

        /// <summary>
        /// Allow pointer-like addition on a memory node.
        /// </summary>
        /// <param name="memoryNode"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static MemoryNode operator +(MemoryNode memoryNode, uint offset)
        {
            memoryNode.Address += offset * Size;
            return memoryNode;
        }

        /// <summary>
        /// Allow pointer-like subtraction on a memory node.
        /// </summary>
        /// <param name="memoryNode"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static MemoryNode operator -(MemoryNode memoryNode, int offset)
        {
            memoryNode.Address = (uint) (memoryNode.Address - offset * Size);
            return memoryNode;
        }

        /// <summary>
        /// Allow pointer-like subtraction on a memory node.
        /// </summary>
        /// <param name="memoryNode"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static MemoryNode operator -(MemoryNode memoryNode, uint offset)
        {
            memoryNode.Address -= offset * Size;
            return memoryNode;
        }

        /// <summary>
        /// Compare two memory nodes.
        /// </summary>
        /// <param name="memoryNode1"></param>
        /// <param name="memoryNode2"></param>
        /// <returns></returns>
        public static bool operator ==(MemoryNode memoryNode1, MemoryNode memoryNode2)
        {
            return memoryNode1.Address == memoryNode2.Address;
        }

        /// <summary>
        /// Compare two memory nodes.
        /// </summary>
        /// <param name="memoryNode1"></param>
        /// <param name="memoryNode2"></param>
        /// <returns></returns>
        public static bool operator !=(MemoryNode memoryNode1, MemoryNode memoryNode2)
        {
            return memoryNode1.Address != memoryNode2.Address;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        /// <param name="obj">Another object to compare to.</param>
        public override bool Equals(object obj)
        {
            if (obj is MemoryNode)
            {
                MemoryNode memoryNode = (MemoryNode) obj;
                return memoryNode.Address == Address;
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return Address.GetHashCode();
        }
    }
}
