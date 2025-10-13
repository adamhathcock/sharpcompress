using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_BIT_mask =>
        new uint[32]
        {
            0,
            1,
            3,
            7,
            0xF,
            0x1F,
            0x3F,
            0x7F,
            0xFF,
            0x1FF,
            0x3FF,
            0x7FF,
            0xFFF,
            0x1FFF,
            0x3FFF,
            0x7FFF,
            0xFFFF,
            0x1FFFF,
            0x3FFFF,
            0x7FFFF,
            0xFFFFF,
            0x1FFFFF,
            0x3FFFFF,
            0x7FFFFF,
            0xFFFFFF,
            0x1FFFFFF,
            0x3FFFFFF,
            0x7FFFFFF,
            0xFFFFFFF,
            0x1FFFFFFF,
            0x3FFFFFFF,
            0x7FFFFFFF,
        };
    private static uint* BIT_mask =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_BIT_mask)
            );
#else

    private static readonly uint* BIT_mask = GetArrayPointer(
        new uint[32]
        {
            0,
            1,
            3,
            7,
            0xF,
            0x1F,
            0x3F,
            0x7F,
            0xFF,
            0x1FF,
            0x3FF,
            0x7FF,
            0xFFF,
            0x1FFF,
            0x3FFF,
            0x7FFF,
            0xFFFF,
            0x1FFFF,
            0x3FFFF,
            0x7FFFF,
            0xFFFFF,
            0x1FFFFF,
            0x3FFFFF,
            0x7FFFFF,
            0xFFFFFF,
            0x1FFFFFF,
            0x3FFFFFF,
            0x7FFFFFF,
            0xFFFFFFF,
            0x1FFFFFFF,
            0x3FFFFFFF,
            0x7FFFFFFF,
        }
    );
