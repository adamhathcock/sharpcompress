using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using InlineIL;
using static InlineIL.IL.Emit;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /*-**************************************************************
        *  Memory I/O API
        *****************************************************************/
        /*=== Static platform detection ===*/
        public static bool MEM_32bits => sizeof(nint) == 4;

        public static bool MEM_64bits => sizeof(nint) == 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /* default method, safe and standard.
           can sometimes prove slower */
        [InlineMethod.Inline]
        private static ushort MEM_read16(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_U2();
            return IL.Return<ushort>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint MEM_read32(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_U4();
            return IL.Return<uint>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static ulong MEM_read64(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_I8();
            return IL.Return<ulong>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint MEM_readST(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_I();
            return IL.Return<nuint>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void MEM_write64(void* memPtr, ulong value)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(value));
            Unaligned(1);
            Stind_I8();
        }

        /*=== Little endian r/w ===*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static ushort MEM_readLE16(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_U2();

            if (!BitConverter.IsLittleEndian)
            {
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness), typeof(ushort)));
            }

            return IL.Return<ushort>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void MEM_writeLE16(void* memPtr, ushort val)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val));

            if (!BitConverter.IsLittleEndian)
            {
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness),
                    typeof(ushort)));
            }

            Unaligned(1);
            Stind_I2();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint MEM_readLE24(void* memPtr) =>
            (uint) (MEM_readLE16(memPtr) + (((byte*) memPtr)[2] << 16));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void MEM_writeLE24(void* memPtr, uint val)
        {
            MEM_writeLE16(memPtr, (ushort) val);
            ((byte*) memPtr)[2] = (byte) (val >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint MEM_readLE32(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_U4();

            if (!BitConverter.IsLittleEndian)
            {
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness), typeof(uint)));
            }

            return IL.Return<uint>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void MEM_writeLE32(void* memPtr, uint val32)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val32));

            if (!BitConverter.IsLittleEndian)
            {
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness),
                    typeof(uint)));
            }

            Unaligned(1);
            Stind_I4();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static ulong MEM_readLE64(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_I8();

            if (!BitConverter.IsLittleEndian)
            {
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness), typeof(ulong)));
            }

            return IL.Return<ulong>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void MEM_writeLE64(void* memPtr, ulong val64)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val64));

            if (!BitConverter.IsLittleEndian)
            {
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness),
                    typeof(ulong)));
            }

            Unaligned(1);
            Stind_I8();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint MEM_readLEST(void* memPtr)
        {
            Ldarg(nameof(memPtr));
            Unaligned(1);
            Ldind_I();

            if (!BitConverter.IsLittleEndian)
            {
                Conv_U8();
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness),
                    typeof(ulong)));
                Conv_U();
            }

            return IL.Return<nuint>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void MEM_writeLEST(void* memPtr, nuint val)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val));

            if (!BitConverter.IsLittleEndian)
            {
                Conv_U8();
                Call(new MethodRef(typeof(BinaryPrimitives), nameof(BinaryPrimitives.ReverseEndianness),
                    typeof(ulong)));
                Conv_U();
            }

            Unaligned(1);
            Stind_I();
        }
    }
}
