using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static InlineIL.IL.Emit;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

//[module: SkipLocalsInit]

namespace ZstdSharp
{
    public static unsafe class UnsafeHelper
    {
        public static void* PoisonMemory(void* destination, int size)
        {
            memset(destination, 0xCC, size);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* malloc(uint size)
        {
#if DEBUG
            return PoisonMemory((void*)Marshal.AllocHGlobal((int)size), (int)size);
#else
            return (void*) Marshal.AllocHGlobal((int) size);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* malloc(ulong size)
        {
#if DEBUG
            return PoisonMemory((void*)Marshal.AllocHGlobal((int)size), (int)size);
#else
            return (void*)Marshal.AllocHGlobal((int)size);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* calloc(ulong num, ulong size)
        {
            int total = (int)(num * size);
            var destination = (void*)Marshal.AllocHGlobal(total);
            //Unsafe.InitBlockUnaligned(destination, 0, (uint)total);
            //Ldloc(nameof(destination));
            memset(destination, 0, total);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void free(void* ptr)
        {
            Marshal.FreeHGlobal((IntPtr)ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void* memcpy(void* destination, void* source, ulong size)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(size));
            Unaligned(1);
            Cpblk();
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void* memcpy(void* destination, void* source, uint size)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(size));
            Unaligned(1);
            Cpblk();
            return destination;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void* memcpy(void* destination, void* source, int size)
        {
            Ldarg(nameof(destination));
            Ldarg(nameof(source));
            Ldarg(nameof(size));
            Unaligned(1);
            Cpblk();
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void memset(void* memPtr, int val, uint size)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val));
            Ldarg(nameof(size));
            Unaligned(1);
            Initblk();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void memset(void* memPtr, int val, int size)
        {
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val));
            Ldarg(nameof(size));
            Unaligned(1);
            Initblk();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void memset(void* memPtr, int val, ulong size)
        {
            //Unsafe.InitBlockUnaligned(memPtr, (byte)val, (uint)size);
            Ldarg(nameof(memPtr));
            Ldarg(nameof(val));
            Ldarg(nameof(size));
            Unaligned(1);
            Initblk();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetArrayPointer<T>(T[] array) where T : unmanaged
        {
            var size = (uint)(sizeof(T) * array.Length);
            var destination = (T*)malloc(size);
            fixed (void* source = &array[0])
            {
                memcpy(destination, source, size);
            }

            return destination;
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void assert(bool condition, string? message = null)
        {
            if (!condition)
                throw new ArgumentException(message ?? "assert failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void memmove(void* destination, void* source, ulong size)
        {
            Buffer.MemoryCopy(source, destination, size, size);
        }

#if NET
        public static bool IsBmi2Supported => System.Runtime.Intrinsics.X86.Bmi2.IsSupported;
#else
        public static bool IsBmi2Supported => false;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void Prefetch0(void* p)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
            {
                System.Runtime.Intrinsics.X86.Sse.Prefetch0(p);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void Prefetch1(void* p)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
            {
                System.Runtime.Intrinsics.X86.Sse.Prefetch1(p);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int memcmp(void* buf1, void* buf2, ulong size)
        {
            var p1 = (byte*)buf1;
            var p2 = (byte*)buf2;

            while (size > 0)
            {
                var diff = *p1++ - *p2++;
                if (diff != 0)
                {
                    return diff;
                }
                size--;
            }
            return 0;
        }
    }
}