#endif
    /*-**************************************************************
     *  bitStream encoding
     ****************************************************************/
    /*! BIT_initCStream() :
     *  `dstCapacity` must be > sizeof(size_t)
     *  @return : 0 if success,
     *            otherwise an error code (can be tested using ERR_isError()) */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_initCStream(ref BIT_CStream_t bitC, void* startPtr, nuint dstCapacity)
    {
        bitC.bitContainer = 0;
        bitC.bitPos = 0;
        bitC.startPtr = (sbyte*)startPtr;
        bitC.ptr = bitC.startPtr;
        bitC.endPtr = bitC.startPtr + dstCapacity - sizeof(nuint);
        if (dstCapacity <= (nuint)sizeof(nuint))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_getLowerBits(nuint bitContainer, uint nbBits)
    {
        assert(nbBits < sizeof(uint) * 32 / sizeof(uint));
#if NETCOREAPP3_1_OR_GREATER
        if (Bmi2.X64.IsSupported)
        {
            return (nuint)Bmi2.X64.ZeroHighBits(bitContainer, nbBits);
        }

        if (Bmi2.IsSupported)
        {
            return Bmi2.ZeroHighBits((uint)bitContainer, nbBits);
        }
#endif

        return bitContainer & BIT_mask[nbBits];
    }

    /*! BIT_addBits() :
     *  can add up to 31 bits into `bitC`.
     *  Note : does not check for register overflow ! */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BIT_addBits(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        nuint value,
        uint nbBits
    )
    {
        assert(nbBits < sizeof(uint) * 32 / sizeof(uint));
        assert(nbBits + bitC_bitPos < (uint)(sizeof(nuint) * 8));
        bitC_bitContainer |= BIT_getLowerBits(value, nbBits) << (int)bitC_bitPos;
        bitC_bitPos += nbBits;
    }

    /*! BIT_addBitsFast() :
     *  works only if `value` is _clean_,
     *  meaning all high bits above nbBits are 0 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BIT_addBitsFast(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        nuint value,
        uint nbBits
    )
    {
        assert(value >> (int)nbBits == 0);
        assert(nbBits + bitC_bitPos < (uint)(sizeof(nuint) * 8));
        bitC_bitContainer |= value << (int)bitC_bitPos;
        bitC_bitPos += nbBits;
    }

    /*! BIT_flushBitsFast() :
     *  assumption : bitContainer has not overflowed
     *  unsafe version; does not check buffer overflow */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BIT_flushBitsFast(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        ref sbyte* bitC_ptr,
        sbyte* bitC_endPtr
    )
    {
        nuint nbBytes = bitC_bitPos >> 3;
        assert(bitC_bitPos < (uint)(sizeof(nuint) * 8));
        assert(bitC_ptr <= bitC_endPtr);
        MEM_writeLEST(bitC_ptr, bitC_bitContainer);
        bitC_ptr += nbBytes;
        bitC_bitPos &= 7;
        bitC_bitContainer >>= (int)(nbBytes * 8);
    }

    /*! BIT_flushBits() :
     *  assumption : bitContainer has not overflowed
     *  safe version; check for buffer overflow, and prevents it.
     *  note : does not signal buffer overflow.
     *  overflow will be revealed later on using BIT_closeCStream() */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BIT_flushBits(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        ref sbyte* bitC_ptr,
        sbyte* bitC_endPtr
    )
    {
        nuint nbBytes = bitC_bitPos >> 3;
        assert(bitC_bitPos < (uint)(sizeof(nuint) * 8));
        assert(bitC_ptr <= bitC_endPtr);
        MEM_writeLEST(bitC_ptr, bitC_bitContainer);
        bitC_ptr += nbBytes;
        if (bitC_ptr > bitC_endPtr)
            bitC_ptr = bitC_endPtr;
        bitC_bitPos &= 7;
        bitC_bitContainer >>= (int)(nbBytes * 8);
    }

    /*! BIT_closeCStream() :
     *  @return : size of CStream, in bytes,
     *            or 0 if it could not fit into dstBuffer */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_closeCStream(
        ref nuint bitC_bitContainer,
        ref uint bitC_bitPos,
        sbyte* bitC_ptr,
        sbyte* bitC_endPtr,
        sbyte* bitC_startPtr
    )
    {
        BIT_addBitsFast(ref bitC_bitContainer, ref bitC_bitPos, 1, 1);
        BIT_flushBits(ref bitC_bitContainer, ref bitC_bitPos, ref bitC_ptr, bitC_endPtr);
        if (bitC_ptr >= bitC_endPtr)
            return 0;
        return (nuint)(bitC_ptr - bitC_startPtr) + (nuint)(bitC_bitPos > 0 ? 1 : 0);
    }

    /*-********************************************************
     *  bitStream decoding
     **********************************************************/
    /*! BIT_initDStream() :
     *  Initialize a BIT_DStream_t.
     * `bitD` : a pointer to an already allocated BIT_DStream_t structure.
     * `srcSize` must be the *exact* size of the bitStream, in bytes.
     * @return : size of stream (== srcSize), or an errorCode if a problem is detected
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_initDStream(BIT_DStream_t* bitD, void* srcBuffer, nuint srcSize)
    {
        if (srcSize < 1)
        {
            *bitD = new BIT_DStream_t();
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        bitD->start = (sbyte*)srcBuffer;
        bitD->limitPtr = bitD->start + sizeof(nuint);
        if (srcSize >= (nuint)sizeof(nuint))
        {
            bitD->ptr = (sbyte*)srcBuffer + srcSize - sizeof(nuint);
            bitD->bitContainer = MEM_readLEST(bitD->ptr);
            {
                byte lastByte = ((byte*)srcBuffer)[srcSize - 1];
                bitD->bitsConsumed = lastByte != 0 ? 8 - ZSTD_highbit32(lastByte) : 0;
                if (lastByte == 0)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
            }
        }
        else
        {
            bitD->ptr = bitD->start;
            bitD->bitContainer = *(byte*)bitD->start;
            switch (srcSize)
            {
                case 7:
                    bitD->bitContainer += (nuint)((byte*)srcBuffer)[6] << sizeof(nuint) * 8 - 16;
                    goto case 6;
                case 6:
                    bitD->bitContainer += (nuint)((byte*)srcBuffer)[5] << sizeof(nuint) * 8 - 24;
                    goto case 5;
                case 5:
                    bitD->bitContainer += (nuint)((byte*)srcBuffer)[4] << sizeof(nuint) * 8 - 32;
                    goto case 4;
                case 4:
                    bitD->bitContainer += (nuint)((byte*)srcBuffer)[3] << 24;
                    goto case 3;
                case 3:
                    bitD->bitContainer += (nuint)((byte*)srcBuffer)[2] << 16;
                    goto case 2;
                case 2:
                    bitD->bitContainer += (nuint)((byte*)srcBuffer)[1] << 8;
                    goto default;
                default:
                    break;
            }

            {
                byte lastByte = ((byte*)srcBuffer)[srcSize - 1];
                bitD->bitsConsumed = lastByte != 0 ? 8 - ZSTD_highbit32(lastByte) : 0;
                if (lastByte == 0)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            bitD->bitsConsumed += (uint)((nuint)sizeof(nuint) - srcSize) * 8;
        }

        return srcSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_getUpperBits(nuint bitContainer, uint start)
    {
        return bitContainer >> (int)start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_getMiddleBits(nuint bitContainer, uint start, uint nbBits)
    {
        uint regMask = (uint)(sizeof(nuint) * 8 - 1);
        assert(nbBits < sizeof(uint) * 32 / sizeof(uint));
#if NETCOREAPP3_1_OR_GREATER
        if (Bmi2.X64.IsSupported)
        {
            return (nuint)Bmi2.X64.ZeroHighBits(bitContainer >> (int)(start & regMask), nbBits);
        }

        if (Bmi2.IsSupported)
        {
            return Bmi2.ZeroHighBits((uint)(bitContainer >> (int)(start & regMask)), nbBits);
        }
#endif

        return (nuint)(bitContainer >> (int)(start & regMask) & ((ulong)1 << (int)nbBits) - 1);
    }

    /*! BIT_lookBits() :
     *  Provides next n bits from local register.
     *  local register is not modified.
     *  On 32-bits, maxNbBits==24.
     *  On 64-bits, maxNbBits==56.
     * @return : value extracted */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_lookBits(BIT_DStream_t* bitD, uint nbBits)
    {
        return BIT_getMiddleBits(
            bitD->bitContainer,
            (uint)(sizeof(nuint) * 8) - bitD->bitsConsumed - nbBits,
            nbBits
        );
    }

    /*! BIT_lookBitsFast() :
     *  unsafe version; only works if nbBits >= 1 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_lookBitsFast(BIT_DStream_t* bitD, uint nbBits)
    {
        uint regMask = (uint)(sizeof(nuint) * 8 - 1);
        assert(nbBits >= 1);
        return bitD->bitContainer
            << (int)(bitD->bitsConsumed & regMask)
            >> (int)(regMask + 1 - nbBits & regMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BIT_skipBits(BIT_DStream_t* bitD, uint nbBits)
    {
        bitD->bitsConsumed += nbBits;
    }

    /*! BIT_readBits() :
     *  Read (consume) next n bits from local register and update.
     *  Pay attention to not read more than nbBits contained into local register.
     * @return : extracted value. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_readBits(BIT_DStream_t* bitD, uint nbBits)
    {
        nuint value = BIT_lookBits(bitD, nbBits);
        BIT_skipBits(bitD, nbBits);
        return value;
    }

    /*! BIT_readBitsFast() :
     *  unsafe version; only works if nbBits >= 1 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_readBitsFast(BIT_DStream_t* bitD, uint nbBits)
    {
        nuint value = BIT_lookBitsFast(bitD, nbBits);
        assert(nbBits >= 1);
        BIT_skipBits(bitD, nbBits);
        return value;
    }

    /*! BIT_reloadDStream_internal() :
     *  Simple variant of BIT_reloadDStream(), with two conditions:
     *  1. bitstream is valid : bitsConsumed <= sizeof(bitD->bitContainer)*8
     *  2. look window is valid after shifted down : bitD->ptr >= bitD->start
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BIT_DStream_status BIT_reloadDStream_internal(BIT_DStream_t* bitD)
    {
        assert(bitD->bitsConsumed <= (uint)(sizeof(nuint) * 8));
        bitD->ptr -= bitD->bitsConsumed >> 3;
        assert(bitD->ptr >= bitD->start);
        bitD->bitsConsumed &= 7;
        bitD->bitContainer = MEM_readLEST(bitD->ptr);
        return BIT_DStream_status.BIT_DStream_unfinished;
    }

    /*! BIT_reloadDStreamFast() :
     *  Similar to BIT_reloadDStream(), but with two differences:
     *  1. bitsConsumed <= sizeof(bitD->bitContainer)*8 must hold!
     *  2. Returns BIT_DStream_overflow when bitD->ptr < bitD->limitPtr, at this
     *     point you must use BIT_reloadDStream() to reload.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BIT_DStream_status BIT_reloadDStreamFast(BIT_DStream_t* bitD)
    {
        if (bitD->ptr < bitD->limitPtr)
            return BIT_DStream_status.BIT_DStream_overflow;
        return BIT_reloadDStream_internal(bitD);
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<byte> Span_static_zeroFilled =>
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
    private static nuint* static_zeroFilled =>
        (nuint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_static_zeroFilled)
            );
#else

    private static readonly nuint* static_zeroFilled = (nuint*)GetArrayPointer(
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }
    );
#endif
    /*! BIT_reloadDStream() :
     *  Refill `bitD` from buffer previously set in BIT_initDStream() .
     *  This function is safe, it guarantees it will not never beyond src buffer.
     * @return : status of `BIT_DStream_t` internal register.
     *           when status == BIT_DStream_unfinished, internal register is filled with at least 25 or 57 bits */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BIT_DStream_status BIT_reloadDStream(BIT_DStream_t* bitD)
    {
        if (bitD->bitsConsumed > (uint)(sizeof(nuint) * 8))
        {
            bitD->ptr = (sbyte*)&static_zeroFilled[0];
            return BIT_DStream_status.BIT_DStream_overflow;
        }

        assert(bitD->ptr >= bitD->start);
        if (bitD->ptr >= bitD->limitPtr)
        {
            return BIT_reloadDStream_internal(bitD);
        }

        if (bitD->ptr == bitD->start)
        {
            if (bitD->bitsConsumed < (uint)(sizeof(nuint) * 8))
                return BIT_DStream_status.BIT_DStream_endOfBuffer;
            return BIT_DStream_status.BIT_DStream_completed;
        }

        {
            uint nbBytes = bitD->bitsConsumed >> 3;
            BIT_DStream_status result = BIT_DStream_status.BIT_DStream_unfinished;
            if (bitD->ptr - nbBytes < bitD->start)
            {
                nbBytes = (uint)(bitD->ptr - bitD->start);
                result = BIT_DStream_status.BIT_DStream_endOfBuffer;
            }

            bitD->ptr -= nbBytes;
            bitD->bitsConsumed -= nbBytes * 8;
            bitD->bitContainer = MEM_readLEST(bitD->ptr);
            return result;
        }
    }

    /*! BIT_endOfDStream() :
     * @return : 1 if DStream has _exactly_ reached its end (all bits consumed).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BIT_endOfDStream(BIT_DStream_t* DStream)
    {
        return DStream->ptr == DStream->start && DStream->bitsConsumed == (uint)(sizeof(nuint) * 8)
            ? 1U
            : 0U;
    }

    /*-********************************************************
     *  bitStream decoding
     **********************************************************/
    /*! BIT_initDStream() :
     *  Initialize a BIT_DStream_t.
     * `bitD` : a pointer to an already allocated BIT_DStream_t structure.
     * `srcSize` must be the *exact* size of the bitStream, in bytes.
     * @return : size of stream (== srcSize), or an errorCode if a problem is detected
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_initDStream(ref BIT_DStream_t bitD, void* srcBuffer, nuint srcSize)
    {
        if (srcSize < 1)
        {
            bitD = new BIT_DStream_t();
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        bitD.start = (sbyte*)srcBuffer;
        bitD.limitPtr = bitD.start + sizeof(nuint);
        if (srcSize >= (nuint)sizeof(nuint))
        {
            bitD.ptr = (sbyte*)srcBuffer + srcSize - sizeof(nuint);
            bitD.bitContainer = MEM_readLEST(bitD.ptr);
            {
                byte lastByte = ((byte*)srcBuffer)[srcSize - 1];
                bitD.bitsConsumed = lastByte != 0 ? 8 - ZSTD_highbit32(lastByte) : 0;
                if (lastByte == 0)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
            }
        }
        else
        {
            bitD.ptr = bitD.start;
            bitD.bitContainer = *(byte*)bitD.start;
            switch (srcSize)
            {
                case 7:
                    bitD.bitContainer += (nuint)((byte*)srcBuffer)[6] << sizeof(nuint) * 8 - 16;
                    goto case 6;
                case 6:
                    bitD.bitContainer += (nuint)((byte*)srcBuffer)[5] << sizeof(nuint) * 8 - 24;
                    goto case 5;
                case 5:
                    bitD.bitContainer += (nuint)((byte*)srcBuffer)[4] << sizeof(nuint) * 8 - 32;
                    goto case 4;
                case 4:
                    bitD.bitContainer += (nuint)((byte*)srcBuffer)[3] << 24;
                    goto case 3;
                case 3:
                    bitD.bitContainer += (nuint)((byte*)srcBuffer)[2] << 16;
                    goto case 2;
                case 2:
                    bitD.bitContainer += (nuint)((byte*)srcBuffer)[1] << 8;
                    goto default;
                default:
                    break;
            }

            {
                byte lastByte = ((byte*)srcBuffer)[srcSize - 1];
                bitD.bitsConsumed = lastByte != 0 ? 8 - ZSTD_highbit32(lastByte) : 0;
                if (lastByte == 0)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            bitD.bitsConsumed += (uint)((nuint)sizeof(nuint) - srcSize) * 8;
        }

        return srcSize;
    }

    /*! BIT_lookBits() :
     *  Provides next n bits from local register.
     *  local register is not modified.
     *  On 32-bits, maxNbBits==24.
     *  On 64-bits, maxNbBits==56.
     * @return : value extracted */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_lookBits(nuint bitD_bitContainer, uint bitD_bitsConsumed, uint nbBits)
    {
        return BIT_getMiddleBits(
            bitD_bitContainer,
            (uint)(sizeof(nuint) * 8) - bitD_bitsConsumed - nbBits,
            nbBits
        );
    }

    /*! BIT_lookBitsFast() :
     *  unsafe version; only works if nbBits >= 1 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_lookBitsFast(
        nuint bitD_bitContainer,
        uint bitD_bitsConsumed,
        uint nbBits
    )
    {
        uint regMask = (uint)(sizeof(nuint) * 8 - 1);
        assert(nbBits >= 1);
        return bitD_bitContainer
            << (int)(bitD_bitsConsumed & regMask)
            >> (int)(regMask + 1 - nbBits & regMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BIT_skipBits(ref uint bitD_bitsConsumed, uint nbBits)
    {
        bitD_bitsConsumed += nbBits;
    }

    /*! BIT_readBits() :
     *  Read (consume) next n bits from local register and update.
     *  Pay attention to not read more than nbBits contained into local register.
     * @return : extracted value. */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_readBits(
        nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed,
        uint nbBits
    )
    {
        nuint value = BIT_lookBits(bitD_bitContainer, bitD_bitsConsumed, nbBits);
        BIT_skipBits(ref bitD_bitsConsumed, nbBits);
        return value;
    }

    /*! BIT_readBitsFast() :
     *  unsafe version; only works if nbBits >= 1 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint BIT_readBitsFast(
        nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed,
        uint nbBits
    )
    {
        nuint value = BIT_lookBitsFast(bitD_bitContainer, bitD_bitsConsumed, nbBits);
        assert(nbBits >= 1);
        BIT_skipBits(ref bitD_bitsConsumed, nbBits);
        return value;
    }

    /*! BIT_reloadDStreamFast() :
     *  Similar to BIT_reloadDStream(), but with two differences:
     *  1. bitsConsumed <= sizeof(bitD->bitContainer)*8 must hold!
     *  2. Returns BIT_DStream_overflow when bitD->ptr < bitD->limitPtr, at this
     *     point you must use BIT_reloadDStream() to reload.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BIT_DStream_status BIT_reloadDStreamFast(
        ref nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed,
        ref sbyte* bitD_ptr,
        sbyte* bitD_start,
        sbyte* bitD_limitPtr
    )
    {
        if (bitD_ptr < bitD_limitPtr)
            return BIT_DStream_status.BIT_DStream_overflow;
        return BIT_reloadDStream_internal(
            ref bitD_bitContainer,
            ref bitD_bitsConsumed,
            ref bitD_ptr,
            bitD_start
        );
    }

    /*! BIT_reloadDStream() :
     *  Refill `bitD` from buffer previously set in BIT_initDStream() .
     *  This function is safe, it guarantees it will not never beyond src buffer.
     * @return : status of `BIT_DStream_t` internal register.
     *           when status == BIT_DStream_unfinished, internal register is filled with at least 25 or 57 bits */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BIT_DStream_status BIT_reloadDStream(
        ref nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed,
        ref sbyte* bitD_ptr,
        sbyte* bitD_start,
        sbyte* bitD_limitPtr
    )
    {
        if (bitD_bitsConsumed > (uint)(sizeof(nuint) * 8))
        {
            bitD_ptr = (sbyte*)&static_zeroFilled[0];
            return BIT_DStream_status.BIT_DStream_overflow;
        }

        assert(bitD_ptr >= bitD_start);
        if (bitD_ptr >= bitD_limitPtr)
        {
            return BIT_reloadDStream_internal(
                ref bitD_bitContainer,
                ref bitD_bitsConsumed,
                ref bitD_ptr,
                bitD_start
            );
        }

        if (bitD_ptr == bitD_start)
        {
            if (bitD_bitsConsumed < (uint)(sizeof(nuint) * 8))
                return BIT_DStream_status.BIT_DStream_endOfBuffer;
            return BIT_DStream_status.BIT_DStream_completed;
        }

        {
            uint nbBytes = bitD_bitsConsumed >> 3;
            BIT_DStream_status result = BIT_DStream_status.BIT_DStream_unfinished;
            if (bitD_ptr - nbBytes < bitD_start)
            {
                nbBytes = (uint)(bitD_ptr - bitD_start);
                result = BIT_DStream_status.BIT_DStream_endOfBuffer;
            }

            bitD_ptr -= nbBytes;
            bitD_bitsConsumed -= nbBytes * 8;
            bitD_bitContainer = MEM_readLEST(bitD_ptr);
            return result;
        }
    }

    /*! BIT_reloadDStream_internal() :
     *  Simple variant of BIT_reloadDStream(), with two conditions:
     *  1. bitstream is valid : bitsConsumed <= sizeof(bitD->bitContainer)*8
     *  2. look window is valid after shifted down : bitD->ptr >= bitD->start
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BIT_DStream_status BIT_reloadDStream_internal(
        ref nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed,
        ref sbyte* bitD_ptr,
        sbyte* bitD_start
    )
    {
        assert(bitD_bitsConsumed <= (uint)(sizeof(nuint) * 8));
        bitD_ptr -= bitD_bitsConsumed >> 3;
        assert(bitD_ptr >= bitD_start);
        bitD_bitsConsumed &= 7;
        bitD_bitContainer = MEM_readLEST(bitD_ptr);
        return BIT_DStream_status.BIT_DStream_unfinished;
    }

    /*! BIT_endOfDStream() :
     * @return : 1 if DStream has _exactly_ reached its end (all bits consumed).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BIT_endOfDStream(
        uint DStream_bitsConsumed,
        sbyte* DStream_ptr,
        sbyte* DStream_start
    )
    {
        return DStream_ptr == DStream_start && DStream_bitsConsumed == (uint)(sizeof(nuint) * 8)
            ? 1U
            : 0U;
    }
}
