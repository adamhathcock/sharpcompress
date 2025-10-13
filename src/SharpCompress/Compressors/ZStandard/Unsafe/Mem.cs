using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using BclUnsafe = System.Runtime.CompilerServices.Unsafe;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*-**************************************************************
     *  Memory I/O API
     *****************************************************************/
    /*=== Static platform detection ===*/
    private static bool MEM_32bits
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => sizeof(nint) == 4;
    }

    private static bool MEM_64bits
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => sizeof(nint) == 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /* default method, safe and standard.
       can sometimes prove slower */
    private static ushort MEM_read16(void* memPtr) => BclUnsafe.ReadUnaligned<ushort>(memPtr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MEM_read32(void* memPtr) => BclUnsafe.ReadUnaligned<uint>(memPtr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MEM_read64(void* memPtr) => BclUnsafe.ReadUnaligned<ulong>(memPtr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint MEM_readST(void* memPtr) => BclUnsafe.ReadUnaligned<nuint>(memPtr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_write16(void* memPtr, ushort value) =>
        BclUnsafe.WriteUnaligned(memPtr, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_write64(void* memPtr, ulong value) =>
        BclUnsafe.WriteUnaligned(memPtr, value);

    /*=== Little endian r/w ===*/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort MEM_readLE16(void* memPtr)
    {
        var val = BclUnsafe.ReadUnaligned<ushort>(memPtr);
        if (!BitConverter.IsLittleEndian)
        {
            val = BinaryPrimitives.ReverseEndianness(val);
        }
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_writeLE16(void* memPtr, ushort val)
    {
        if (!BitConverter.IsLittleEndian)
        {
            val = BinaryPrimitives.ReverseEndianness(val);
        }
        BclUnsafe.WriteUnaligned(memPtr, val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MEM_readLE24(void* memPtr) =>
        (uint)(MEM_readLE16(memPtr) + (((byte*)memPtr)[2] << 16));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_writeLE24(void* memPtr, uint val)
    {
        MEM_writeLE16(memPtr, (ushort)val);
        ((byte*)memPtr)[2] = (byte)(val >> 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MEM_readLE32(void* memPtr)
    {
        var val = BclUnsafe.ReadUnaligned<uint>(memPtr);
        if (!BitConverter.IsLittleEndian)
        {
            val = BinaryPrimitives.ReverseEndianness(val);
        }
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_writeLE32(void* memPtr, uint val32)
    {
        if (!BitConverter.IsLittleEndian)
        {
            val32 = BinaryPrimitives.ReverseEndianness(val32);
        }
        BclUnsafe.WriteUnaligned(memPtr, val32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MEM_readLE64(void* memPtr)
    {
        var val = BclUnsafe.ReadUnaligned<ulong>(memPtr);
        if (!BitConverter.IsLittleEndian)
        {
            val = BinaryPrimitives.ReverseEndianness(val);
        }
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_writeLE64(void* memPtr, ulong val64)
    {
        if (!BitConverter.IsLittleEndian)
        {
            val64 = BinaryPrimitives.ReverseEndianness(val64);
        }
        BclUnsafe.WriteUnaligned(memPtr, val64);
    }

#if !NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ReverseEndiannessNative(nuint val) =>
        MEM_32bits
            ? BinaryPrimitives.ReverseEndianness((uint)val)
            : (nuint)BinaryPrimitives.ReverseEndianness(val);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint MEM_readLEST(void* memPtr)
    {
        var val = BclUnsafe.ReadUnaligned<nuint>(memPtr);
        if (!BitConverter.IsLittleEndian)
        {
#if NET8_0_OR_GREATER
            val = BinaryPrimitives.ReverseEndianness(val);
#else
            val = ReverseEndiannessNative(val);
#endif
        }
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MEM_writeLEST(void* memPtr, nuint val)
    {
        if (!BitConverter.IsLittleEndian)
        {
#if NET8_0_OR_GREATER
            val = BinaryPrimitives.ReverseEndianness(val);
#else
            val = ReverseEndiannessNative(val);
#endif
        }
        BclUnsafe.WriteUnaligned(memPtr, val);
    }
}
