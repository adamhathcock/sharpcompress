using System;
using System.Buffers.Binary;
#if NET6_0_OR_GREATER
using System.Numerics;
#endif

namespace SharpCompress.Compressors.Deflate;

internal sealed partial class DeflateManager
{
    internal static int CountMatchBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var length = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index <= length - sizeof(ulong))
        {
            var difference =
                BinaryPrimitives.ReadUInt64LittleEndian(left.Slice(index))
                ^ BinaryPrimitives.ReadUInt64LittleEndian(right.Slice(index));
            if (difference != 0)
            {
#if NET6_0_OR_GREATER
                // Little-endian words put the first mismatching byte in the low bits.
                return index + (BitOperations.TrailingZeroCount(difference) >> 3);
#else
                break;
#endif
            }
            index += sizeof(ulong);
        }
        while (index < length && left[index] == right[index])
        {
            index++;
        }
        return index;
    }
}
