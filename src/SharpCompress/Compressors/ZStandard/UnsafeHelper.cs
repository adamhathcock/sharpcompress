using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpCompress.Compressors.ZStandard;

public static unsafe class UnsafeHelper
{
    public static void* PoisonMemory(void* destination, ulong size)
    {
        memset(destination, 0xCC, (uint)size);
        return destination;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* malloc(ulong size)
    {
#if NET6_0_OR_GREATER
        var ptr = NativeMemory.Alloc((nuint)size);
#else
        var ptr = (void*)Marshal.AllocHGlobal((nint)size);
#endif
#if DEBUG
        return PoisonMemory(ptr, size);
#else
        return ptr;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* calloc(ulong num, ulong size)
    {
#if NET6_0_OR_GREATER
        return NativeMemory.AllocZeroed((nuint)num, (nuint)size);
#else
        var total = num * size;
        assert(total <= uint.MaxValue);
        var destination = (void*)Marshal.AllocHGlobal((nint)total);
        memset(destination, 0, (uint)total);
        return destination;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void memcpy(void* destination, void* source, uint size) =>
        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(destination, source, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void memset(void* memPtr, byte val, uint size) =>
        System.Runtime.CompilerServices.Unsafe.InitBlockUnaligned(memPtr, val, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void free(void* ptr)
    {
#if NET6_0_OR_GREATER
        NativeMemory.Free(ptr);
#else
        Marshal.FreeHGlobal((IntPtr)ptr);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* GetArrayPointer<T>(T[] array)
        where T : unmanaged
    {
        var size = (uint)(sizeof(T) * array.Length);
#if NET9_0_OR_GREATER
        // This function is used to allocate memory for static data blocks.
        // We have to use AllocateTypeAssociatedMemory and link the memory's
        // lifetime to this assembly, in order to prevent memory leaks when
        // loading the assembly in an unloadable AssemblyLoadContext.
        // While introduced in .NET 5, we call this only in .NET 9+, because
        // it's not implemented in the Mono runtime until then.
        var destination = (T*)
            RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(UnsafeHelper), (int)size);
#else
        var destination = (T*)malloc(size);
#endif
        fixed (void* source = &array[0])
            System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(destination, source, size);

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
    public static void memmove(void* destination, void* source, ulong size) =>
        Buffer.MemoryCopy(source, destination, size, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int memcmp(void* buf1, void* buf2, ulong size)
    {
        assert(size <= int.MaxValue);
        var intSize = (int)size;
        return new ReadOnlySpan<byte>(buf1, intSize).SequenceCompareTo(
            new ReadOnlySpan<byte>(buf2, intSize)
        );
    }
}
