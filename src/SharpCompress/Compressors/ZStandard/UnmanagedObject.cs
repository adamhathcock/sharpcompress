using System;
using System.Runtime.InteropServices;

namespace SharpCompress.Compressors.ZStandard;

/*
 * Wrap object to void* to make it unmanaged
 */
internal static unsafe class UnmanagedObject
{
    public static void* Wrap(object obj) => (void*)GCHandle.ToIntPtr(GCHandle.Alloc(obj));

    private static GCHandle UnwrapGcHandle(void* value) => GCHandle.FromIntPtr((IntPtr)value);

    public static T Unwrap<T>(void* value) => (T)UnwrapGcHandle(value).Target!;

    public static void Free(void* value) => UnwrapGcHandle(value).Free();
}
