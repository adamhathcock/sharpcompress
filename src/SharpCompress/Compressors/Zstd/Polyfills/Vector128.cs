// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETCOREAPP3_0_OR_GREATER

using System.Runtime.CompilerServices;

namespace System.Runtime.Intrinsics
{
    public static class Vector128
    {
        internal const int Size = 16;

        public static unsafe Vector128<byte> Create(byte value)
        {
            byte* pResult = stackalloc byte[16]
            {
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
            };

            return Unsafe.AsRef<Vector128<byte>>(pResult);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<U> As<T, U>(this Vector128<T> vector)
            where T : struct
            where U : struct =>
            Unsafe.As<Vector128<T>, Vector128<U>>(ref vector);

        public static T GetElement<T>(this Vector128<T> vector, int index)
            where T : struct
        {
            ref T e0 = ref Unsafe.As<Vector128<T>, T>(ref vector);
            return Unsafe.Add(ref e0, index);
        }
    }
}

#endif
