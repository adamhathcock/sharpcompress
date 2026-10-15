using System;
#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace SharpCompress.Compressors.Deflate;

internal sealed partial class DeflateManager
{
    internal static void SlideHashTable(short[] table, int count, int windowSize)
    {
        var index = 0;
#if NET6_0_OR_GREATER
        // Hash links are unsigned 16-bit positions stored in short arrays.
        // Saturating subtraction maps both expired links and NIL to zero.
        if (AdvSimd.IsSupported || Sse2.IsSupported)
        {
            unsafe
            {
                fixed (short* start = table)
                {
                    var distance = Vector128.Create((ushort)windowSize);
                    if (AdvSimd.IsSupported)
                    {
                        for (
                            ;
                            index <= count - Vector128<ushort>.Count;
                            index += Vector128<ushort>.Count
                        )
                        {
                            var location = (ushort*)(start + index);
                            AdvSimd.Store(
                                location,
                                AdvSimd.SubtractSaturate(AdvSimd.LoadVector128(location), distance)
                            );
                        }
                    }
                    else
                    {
                        for (
                            ;
                            index <= count - Vector128<ushort>.Count;
                            index += Vector128<ushort>.Count
                        )
                        {
                            var location = (ushort*)(start + index);
                            Sse2.Store(
                                location,
                                Sse2.SubtractSaturate(Sse2.LoadVector128(location), distance)
                            );
                        }
                    }
                }
            }
        }
#endif
        // Legacy frameworks, unsupported CPUs, and vector tails use the same mapping.
        for (; index < count; index++)
        {
            var position = table[index] & 0xFFFF;
            table[index] = (short)(position >= windowSize ? position - windowSize : 0);
        }
    }
}
