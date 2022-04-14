using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static readonly rawSeqStore_t kNullRawSeqStore = new rawSeqStore_t
        {
            seq = (rawSeq*)null,
            pos = 0,
            posInSequence = 0,
            size = 0,
            capacity = 0,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_LLcode(uint litLength)
        {

            uint LL_deltaCode = 19;

            return (litLength > 63) ? ZSTD_highbit32(litLength) + LL_deltaCode : LL_Code[litLength];
        }

        /* ZSTD_MLcode() :
         * note : mlBase = matchLength - MINMATCH;
         *        because it's the format it's stored in seqStore->sequences */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_MLcode(uint mlBase)
        {

            uint ML_deltaCode = 36;

            return (mlBase > 127) ? ZSTD_highbit32(mlBase) + ML_deltaCode : ML_Code[mlBase];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static repcodes_s ZSTD_updateRep(uint* rep, uint offset, uint ll0)
        {
            repcodes_s newReps;

            if (offset >= 3)
            {
                newReps.rep[2] = rep[1];
                newReps.rep[1] = rep[0];
                newReps.rep[0] = offset - (uint)((3 - 1));
            }
            else
            {
                uint repCode = offset + ll0;

                if (repCode > 0)
                {
                    uint currentOffset = (repCode == 3) ? (rep[0] - 1) : rep[repCode];

                    newReps.rep[2] = (repCode >= 2) ? rep[1] : rep[2];
                    newReps.rep[1] = rep[0];
                    newReps.rep[0] = currentOffset;
                }
                else
                {
                    memcpy((void*)(&newReps), (void*)(rep), ((nuint)(sizeof(repcodes_s))));
                }
            }

            return newReps;
        }

        /* ZSTD_cParam_withinBounds:
         * @return 1 if value is within cParam bounds,
         * 0 otherwise */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ZSTD_cParam_withinBounds(ZSTD_cParameter cParam, int value)
        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(cParam);

            if ((ERR_isError(bounds.error)) != 0)
            {
                return 0;
            }

            if (value < bounds.lowerBound)
            {
                return 0;
            }

            if (value > bounds.upperBound)
            {
                return 0;
            }

            return 1;
        }

        /* ZSTD_noCompressBlock() :
         * Writes uncompressed block to dst buffer from given src.
         * Returns the size of the block */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_noCompressBlock(void* dst, nuint dstCapacity, void* src, nuint srcSize, uint lastBlock)
        {
            uint cBlockHeader24 = lastBlock + (((uint)(blockType_e.bt_raw)) << 1) + (uint)(srcSize << 3);

            if (srcSize + ZSTD_blockHeaderSize > dstCapacity)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            MEM_writeLE24(dst, cBlockHeader24);
            memcpy((void*)(((byte*)(dst) + ZSTD_blockHeaderSize)), (src), (srcSize));
            return ZSTD_blockHeaderSize + srcSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_rleCompressBlock(void* dst, nuint dstCapacity, byte src, nuint srcSize, uint lastBlock)
        {
            byte* op = (byte*)(dst);
            uint cBlockHeader = lastBlock + (((uint)(blockType_e.bt_rle)) << 1) + (uint)(srcSize << 3);

            if (dstCapacity < 4)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            MEM_writeLE24((void*)op, cBlockHeader);
            op[3] = src;
            return 4;
        }

        /* ZSTD_minGain() :
         * minimum compression required
         * to generate a compress block or a compressed literals section.
         * note : use same formula for both situations */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_minGain(nuint srcSize, ZSTD_strategy strat)
        {
            uint minlog = (strat >= ZSTD_strategy.ZSTD_btultra) ? (uint)(strat) - 1 : 6;

            assert((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, (int)strat)) != 0);
            return (srcSize >> (int)minlog) + 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ZSTD_disableLiteralsCompression(ZSTD_CCtx_params_s* cctxParams)
        {
            switch (cctxParams->literalCompressionMode)
            {
                case ZSTD_literalCompressionMode_e.ZSTD_lcm_huffman:
                {
                    return 0;
                }

                case ZSTD_literalCompressionMode_e.ZSTD_lcm_uncompressed:
                {
                    return 1;
                }

                default:
                {
                    assert(0 != 0);
                }


                goto case ZSTD_literalCompressionMode_e.ZSTD_lcm_auto;
                case ZSTD_literalCompressionMode_e.ZSTD_lcm_auto:
                {
                    return (((cctxParams->cParams.strategy == ZSTD_strategy.ZSTD_fast) && (cctxParams->cParams.targetLength > 0)) ? 1 : 0);
                }
            }
        }

        /*! ZSTD_safecopyLiterals() :
         *  memcpy() function that won't read beyond more than WILDCOPY_OVERLENGTH bytes past ilimit_w.
         *  Only called when the sequence ends past ilimit_w, so it only needs to be optimized for single
         *  large copies.
         */
        private static void ZSTD_safecopyLiterals(byte* op, byte* ip, byte* iend, byte* ilimit_w)
        {
            assert(iend > ilimit_w);
            if (ip <= ilimit_w)
            {
                ZSTD_wildcopy((void*)op, (void*)ip, (nint)(ilimit_w - ip), ZSTD_overlap_e.ZSTD_no_overlap);
                op += ilimit_w - ip;
                ip = ilimit_w;
            }

            while (ip < iend)
            {
                *op++ = *ip++;
            }
        }

        /*! ZSTD_storeSeq() :
         *  Store a sequence (litlen, litPtr, offCode and mlBase) into seqStore_t.
         *  `offCode` : distance to match + ZSTD_REP_MOVE (values <= ZSTD_REP_MOVE are repCodes).
         *  `mlBase` : matchLength - MINMATCH
         *  Allowed to overread literals up to litLimit.
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void ZSTD_storeSeq(seqStore_t* seqStorePtr, nuint litLength, byte* literals, byte* litLimit, uint offCode, nuint mlBase)
        {
            byte* litLimit_w = litLimit - 32;
            byte* litEnd = literals + litLength;

            assert((nuint)(seqStorePtr->sequences - seqStorePtr->sequencesStart) < seqStorePtr->maxNbSeq);
            assert(seqStorePtr->maxNbLit <= (uint)(128 * (1 << 10)));
            assert(seqStorePtr->lit + litLength <= seqStorePtr->litStart + seqStorePtr->maxNbLit);
            assert(literals + litLength <= litLimit);
            if (litEnd <= litLimit_w)
            {
                assert(32 >= 16);
                ZSTD_copy16((void*)seqStorePtr->lit, (void*)literals);
                if (litLength > 16)
                {
                    ZSTD_wildcopy((void*)(seqStorePtr->lit + 16), (void*)(literals + 16), (nint)(litLength) - 16, ZSTD_overlap_e.ZSTD_no_overlap);
                }
            }
            else
            {
                ZSTD_safecopyLiterals(seqStorePtr->lit, literals, litEnd, litLimit_w);
            }

            seqStorePtr->lit += litLength;
            if (litLength > 0xFFFF)
            {
                assert(seqStorePtr->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_none);
                seqStorePtr->longLengthType = ZSTD_longLengthType_e.ZSTD_llt_literalLength;
                seqStorePtr->longLengthPos = (uint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
            }

            seqStorePtr->sequences[0].litLength = (ushort)(litLength);
            seqStorePtr->sequences[0].offset = offCode + 1;
            if (mlBase > 0xFFFF)
            {
                assert(seqStorePtr->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_none);
                seqStorePtr->longLengthType = ZSTD_longLengthType_e.ZSTD_llt_matchLength;
                seqStorePtr->longLengthPos = (uint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
            }

            seqStorePtr->sequences[0].matchLength = (ushort)(mlBase);
            seqStorePtr->sequences++;
        }

        /*-*************************************
        *  Match length counter
        ***************************************/
        [InlineMethod.Inline]
        private static uint ZSTD_NbCommonBytes(nuint val)
        {
            if (val == 0)
            {
                return 0;
            }

            if (BitConverter.IsLittleEndian)
            {
                return (uint)(BitOperations.TrailingZeroCount(val) >> 3);
            }

            return (uint)(BitOperations.Log2(val) >> 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint ZSTD_count(byte* pIn, byte* pMatch, byte* pInLimit)
        {
            byte* pStart = pIn;
            byte* pInLoopLimit = pInLimit - ((nuint)(sizeof(nuint)) - 1);

            if (pIn < pInLoopLimit)
            {

                {
                    nuint diff = MEM_readST((void*)pMatch) ^ MEM_readST((void*)pIn);

                    if (diff != 0)
                    {
                        return ZSTD_NbCommonBytes(diff);
                    }
                }

                pIn += (nuint)(sizeof(nuint));
                pMatch += (nuint)(sizeof(nuint));
                while (pIn < pInLoopLimit)
                {
                    nuint diff = MEM_readST((void*)pMatch) ^ MEM_readST((void*)pIn);

                    if (diff == 0)
                    {
                        pIn += (nuint)(sizeof(nuint));
                        pMatch += (nuint)(sizeof(nuint));
                        continue;
                    }

                    pIn += ZSTD_NbCommonBytes(diff);
                    return (nuint)(pIn - pStart);
                }
            }

            if (MEM_64bits && (pIn < (pInLimit - 3)) && (MEM_read32((void*)pMatch) == MEM_read32((void*)pIn)))
            {
                pIn += 4;
                pMatch += 4;
            }

            if ((pIn < (pInLimit - 1)) && (MEM_read16((void*)pMatch) == MEM_read16((void*)pIn)))
            {
                pIn += 2;
                pMatch += 2;
            }

            if ((pIn < pInLimit) && (*pMatch == *pIn))
            {
                pIn++;
            }

            return (nuint)(pIn - pStart);
        }

        /** ZSTD_count_2segments() :
         *  can count match length with `ip` & `match` in 2 different segments.
         *  convention : on reaching mEnd, match count continue starting from iStart
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_count_2segments(byte* ip, byte* match, byte* iEnd, byte* mEnd, byte* iStart)
        {
            byte* vEnd = ((ip + (mEnd - match)) < (iEnd) ? (ip + (mEnd - match)) : (iEnd));
            nuint matchLength = ZSTD_count(ip, match, vEnd);

            if (match + matchLength != mEnd)
            {
                return matchLength;
            }

            return matchLength + ZSTD_count(ip + matchLength, iStart, iEnd);
        }

        public const uint prime3bytes = 506832829U;

        [InlineMethod.Inline]
        private static uint ZSTD_hash3(uint u, uint h)
        {
            return ((u << (32 - 24)) * prime3bytes) >> (int)(32 - h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint ZSTD_hash3Ptr(void* ptr, uint h)
        {
            return ZSTD_hash3(MEM_readLE32(ptr), h);
        }

        public const uint prime4bytes = 2654435761U;

        [InlineMethod.Inline]
        private static uint ZSTD_hash4(uint u, uint h)
        {
            return (u * prime4bytes) >> (int)(32 - h);
        }

        [InlineMethod.Inline]
        private static nuint ZSTD_hash4Ptr(void* ptr, uint h)
        {
            return ZSTD_hash4(MEM_read32(ptr), h);
        }

        public const ulong prime5bytes = 889523592379UL;

        [InlineMethod.Inline]
        private static nuint ZSTD_hash5(ulong u, uint h)
        {
            return (nuint)(((u << (64 - 40)) * prime5bytes) >> (int)(64 - h));
        }

        [InlineMethod.Inline]
        private static nuint ZSTD_hash5Ptr(void* p, uint h)
        {
            return ZSTD_hash5(MEM_readLE64(p), h);
        }

        public const ulong prime6bytes = 227718039650203UL;

        [InlineMethod.Inline]
        private static nuint ZSTD_hash6(ulong u, uint h)
        {
            return (nuint)(((u << (64 - 48)) * prime6bytes) >> (int)(64 - h));
        }

        [InlineMethod.Inline]
        private static nuint ZSTD_hash6Ptr(void* p, uint h)
        {
            return ZSTD_hash6(MEM_readLE64(p), h);
        }

        public const ulong prime7bytes = 58295818150454627UL;

        [InlineMethod.Inline]
        private static nuint ZSTD_hash7(ulong u, uint h)
        {
            return (nuint)(((u << (64 - 56)) * prime7bytes) >> (int)(64 - h));
        }

        [InlineMethod.Inline]
        private static nuint ZSTD_hash7Ptr(void* p, uint h)
        {
            return ZSTD_hash7(MEM_readLE64(p), h);
        }

        public const ulong prime8bytes = 0xCF1BBCDCB7A56463UL;

        [InlineMethod.Inline]
        private static nuint ZSTD_hash8(ulong u, uint h)
        {
            return (nuint)(((u) * prime8bytes) >> (int)(64 - h));
        }

        [InlineMethod.Inline]
        private static nuint ZSTD_hash8Ptr(void* p, uint h)
        {
            return ZSTD_hash8(MEM_readLE64(p), h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint ZSTD_hashPtr(void* p, uint hBits, uint mls)
        {
            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_hash4Ptr(p, hBits);
                }

                case 5:
                {
                    return ZSTD_hash5Ptr(p, hBits);
                }

                case 6:
                {
                    return ZSTD_hash6Ptr(p, hBits);
                }

                case 7:
                {
                    return ZSTD_hash7Ptr(p, hBits);
                }

                case 8:
                {
                    return ZSTD_hash8Ptr(p, hBits);
                }
            }
        }

        /** ZSTD_ipow() :
         * Return base^exponent.
         */
        private static ulong ZSTD_ipow(ulong @base, ulong exponent)
        {
            ulong power = 1;

            while (exponent != 0)
            {
                if ((exponent & 1) != 0)
                {
                    power *= @base;
                }

                exponent >>= 1;
                @base *= @base;
            }

            return power;
        }

        /** ZSTD_rollingHash_append() :
         * Add the buffer to the hash value.
         */
        private static ulong ZSTD_rollingHash_append(ulong hash, void* buf, nuint size)
        {
            byte* istart = (byte*)(buf);
            nuint pos;

            for (pos = 0; pos < size; ++pos)
            {
                hash *= prime8bytes;
                hash += (ulong)(istart[pos] + 10);
            }

            return hash;
        }

        /** ZSTD_rollingHash_compute() :
         * Compute the rolling hash value of the buffer.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ZSTD_rollingHash_compute(void* buf, nuint size)
        {
            return ZSTD_rollingHash_append(0, buf, size);
        }

        /** ZSTD_rollingHash_primePower() :
         * Compute the primePower to be passed to ZSTD_rollingHash_rotate() for a hash
         * over a window of length bytes.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ZSTD_rollingHash_primePower(uint length)
        {
            return ZSTD_ipow(prime8bytes, length - 1);
        }

        /** ZSTD_rollingHash_rotate() :
         * Rotate the rolling hash by one byte.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ZSTD_rollingHash_rotate(ulong hash, byte toRemove, byte toAdd, ulong primePower)
        {
            hash -= (uint)((toRemove + 10)) * primePower;
            hash *= prime8bytes;
            hash += (ulong)(toAdd + 10);
            return hash;
        }

        /**
         * ZSTD_window_clear():
         * Clears the window containing the history by simply setting it to empty.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_window_clear(ZSTD_window_t* window)
        {
            nuint endT = (nuint)(window->nextSrc - window->@base);
            uint end = (uint)(endT);

            window->lowLimit = end;
            window->dictLimit = end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_window_isEmpty(ZSTD_window_t window)
        {
            return ((window.dictLimit == 1 && window.lowLimit == 1 && (window.nextSrc - window.@base) == 1) ? 1U : 0U);
        }

        /**
         * ZSTD_window_hasExtDict():
         * Returns non-zero if the window has a non-empty extDict.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_window_hasExtDict(ZSTD_window_t window)
        {
            return ((window.lowLimit < window.dictLimit) ? 1U : 0U);
        }

        /**
         * ZSTD_matchState_dictMode():
         * Inspects the provided matchState and figures out what dictMode should be
         * passed to the compressor.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ZSTD_dictMode_e ZSTD_matchState_dictMode(ZSTD_matchState_t* ms)
        {
            return (ZSTD_window_hasExtDict(ms->window)) != 0 ? ZSTD_dictMode_e.ZSTD_extDict : ms->dictMatchState != null ? (ms->dictMatchState->dedicatedDictSearch != 0 ? ZSTD_dictMode_e.ZSTD_dedicatedDictSearch : ZSTD_dictMode_e.ZSTD_dictMatchState) : ZSTD_dictMode_e.ZSTD_noDict;
        }

        /**
         * ZSTD_window_canOverflowCorrect():
         * Returns non-zero if the indices are large enough for overflow correction
         * to work correctly without impacting compression ratio.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_window_canOverflowCorrect(ZSTD_window_t window, uint cycleLog, uint maxDist, uint loadedDictEnd, void* src)
        {
            uint cycleSize = 1U << (int)cycleLog;
            uint curr = (uint)((byte*)(src) - window.@base);
            uint minIndexToOverflowCorrect = cycleSize + ((maxDist) > (cycleSize) ? (maxDist) : (cycleSize));
            uint adjustment = window.nbOverflowCorrections + 1;
            uint adjustedIndex = ((minIndexToOverflowCorrect * adjustment) > (minIndexToOverflowCorrect) ? (minIndexToOverflowCorrect * adjustment) : (minIndexToOverflowCorrect));
            uint indexLargeEnough = ((curr > adjustedIndex) ? 1U : 0U);
            uint dictionaryInvalidated = ((curr > maxDist + loadedDictEnd) ? 1U : 0U);

            return ((indexLargeEnough != 0 && dictionaryInvalidated != 0) ? 1U : 0U);
        }

        /**
         * ZSTD_window_needOverflowCorrection():
         * Returns non-zero if the indices are getting too large and need overflow
         * protection.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_window_needOverflowCorrection(ZSTD_window_t window, uint cycleLog, uint maxDist, uint loadedDictEnd, void* src, void* srcEnd)
        {
            uint curr = (uint)((byte*)(srcEnd) - window.@base);

            ;
            return ((curr > ((3U << 29) + (1U << ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))))) ? 1U : 0U);
        }

        /**
         * ZSTD_window_correctOverflow():
         * Reduces the indices to protect from index overflow.
         * Returns the correction made to the indices, which must be applied to every
         * stored index.
         *
         * The least significant cycleLog bits of the indices must remain the same,
         * which may be 0. Every index up to maxDist in the past must be valid.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_window_correctOverflow(ZSTD_window_t* window, uint cycleLog, uint maxDist, void* src)
        {
            uint cycleSize = 1U << (int)cycleLog;
            uint cycleMask = cycleSize - 1;
            uint curr = (uint)((byte*)(src) - window->@base);
            uint currentCycle0 = curr & cycleMask;
            uint currentCycle1 = currentCycle0 == 0 ? cycleSize : currentCycle0;
            uint newCurrent = currentCycle1 + ((maxDist) > (cycleSize) ? (maxDist) : (cycleSize));
            uint correction = curr - newCurrent;

            assert((maxDist & (maxDist - 1)) == 0);
            assert((curr & cycleMask) == (newCurrent & cycleMask));
            assert(curr > newCurrent);
            if (0 == 0)
            {
                assert(correction > (uint)(1 << 28));
            }

            window->@base += correction;
            window->dictBase += correction;
            if (window->lowLimit <= correction)
            {
                window->lowLimit = 1;
            }
            else
            {
                window->lowLimit -= correction;
            }

            if (window->dictLimit <= correction)
            {
                window->dictLimit = 1;
            }
            else
            {
                window->dictLimit -= correction;
            }

            assert(newCurrent >= maxDist);
            assert(newCurrent - maxDist >= 1);
            assert(window->lowLimit <= newCurrent);
            assert(window->dictLimit <= newCurrent);
            ++window->nbOverflowCorrections;
            return correction;
        }

        /**
         * ZSTD_window_enforceMaxDist():
         * Updates lowLimit so that:
         *    (srcEnd - base) - lowLimit == maxDist + loadedDictEnd
         *
         * It ensures index is valid as long as index >= lowLimit.
         * This must be called before a block compression call.
         *
         * loadedDictEnd is only defined if a dictionary is in use for current compression.
         * As the name implies, loadedDictEnd represents the index at end of dictionary.
         * The value lies within context's referential, it can be directly compared to blockEndIdx.
         *
         * If loadedDictEndPtr is NULL, no dictionary is in use, and we use loadedDictEnd == 0.
         * If loadedDictEndPtr is not NULL, we set it to zero after updating lowLimit.
         * This is because dictionaries are allowed to be referenced fully
         * as long as the last byte of the dictionary is in the window.
         * Once input has progressed beyond window size, dictionary cannot be referenced anymore.
         *
         * In normal dict mode, the dictionary lies between lowLimit and dictLimit.
         * In dictMatchState mode, lowLimit and dictLimit are the same,
         * and the dictionary is below them.
         * forceWindow and dictMatchState are therefore incompatible.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_window_enforceMaxDist(ZSTD_window_t* window, void* blockEnd, uint maxDist, uint* loadedDictEndPtr, ZSTD_matchState_t** dictMatchStatePtr)
        {
            uint blockEndIdx = (uint)((byte*)(blockEnd) - window->@base);
            uint loadedDictEnd = (loadedDictEndPtr != null) ? *loadedDictEndPtr : 0;

            if (blockEndIdx > maxDist + loadedDictEnd)
            {
                uint newLowLimit = blockEndIdx - maxDist;

                if (window->lowLimit < newLowLimit)
                {
                    window->lowLimit = newLowLimit;
                }

                if (window->dictLimit < window->lowLimit)
                {
                    window->dictLimit = window->lowLimit;
                }

                if (loadedDictEndPtr != null)
                {
                    *loadedDictEndPtr = 0;
                }

                if (dictMatchStatePtr != null)
                {
                    *dictMatchStatePtr = null;
                }
            }
        }

        /* Similar to ZSTD_window_enforceMaxDist(),
         * but only invalidates dictionary
         * when input progresses beyond window size.
         * assumption : loadedDictEndPtr and dictMatchStatePtr are valid (non NULL)
         *              loadedDictEnd uses same referential as window->base
         *              maxDist is the window size */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_checkDictValidity(ZSTD_window_t* window, void* blockEnd, uint maxDist, uint* loadedDictEndPtr, ZSTD_matchState_t** dictMatchStatePtr)
        {
            assert(loadedDictEndPtr != null);
            assert(dictMatchStatePtr != null);

            {
                uint blockEndIdx = (uint)((byte*)(blockEnd) - window->@base);
                uint loadedDictEnd = *loadedDictEndPtr;

                assert(blockEndIdx >= loadedDictEnd);
                if (blockEndIdx > loadedDictEnd + maxDist)
                {
                    *loadedDictEndPtr = 0;
                    *dictMatchStatePtr = null;
                }
                else
                {
                    if (*loadedDictEndPtr != 0)
                    {
        ;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_window_init(ZSTD_window_t* window)
        {
            memset((void*)(window), (0), ((nuint)(sizeof(ZSTD_window_t))));
            window->@base = emptyString;
            window->dictBase = emptyString;
            window->dictLimit = 1;
            window->lowLimit = 1;
            window->nextSrc = window->@base + 1;
            window->nbOverflowCorrections = 0;
        }

        /**
         * ZSTD_window_update():
         * Updates the window by appending [src, src + srcSize) to the window.
         * If it is not contiguous, the current prefix becomes the extDict, and we
         * forget about the extDict. Handles overlap of the prefix and extDict.
         * Returns non-zero if the segment is contiguous.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_window_update(ZSTD_window_t* window, void* src, nuint srcSize, int forceNonContiguous)
        {
            byte* ip = (byte*)(src);
            uint contiguous = 1;

            if (srcSize == 0)
            {
                return contiguous;
            }

            assert(window->@base != null);
            assert(window->dictBase != null);
            if (src != window->nextSrc || forceNonContiguous != 0)
            {
                nuint distanceFromBase = (nuint)(window->nextSrc - window->@base);

                window->lowLimit = window->dictLimit;
                assert(distanceFromBase == (nuint)((uint)(distanceFromBase)));
                window->dictLimit = (uint)(distanceFromBase);
                window->dictBase = window->@base;
                window->@base = ip - distanceFromBase;
                if (window->dictLimit - window->lowLimit < 8)
                {
                    window->lowLimit = window->dictLimit;
                }

                contiguous = 0;
            }

            window->nextSrc = ip + srcSize;
            if (((ip + srcSize > window->dictBase + window->lowLimit) && (ip < window->dictBase + window->dictLimit)))
            {
                nint highInputIdx = (nint)((ip + srcSize) - window->dictBase);
                uint lowLimitMax = (highInputIdx > (nint)(window->dictLimit)) ? window->dictLimit : (uint)(highInputIdx);

                window->lowLimit = lowLimitMax;
            }

            return contiguous;
        }

        /**
         * Returns the lowest allowed match index. It may either be in the ext-dict or the prefix.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint ZSTD_getLowestMatchIndex(ZSTD_matchState_t* ms, uint curr, uint windowLog)
        {
            uint maxDistance = 1U << (int)windowLog;
            uint lowestValid = ms->window.lowLimit;
            uint withinWindow = (curr - lowestValid > maxDistance) ? curr - maxDistance : lowestValid;
            uint isDictionary = (((ms->loadedDictEnd != 0)) ? 1U : 0U);
            uint matchLowest = isDictionary != 0 ? lowestValid : withinWindow;

            return matchLowest;
        }

        /**
         * Returns the lowest allowed match index in the prefix.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_getLowestPrefixIndex(ZSTD_matchState_t* ms, uint curr, uint windowLog)
        {
            uint maxDistance = 1U << (int)windowLog;
            uint lowestValid = ms->window.dictLimit;
            uint withinWindow = (curr - lowestValid > maxDistance) ? curr - maxDistance : lowestValid;
            uint isDictionary = (((ms->loadedDictEnd != 0)) ? 1U : 0U);
            uint matchLowest = isDictionary != 0 ? lowestValid : withinWindow;

            return matchLowest;
        }
    }
}
