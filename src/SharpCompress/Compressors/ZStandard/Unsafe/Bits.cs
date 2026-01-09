using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_countTrailingZeros32(uint val)
    {
        assert(val != 0);
        return (uint)BitOperations.TrailingZeroCount(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_countLeadingZeros32(uint val)
    {
        assert(val != 0);
        return (uint)BitOperations.LeadingZeroCount(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_countTrailingZeros64(ulong val)
    {
        assert(val != 0);
        return (uint)BitOperations.TrailingZeroCount(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_countLeadingZeros64(ulong val)
    {
        assert(val != 0);
        return (uint)BitOperations.LeadingZeroCount(val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_NbCommonBytes(nuint val)
    {
        assert(val != 0);
        if (BitConverter.IsLittleEndian)
        {
            return MEM_64bits
                ? (uint)BitOperations.TrailingZeroCount(val) >> 3
                : (uint)BitOperations.TrailingZeroCount((uint)val) >> 3;
        }

        return MEM_64bits
            ? (uint)BitOperations.LeadingZeroCount(val) >> 3
            : (uint)BitOperations.LeadingZeroCount((uint)val) >> 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_highbit32(uint val)
    {
        assert(val != 0);
        return (uint)BitOperations.Log2(val);
    }
}
