#region Using

using System;

#endregion

namespace SharpCompress.Compressor.PPMd.I1
{
    /// <summary>
    /// PPM state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This must be a structure rather than a class because several places in the associated code assume that
    /// <see cref="PpmState"/> is a value type (meaning that assignment creates a completely new copy of the
    /// instance rather than just copying a reference to the same instance).
    /// </para>
    /// <para>
    /// Note that <see cref="Address"/> is a field rather than a property for performance reasons.
    /// </para>
    /// </remarks>
    internal struct PpmState
    {
        public uint Address;
        public byte[] Memory;
        public static readonly PpmState Zero = new PpmState(0, null);
        public const int Size = 6;

        /// <summary>
        /// Initializes a new instance of the <see cref="PpmState"/> structure.
        /// </summary>
        public PpmState(uint address, byte[] memory)
        {
            Address = address;
            Memory = memory;
        }

        /// <summary>
        /// Gets or sets the symbol.
        /// </summary>
        public byte Symbol
        {
            get { return Memory[Address]; }
            set { Memory[Address] = value; }
        }

        /// <summary>
        /// Gets or sets the frequency.
        /// </summary>
        public byte Frequency
        {
            get { return Memory[Address + 1]; }
            set { Memory[Address + 1] = value; }
        }

        /// <summary>
        /// Gets or sets the successor.
        /// </summary>
        public Model.PpmContext Successor
        {
            get { return new Model.PpmContext(((uint) Memory[Address + 2]) | ((uint) Memory[Address + 3]) << 8 | ((uint) Memory[Address + 4]) << 16 | ((uint) Memory[Address + 5]) << 24, Memory); }
            set
            {
                Memory[Address + 2] = (byte) value.Address;
                Memory[Address + 3] = (byte) (value.Address >> 8);
                Memory[Address + 4] = (byte) (value.Address >> 16);
                Memory[Address + 5] = (byte) (value.Address >> 24);
            }
        }

        /// <summary>
        /// Gets the <see cref="PpmState"/> at the <paramref name="offset"/> relative to this
        /// <see cref="PpmState"/>.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public PpmState this[int offset]
        {
            get { return new PpmState((uint) (Address + offset * Size), Memory); }
        }

        /// <summary>
        /// Allow a pointer to be implicitly converted to a PPM state.
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        public static implicit operator PpmState(Pointer pointer)
        {
            return new PpmState(pointer.Address, pointer.Memory);
        }

        /// <summary>
        /// Allow pointer-like addition on a PPM state.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static PpmState operator +(PpmState state, int offset)
        {
            state.Address = (uint) (state.Address + offset * Size);
            return state;
        }

        /// <summary>
        /// Allow pointer-like incrementing on a PPM state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static PpmState operator ++(PpmState state)
        {
            state.Address += Size;
            return state;
        }

        /// <summary>
        /// Allow pointer-like subtraction on a PPM state.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static PpmState operator -(PpmState state, int offset)
        {
            state.Address = (uint) (state.Address - offset * Size);
            return state;
        }

        /// <summary>
        /// Allow pointer-like decrementing on a PPM state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static PpmState operator --(PpmState state)
        {
            state.Address -= Size;
            return state;
        }

        /// <summary>
        /// Compare two PPM states.
        /// </summary>
        /// <param name="state1"></param>
        /// <param name="state2"></param>
        /// <returns></returns>
        public static bool operator <=(PpmState state1, PpmState state2)
        {
            return state1.Address <= state2.Address;
        }

        /// <summary>
        /// Compare two PPM states.
        /// </summary>
        /// <param name="state1"></param>
        /// <param name="state2"></param>
        /// <returns></returns>
        public static bool operator >=(PpmState state1, PpmState state2)
        {
            return state1.Address >= state2.Address;
        }

        /// <summary>
        /// Compare two PPM states.
        /// </summary>
        /// <param name="state1"></param>
        /// <param name="state2"></param>
        /// <returns></returns>
        public static bool operator ==(PpmState state1, PpmState state2)
        {
            return state1.Address == state2.Address;
        }

        /// <summary>
        /// Compare two PPM states.
        /// </summary>
        /// <param name="state1"></param>
        /// <param name="state2"></param>
        /// <returns></returns>
        public static bool operator !=(PpmState state1, PpmState state2)
        {
            return state1.Address != state2.Address;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        /// <param name="obj">Another object to compare to.</param>
        public override bool Equals(object obj)
        {
            if (obj is PpmState)
            {
                PpmState state = (PpmState) obj;
                return state.Address == Address;
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
