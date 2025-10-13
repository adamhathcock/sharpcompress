using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
#endif

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_repStartValue => new uint[3] { 1, 4, 8 };
    private static uint* repStartValue =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_repStartValue)
            );
#else

    private static readonly uint* repStartValue = GetArrayPointer(new uint[3] { 1, 4, 8 });
#endif
    private static readonly nuint* ZSTD_fcs_fieldSize = GetArrayPointer(
        new nuint[4] { 0, 2, 4, 8 }
    );
    private static readonly nuint* ZSTD_did_fieldSize = GetArrayPointer(
        new nuint[4] { 0, 1, 2, 4 }
    );
    private const uint ZSTD_blockHeaderSize = 3;
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<byte> Span_LL_bits =>
        new byte[36]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            3,
            3,
            4,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
        };
    private static byte* LL_bits =>
        (byte*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_LL_bits)
            );
#else

    private static readonly byte* LL_bits = GetArrayPointer(
        new byte[36]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            3,
            3,
            4,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
        }
    );
#endif
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<short> Span_LL_defaultNorm =>
        new short[36]
        {
            4,
            3,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            3,
            2,
            1,
            1,
            1,
            1,
            1,
            -1,
            -1,
            -1,
            -1,
        };
    private static short* LL_defaultNorm =>
        (short*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_LL_defaultNorm)
            );
#else

    private static readonly short* LL_defaultNorm = GetArrayPointer(
        new short[36]
        {
            4,
            3,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            3,
            2,
            1,
            1,
            1,
            1,
            1,
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
        }
    );
#endif
    private const uint LL_defaultNormLog = 6;
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<byte> Span_ML_bits =>
        new byte[53]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            3,
            3,
            4,
            4,
            5,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
        };
    private static byte* ML_bits =>
        (byte*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_ML_bits)
            );
#else

    private static readonly byte* ML_bits = GetArrayPointer(
        new byte[53]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            3,
            3,
            4,
            4,
            5,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
        }
    );
#endif
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<short> Span_ML_defaultNorm =>
        new short[53]
        {
            1,
            4,
            3,
            2,
            2,
            2,
            2,
            2,
            2,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
        };
    private static short* ML_defaultNorm =>
        (short*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_ML_defaultNorm)
            );
#else

    private static readonly short* ML_defaultNorm = GetArrayPointer(
        new short[53]
        {
            1,
            4,
            3,
            2,
            2,
            2,
            2,
            2,
            2,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
        }
    );
#endif
    private const uint ML_defaultNormLog = 6;
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<short> Span_OF_defaultNorm =>
        new short[29]
        {
            1,
            1,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            -1,
            -1,
            -1,
            -1,
            -1,
        };
    private static short* OF_defaultNorm =>
        (short*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_OF_defaultNorm)
            );
#else

    private static readonly short* OF_defaultNorm = GetArrayPointer(
        new short[29]
        {
            1,
            1,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
            (short)(-1),
        }
    );
#endif
    private const uint OF_defaultNormLog = 5;

    /*-*******************************************
     *  Shared functions to include for inlining
     *********************************************/
    private static void ZSTD_copy8(void* dst, void* src)
    {
        memcpy(dst, src, 8);
    }

    /* Need to use memmove here since the literal buffer can now be located within
    the dst buffer. In circumstances where the op "catches up" to where the
    literal buffer is, there can be partial overlaps in this call on the final
    copy if the literal is being shifted by less than 16 bytes. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_copy16(void* dst, void* src)
    {
#if NET5_0_OR_GREATER
        if (AdvSimd.IsSupported)
        {
            AdvSimd.Store((byte*)dst, AdvSimd.LoadVector128((byte*)src));
        }
        else
#endif
#if NETCOREAPP3_0_OR_GREATER
        if (Sse2.IsSupported)
        {
            Sse2.Store((byte*)dst, Sse2.LoadVector128((byte*)src));
        }
        else
#endif
        {
            var v1 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<ulong>((ulong*)src);
            var v2 = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<ulong>((ulong*)src + 1);
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned((ulong*)dst, v1);
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned((ulong*)dst + 1, v2);
        }
    }

    /*! ZSTD_wildcopy() :
     *  Custom version of ZSTD_memcpy(), can over read/write up to WILDCOPY_OVERLENGTH bytes (if length==0)
     *  @param ovtype controls the overlap detection
     *         - ZSTD_no_overlap: The source and destination are guaranteed to be at least WILDCOPY_VECLEN bytes apart.
     *         - ZSTD_overlap_src_before_dst: The src and dst may overlap, but they MUST be at least 8 bytes apart.
     *           The src buffer must be before the dst buffer.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_wildcopy(void* dst, void* src, nint length, ZSTD_overlap_e ovtype)
    {
        nint diff = (nint)((byte*)dst - (byte*)src);
        byte* ip = (byte*)src;
        byte* op = (byte*)dst;
        byte* oend = op + length;
        if (ovtype == ZSTD_overlap_e.ZSTD_overlap_src_before_dst && diff < 16)
        {
            do
            {
                ZSTD_copy8(op, ip);
                op += 8;
                ip += 8;
            } while (op < oend);
        }
        else
        {
            assert(diff >= 16 || diff <= -16);
            ZSTD_copy16(op, ip);
            if (16 >= length)
                return;
            op += 16;
            ip += 16;
            do
            {
                {
                    ZSTD_copy16(op, ip);
                    op += 16;
                    ip += 16;
                }

                {
                    ZSTD_copy16(op, ip);
                    op += 16;
                    ip += 16;
                }
            } while (op < oend);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_limitCopy(void* dst, nuint dstCapacity, void* src, nuint srcSize)
    {
        nuint length = dstCapacity < srcSize ? dstCapacity : srcSize;
        if (length > 0)
        {
            memcpy(dst, src, (uint)length);
        }

        return length;
    }
}
