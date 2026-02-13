#nullable disable

using System;

namespace SharpCompress.Compressors.PPMd.I1;

/// <summary>
/// A structure containing a single address representing a position in the <see cref="_memory"/> array.  This
/// is intended to mimic the behaviour of a pointer in C/C++.
/// </summary>
/// <remarks>
/// <para>
/// This must be a structure rather than a class because several places in the associated code assume that
/// <see cref="Pointer"/> is a value type (meaning that assignment creates a completely new copy of the
/// instance rather than just copying a reference to the same instance).
/// </para>
/// <para>
/// Note that <see cref="_address"/> is a field rather than a property for performance reasons.
/// </para>
/// </remarks>
internal struct Pointer
{
    public uint _address;
    public byte[] _memory;
    public static readonly Pointer ZERO = new(0, null);
    public const int SIZE = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pointer"/> structure.
    /// </summary>
    public Pointer(uint address, byte[] memory)
    {
        _address = address;
        _memory = memory;
    }

    /// <summary>
    /// Gets or sets the byte at the given <paramref name="offset"/>.
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    public byte this[int offset]
    {
        get { return _memory[_address + offset]; }
        set { _memory[_address + offset] = value; }
    }

    /// <summary>
    /// Allow a <see cref="MemoryNode"/> to be implicitly converted to a <see cref="Pointer"/>.
    /// </summary>
    /// <param name="memoryNode"></param>
    /// <returns></returns>
    public static implicit operator Pointer(MemoryNode memoryNode) =>
        new(memoryNode._address, memoryNode._memory);

    /// <summary>
    /// Allow a <see cref="Model.PpmContext"/> to be implicitly converted to a <see cref="Pointer"/>.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static implicit operator Pointer(Model.PpmContext context) =>
        new(context._address, context._memory);

    /// <summary>
    /// Allow a <see cref="PpmState"/> to be implicitly converted to a <see cref="Pointer"/>.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static implicit operator Pointer(PpmState state) => new(state._address, state._memory);

    /// <summary>
    /// Increase the address of a pointer by the given number of bytes.
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static Pointer operator +(Pointer pointer, int offset)
    {
        pointer._address = (uint)(pointer._address + offset);
        return pointer;
    }

    /// <summary>
    /// Increase the address of a pointer by the given number of bytes.
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static Pointer operator +(Pointer pointer, uint offset)
    {
        pointer._address += offset;
        return pointer;
    }

    /// <summary>
    /// Increment the address of a pointer.
    /// </summary>
    /// <param name="pointer"></param>
    /// <returns></returns>
    public static Pointer operator ++(Pointer pointer)
    {
        pointer._address++;
        return pointer;
    }

    /// <summary>
    /// Decrease the address of a pointer by the given number of bytes.
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static Pointer operator -(Pointer pointer, int offset)
    {
        pointer._address = (uint)(pointer._address - offset);
        return pointer;
    }

    /// <summary>
    /// Decrease the address of a pointer by the given number of bytes.
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static Pointer operator -(Pointer pointer, uint offset)
    {
        pointer._address -= offset;
        return pointer;
    }

    /// <summary>
    /// Decrement the address of a pointer.
    /// </summary>
    /// <param name="pointer"></param>
    /// <returns></returns>
    public static Pointer operator --(Pointer pointer)
    {
        pointer._address--;
        return pointer;
    }

    /// <summary>
    /// Subtract two pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns>The number of bytes between the two pointers.</returns>
    public static uint operator -(Pointer pointer1, Pointer pointer2)
    {
        return pointer1._address - pointer2._address;
    }

    /// <summary>
    /// Compare pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns></returns>
    public static bool operator <(Pointer pointer1, Pointer pointer2)
    {
        return pointer1._address < pointer2._address;
    }

    /// <summary>
    /// Compare two pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns></returns>
    public static bool operator <=(Pointer pointer1, Pointer pointer2)
    {
        return pointer1._address <= pointer2._address;
    }

    /// <summary>
    /// Compare two pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns></returns>
    public static bool operator >(Pointer pointer1, Pointer pointer2)
    {
        return pointer1._address > pointer2._address;
    }

    /// <summary>
    /// Compare two pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns></returns>
    public static bool operator >=(Pointer pointer1, Pointer pointer2)
    {
        return pointer1._address >= pointer2._address;
    }

    /// <summary>
    /// Compare two pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns></returns>
    public static bool operator ==(Pointer pointer1, Pointer pointer2) =>
        pointer1._address == pointer2._address;

    /// <summary>
    /// Compare two pointers.
    /// </summary>
    /// <param name="pointer1"></param>
    /// <param name="pointer2"></param>
    /// <returns></returns>
    public static bool operator !=(Pointer pointer1, Pointer pointer2) =>
        pointer1._address != pointer2._address;

    /// <summary>
    /// Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
    /// <param name="obj">Another object to compare to.</param>
    public override bool Equals(object obj)
    {
        if (obj is Pointer pointer)
        {
            return pointer._address == _address;
        }
        return base.Equals(obj);
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
    public override int GetHashCode() => _address.GetHashCode();
}
