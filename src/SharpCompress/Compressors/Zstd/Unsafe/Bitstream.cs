using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /*-**************************************************************
        *  Internal functions
        ****************************************************************/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint BIT_highbit32(uint val)
        {
            assert(val != 0);

            {
                return (uint) BitOperations.Log2(val);
            }
        }

        public static uint* BIT_mask = GetArrayPointer(new uint[32]
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
        });

        /*-**************************************************************
        *  bitStream encoding
        ****************************************************************/
        /*! BIT_initCStream() :
         *  `dstCapacity` must be > sizeof(size_t)
         *  @return : 0 if success,
         *            otherwise an error code (can be tested using ERR_isError()) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint BIT_initCStream(BIT_CStream_t* bitC, void* startPtr, nuint dstCapacity)
        {
            bitC->bitContainer = 0;
            bitC->bitPos = 0;
            bitC->startPtr = (sbyte*)(startPtr);
            bitC->ptr = bitC->startPtr;
            bitC->endPtr = bitC->startPtr + dstCapacity - (nuint)(sizeof(nuint));
            if (dstCapacity <= (nuint)(sizeof(nuint)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            return 0;
        }

        /*! BIT_addBits() :
         *  can add up to 31 bits into `bitC`.
         *  Note : does not check for register overflow ! */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BIT_addBits(BIT_CStream_t* bitC, nuint value, uint nbBits)
        {
            assert(nbBits < ((nuint)(sizeof(uint) * 32) / (nuint)(sizeof(uint))));
            assert(nbBits + bitC->bitPos < (nuint)(sizeof(nuint)) * 8);
            bitC->bitContainer |= (value & BIT_mask[nbBits]) << (int)bitC->bitPos;
            bitC->bitPos += nbBits;
        }

        /*! BIT_addBitsFast() :
         *  works only if `value` is _clean_,
         *  meaning all high bits above nbBits are 0 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BIT_addBitsFast(BIT_CStream_t* bitC, nuint value, uint nbBits)
        {
            assert((value >> (int)nbBits) == 0);
            assert(nbBits + bitC->bitPos < (nuint)(sizeof(nuint)) * 8);
            bitC->bitContainer |= value << (int)bitC->bitPos;
            bitC->bitPos += nbBits;
        }

        /*! BIT_flushBitsFast() :
         *  assumption : bitContainer has not overflowed
         *  unsafe version; does not check buffer overflow */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BIT_flushBitsFast(BIT_CStream_t* bitC)
        {
            nuint nbBytes = bitC->bitPos >> 3;

            assert(bitC->bitPos < (nuint)(sizeof(nuint)) * 8);
            assert(bitC->ptr <= bitC->endPtr);
            MEM_writeLEST((void*)bitC->ptr, bitC->bitContainer);
            bitC->ptr += nbBytes;
            bitC->bitPos &= 7;
            bitC->bitContainer >>= (int)(nbBytes * 8);
        }

        /*! BIT_flushBits() :
         *  assumption : bitContainer has not overflowed
         *  safe version; check for buffer overflow, and prevents it.
         *  note : does not signal buffer overflow.
         *  overflow will be revealed later on using BIT_closeCStream() */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BIT_flushBits(BIT_CStream_t* bitC)
        {
            nuint nbBytes = bitC->bitPos >> 3;

            assert(bitC->bitPos < (nuint)(sizeof(nuint)) * 8);
            assert(bitC->ptr <= bitC->endPtr);
            MEM_writeLEST((void*)bitC->ptr, bitC->bitContainer);
            bitC->ptr += nbBytes;
            if (bitC->ptr > bitC->endPtr)
            {
                bitC->ptr = bitC->endPtr;
            }

            bitC->bitPos &= 7;
            bitC->bitContainer >>= (int)(nbBytes * 8);
        }

        /*! BIT_closeCStream() :
         *  @return : size of CStream, in bytes,
         *            or 0 if it could not fit into dstBuffer */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint BIT_closeCStream(BIT_CStream_t* bitC)
        {
            BIT_addBitsFast(bitC, 1, 1);
            BIT_flushBits(bitC);
            if (bitC->ptr >= bitC->endPtr)
            {
                return 0;
            }

            return (nuint)((bitC->ptr - bitC->startPtr) + (((bitC->bitPos > 0)) ? 1 : 0));
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
                memset((void*)(bitD), (0), ((nuint)(sizeof(BIT_DStream_t))));
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            bitD->start = (sbyte*)(srcBuffer);
            bitD->limitPtr = bitD->start + (nuint)(sizeof(nuint));
            if (srcSize >= (nuint)(sizeof(nuint)))
            {
                bitD->ptr = (sbyte*)(srcBuffer) + srcSize - (nuint)(sizeof(nuint));
                bitD->bitContainer = MEM_readLEST((void*)bitD->ptr);

                {
                    byte lastByte = ((byte*)(srcBuffer))[srcSize - 1];

                    bitD->bitsConsumed = lastByte != 0 ? 8 - BIT_highbit32(lastByte) : 0;
                    if (lastByte == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                    }
                }
            }
            else
            {
                bitD->ptr = bitD->start;
                bitD->bitContainer = *(byte*)(bitD->start);
                switch (srcSize)
                {
                    case 7:
                    {
                        bitD->bitContainer += (nuint)(((byte*)(srcBuffer))[6]) << (int)((nuint)(sizeof(nuint)) * 8 - 16);
                    }


                    goto case 6;
                    case 6:
                    {
                        bitD->bitContainer += (nuint)(((byte*)(srcBuffer))[5]) << (int)((nuint)(sizeof(nuint)) * 8 - 24);
                    }


                    goto case 5;
                    case 5:
                    {
                        bitD->bitContainer += (nuint)(((byte*)(srcBuffer))[4]) << (int)((nuint)(sizeof(nuint)) * 8 - 32);
                    }


                    goto case 4;
                    case 4:
                    {
                        bitD->bitContainer += (nuint)(((byte*)(srcBuffer))[3]) << 24;
                    }


                    goto case 3;
                    case 3:
                    {
                        bitD->bitContainer += (nuint)(((byte*)(srcBuffer))[2]) << 16;
                    }


                    goto case 2;
                    case 2:
                    {
                        bitD->bitContainer += (nuint)(((byte*)(srcBuffer))[1]) << 8;
                    }


                    goto default;
                    default:
                    {
                        break;
                    }
                }


                {
                    byte lastByte = ((byte*)(srcBuffer))[srcSize - 1];

                    bitD->bitsConsumed = lastByte != 0 ? 8 - BIT_highbit32(lastByte) : 0;
                    if (lastByte == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }
                }

                bitD->bitsConsumed += (uint)((nuint)(sizeof(nuint)) - srcSize) * 8;
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
            uint regMask = (uint)((nuint)(sizeof(nuint)) * 8 - 1);

            assert(nbBits < ((nuint)(sizeof(uint) * 32) / (nuint)(sizeof(uint))));
            return (bitContainer >> (int)(start & regMask)) & BIT_mask[nbBits];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint BIT_getLowerBits(nuint bitContainer, uint nbBits)
        {
            assert(nbBits < ((nuint)(sizeof(uint) * 32) / (nuint)(sizeof(uint))));
            return bitContainer & BIT_mask[nbBits];
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
            return BIT_getMiddleBits(bitD->bitContainer, (uint)(((nuint)(sizeof(nuint)) * 8) - bitD->bitsConsumed - nbBits), nbBits);
        }

        /*! BIT_lookBitsFast() :
         *  unsafe version; only works if nbBits >= 1 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint BIT_lookBitsFast(BIT_DStream_t* bitD, uint nbBits)
        {
            uint regMask = (uint)((nuint)(sizeof(nuint)) * 8 - 1);

            assert(nbBits >= 1);
            return (bitD->bitContainer << (int)(bitD->bitsConsumed & regMask)) >> (int)(((regMask + 1) - nbBits) & regMask);
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
         *  unsafe version; only works only if nbBits >= 1 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint BIT_readBitsFast(BIT_DStream_t* bitD, uint nbBits)
        {
            nuint value = BIT_lookBitsFast(bitD, nbBits);

            assert(nbBits >= 1);
            BIT_skipBits(bitD, nbBits);
            return value;
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
            if ((bitD->ptr < bitD->limitPtr))
            {
                return BIT_DStream_status.BIT_DStream_overflow;
            }

            assert(bitD->bitsConsumed <= (nuint)(sizeof(nuint)) * 8);
            bitD->ptr -= bitD->bitsConsumed >> 3;
            bitD->bitsConsumed &= 7;
            bitD->bitContainer = MEM_readLEST((void*)bitD->ptr);
            return BIT_DStream_status.BIT_DStream_unfinished;
        }

        /*! BIT_reloadDStream() :
         *  Refill `bitD` from buffer previously set in BIT_initDStream() .
         *  This function is safe, it guarantees it will not read beyond src buffer.
         * @return : status of `BIT_DStream_t` internal register.
         *           when status == BIT_DStream_unfinished, internal register is filled with at least 25 or 57 bits */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BIT_DStream_status BIT_reloadDStream(BIT_DStream_t* bitD)
        {
            if (bitD->bitsConsumed > ((nuint)(sizeof(nuint)) * 8))
            {
                return BIT_DStream_status.BIT_DStream_overflow;
            }

            if (bitD->ptr >= bitD->limitPtr)
            {
                return BIT_reloadDStreamFast(bitD);
            }

            if (bitD->ptr == bitD->start)
            {
                if (bitD->bitsConsumed < (nuint)(sizeof(nuint)) * 8)
                {
                    return BIT_DStream_status.BIT_DStream_endOfBuffer;
                }

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
                bitD->bitContainer = MEM_readLEST((void*)bitD->ptr);
                return result;
            }
        }

        /*! BIT_endOfDStream() :
         * @return : 1 if DStream has _exactly_ reached its end (all bits consumed).
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BIT_endOfDStream(BIT_DStream_t* DStream)
        {
            return ((((DStream->ptr == DStream->start) && (DStream->bitsConsumed == (nuint)(sizeof(nuint)) * 8))) ? 1U : 0U);
        }
    }
}
