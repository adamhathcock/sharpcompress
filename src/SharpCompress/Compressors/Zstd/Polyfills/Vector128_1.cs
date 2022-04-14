// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETCOREAPP3_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.Intrinsics
{
    [StructLayout(LayoutKind.Sequential, Size = Vector128.Size)]
    public readonly struct Vector128<T> : IEquatable<Vector128<T>>
        where T : struct
    {
        private readonly ulong _00;
        private readonly ulong _01;

        public static int Count => Vector128.Size / Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vector128<T> other)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!((IEquatable<T>)(this.GetElement(i))).Equals(other.GetElement(i)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

#endif
