using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /* ZSTD_bitWeight() :
     * provide estimated "cost" of a stat in full bits only */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_bitWeight(uint stat)
    {
        return ZSTD_highbit32(stat + 1) * (1 << 8);
    }

    /* ZSTD_fracWeight() :
     * provide fractional-bit "cost" of a stat,
     * using linear interpolation approximation */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_fracWeight(uint rawStat)
    {
        uint stat = rawStat + 1;
        uint hb = ZSTD_highbit32(stat);
        uint BWeight = hb * (1 << 8);
        /* Fweight was meant for "Fractional weight"
         * but it's effectively a value between 1 and 2
         * using fixed point arithmetic */
        uint FWeight = stat << 8 >> (int)hb;
        uint weight = BWeight + FWeight;
        assert(hb + 8 < 31);
        return weight;
    }

    private static int ZSTD_compressedLiterals(optState_t* optPtr)
    {
        return optPtr->literalCompressionMode != ZSTD_paramSwitch_e.ZSTD_ps_disable ? 1 : 0;
    }

    private static void ZSTD_setBasePrices(optState_t* optPtr, int optLevel)
    {
        if (ZSTD_compressedLiterals(optPtr) != 0)
            optPtr->litSumBasePrice =
                optLevel != 0 ? ZSTD_fracWeight(optPtr->litSum) : ZSTD_bitWeight(optPtr->litSum);
        optPtr->litLengthSumBasePrice =
            optLevel != 0
                ? ZSTD_fracWeight(optPtr->litLengthSum)
                : ZSTD_bitWeight(optPtr->litLengthSum);
        optPtr->matchLengthSumBasePrice =
            optLevel != 0
                ? ZSTD_fracWeight(optPtr->matchLengthSum)
                : ZSTD_bitWeight(optPtr->matchLengthSum);
        optPtr->offCodeSumBasePrice =
            optLevel != 0
                ? ZSTD_fracWeight(optPtr->offCodeSum)
                : ZSTD_bitWeight(optPtr->offCodeSum);
    }

    private static uint sum_u32(uint* table, nuint nbElts)
    {
        nuint n;
        uint total = 0;
        for (n = 0; n < nbElts; n++)
        {
            total += table[n];
        }

        return total;
    }

    private static uint ZSTD_downscaleStats(
        uint* table,
        uint lastEltIndex,
        uint shift,
        base_directive_e base1
    )
    {
        uint s,
            sum = 0;
        assert(shift < 30);
        for (s = 0; s < lastEltIndex + 1; s++)
        {
            uint @base = (uint)(
                base1 != default ? 1
                : table[s] > 0 ? 1
                : 0
            );
            uint newStat = @base + (table[s] >> (int)shift);
            sum += newStat;
            table[s] = newStat;
        }

        return sum;
    }

    /* ZSTD_scaleStats() :
     * reduce all elt frequencies in table if sum too large
     * return the resulting sum of elements */
    private static uint ZSTD_scaleStats(uint* table, uint lastEltIndex, uint logTarget)
    {
        uint prevsum = sum_u32(table, lastEltIndex + 1);
        uint factor = prevsum >> (int)logTarget;
        assert(logTarget < 30);
        if (factor <= 1)
            return prevsum;
        return ZSTD_downscaleStats(
            table,
            lastEltIndex,
            ZSTD_highbit32(factor),
            base_directive_e.base_1guaranteed
        );
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_baseLLfreqs =>
        new uint[36]
        {
            4,
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
        };
    private static uint* baseLLfreqs =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_baseLLfreqs)
            );
#else

    private static readonly uint* baseLLfreqs = GetArrayPointer(
        new uint[36]
        {
            4,
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
        }
    );
#endif
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_baseOFCfreqs =>
        new uint[32]
        {
            6,
            2,
            1,
            1,
            2,
            3,
            4,
            4,
            4,
            3,
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
        };
    private static uint* baseOFCfreqs =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_baseOFCfreqs)
            );
#else

    private static readonly uint* baseOFCfreqs = GetArrayPointer(
        new uint[32]
        {
            6,
            2,
            1,
            1,
            2,
            3,
            4,
            4,
            4,
            3,
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
        }
    );
#endif
    /* ZSTD_rescaleFreqs() :
     * if first block (detected by optPtr->litLengthSum == 0) : init statistics
     *    take hints from dictionary if there is one
     *    and init from zero if there is none,
     *    using src for literals stats, and baseline stats for sequence symbols
     * otherwise downscale existing stats, to be used as seed for next block.
     */
    private static void ZSTD_rescaleFreqs(
        optState_t* optPtr,
        byte* src,
        nuint srcSize,
        int optLevel
    )
    {
        int compressedLiterals = ZSTD_compressedLiterals(optPtr);
        optPtr->priceType = ZSTD_OptPrice_e.zop_dynamic;
        if (optPtr->litLengthSum == 0)
        {
            if (srcSize <= 8)
            {
                optPtr->priceType = ZSTD_OptPrice_e.zop_predef;
            }

            assert(optPtr->symbolCosts != null);
            if (optPtr->symbolCosts->huf.repeatMode == HUF_repeat.HUF_repeat_valid)
            {
                optPtr->priceType = ZSTD_OptPrice_e.zop_dynamic;
                if (compressedLiterals != 0)
                {
                    /* generate literals statistics from huffman table */
                    uint lit;
                    assert(optPtr->litFreq != null);
                    optPtr->litSum = 0;
                    for (lit = 0; lit <= (1 << 8) - 1; lit++)
                    {
                        /* scale to 2K */
                        const uint scaleLog = 11;
                        uint bitCost = HUF_getNbBitsFromCTable(
                            &optPtr->symbolCosts->huf.CTable.e0,
                            lit
                        );
                        assert(bitCost <= scaleLog);
                        optPtr->litFreq[lit] = (uint)(
                            bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1
                        );
                        optPtr->litSum += optPtr->litFreq[lit];
                    }
                }

                {
                    uint ll;
                    FSE_CState_t llstate;
                    FSE_initCState(&llstate, optPtr->symbolCosts->fse.litlengthCTable);
                    optPtr->litLengthSum = 0;
                    for (ll = 0; ll <= 35; ll++)
                    {
                        /* scale to 1K */
                        const uint scaleLog = 10;
                        uint bitCost = FSE_getMaxNbBits(llstate.symbolTT, ll);
                        assert(bitCost < scaleLog);
                        optPtr->litLengthFreq[ll] = (uint)(
                            bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1
                        );
                        optPtr->litLengthSum += optPtr->litLengthFreq[ll];
                    }
                }

                {
                    uint ml;
                    FSE_CState_t mlstate;
                    FSE_initCState(&mlstate, optPtr->symbolCosts->fse.matchlengthCTable);
                    optPtr->matchLengthSum = 0;
                    for (ml = 0; ml <= 52; ml++)
                    {
                        const uint scaleLog = 10;
                        uint bitCost = FSE_getMaxNbBits(mlstate.symbolTT, ml);
                        assert(bitCost < scaleLog);
                        optPtr->matchLengthFreq[ml] = (uint)(
                            bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1
                        );
                        optPtr->matchLengthSum += optPtr->matchLengthFreq[ml];
                    }
                }

                {
                    uint of;
                    FSE_CState_t ofstate;
                    FSE_initCState(&ofstate, optPtr->symbolCosts->fse.offcodeCTable);
                    optPtr->offCodeSum = 0;
                    for (of = 0; of <= 31; of++)
                    {
                        const uint scaleLog = 10;
                        uint bitCost = FSE_getMaxNbBits(ofstate.symbolTT, of);
                        assert(bitCost < scaleLog);
                        optPtr->offCodeFreq[of] = (uint)(
                            bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1
                        );
                        optPtr->offCodeSum += optPtr->offCodeFreq[of];
                    }
                }
            }
            else
            {
                assert(optPtr->litFreq != null);
                if (compressedLiterals != 0)
                {
                    /* base initial cost of literals on direct frequency within src */
                    uint lit = (1 << 8) - 1;
                    HIST_count_simple(optPtr->litFreq, &lit, src, srcSize);
                    optPtr->litSum = ZSTD_downscaleStats(
                        optPtr->litFreq,
                        (1 << 8) - 1,
                        8,
                        base_directive_e.base_0possible
                    );
                }

                {
                    memcpy(optPtr->litLengthFreq, baseLLfreqs, sizeof(uint) * 36);
                    optPtr->litLengthSum = sum_u32(baseLLfreqs, 35 + 1);
                }

                {
                    uint ml;
                    for (ml = 0; ml <= 52; ml++)
                        optPtr->matchLengthFreq[ml] = 1;
                }

                optPtr->matchLengthSum = 52 + 1;
                {
                    memcpy(optPtr->offCodeFreq, baseOFCfreqs, sizeof(uint) * 32);
                    optPtr->offCodeSum = sum_u32(baseOFCfreqs, 31 + 1);
                }
            }
        }
        else
        {
            if (compressedLiterals != 0)
                optPtr->litSum = ZSTD_scaleStats(optPtr->litFreq, (1 << 8) - 1, 12);
            optPtr->litLengthSum = ZSTD_scaleStats(optPtr->litLengthFreq, 35, 11);
            optPtr->matchLengthSum = ZSTD_scaleStats(optPtr->matchLengthFreq, 52, 11);
            optPtr->offCodeSum = ZSTD_scaleStats(optPtr->offCodeFreq, 31, 11);
        }

        ZSTD_setBasePrices(optPtr, optLevel);
    }

    /* ZSTD_rawLiteralsCost() :
     * price of literals (only) in specified segment (which length can be 0).
     * does not include price of literalLength symbol */
    private static uint ZSTD_rawLiteralsCost(
        byte* literals,
        uint litLength,
        optState_t* optPtr,
        int optLevel
    )
    {
        if (litLength == 0)
            return 0;
        if (ZSTD_compressedLiterals(optPtr) == 0)
            return (litLength << 3) * (1 << 8);
        if (optPtr->priceType == ZSTD_OptPrice_e.zop_predef)
            return litLength * 6 * (1 << 8);
        {
            uint price = optPtr->litSumBasePrice * litLength;
            uint litPriceMax = optPtr->litSumBasePrice - (1 << 8);
            uint u;
            assert(optPtr->litSumBasePrice >= 1 << 8);
            for (u = 0; u < litLength; u++)
            {
                uint litPrice =
                    optLevel != 0
                        ? ZSTD_fracWeight(optPtr->litFreq[literals[u]])
                        : ZSTD_bitWeight(optPtr->litFreq[literals[u]]);
                if (litPrice > litPriceMax)
                    litPrice = litPriceMax;
                price -= litPrice;
            }

            return price;
        }
    }

    /* ZSTD_litLengthPrice() :
     * cost of literalLength symbol */
    private static uint ZSTD_litLengthPrice(uint litLength, optState_t* optPtr, int optLevel)
    {
        assert(litLength <= 1 << 17);
        if (optPtr->priceType == ZSTD_OptPrice_e.zop_predef)
            return optLevel != 0 ? ZSTD_fracWeight(litLength) : ZSTD_bitWeight(litLength);
        if (litLength == 1 << 17)
            return (1 << 8) + ZSTD_litLengthPrice((1 << 17) - 1, optPtr, optLevel);
        {
            uint llCode = ZSTD_LLcode(litLength);
            return (uint)(LL_bits[llCode] * (1 << 8))
                + optPtr->litLengthSumBasePrice
                - (
                    optLevel != 0
                        ? ZSTD_fracWeight(optPtr->litLengthFreq[llCode])
                        : ZSTD_bitWeight(optPtr->litLengthFreq[llCode])
                );
        }
    }

    /* ZSTD_getMatchPrice() :
     * Provides the cost of the match part (offset + matchLength) of a sequence.
     * Must be combined with ZSTD_fullLiteralsCost() to get the full cost of a sequence.
     * @offBase : sumtype, representing an offset or a repcode, and using numeric representation of ZSTD_storeSeq()
     * @optLevel: when <2, favors small offset for decompression speed (improved cache efficiency)
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_getMatchPrice(
        uint offBase,
        uint matchLength,
        optState_t* optPtr,
        int optLevel
    )
    {
        uint price;
        uint offCode = ZSTD_highbit32(offBase);
        uint mlBase = matchLength - 3;
        assert(matchLength >= 3);
        if (optPtr->priceType == ZSTD_OptPrice_e.zop_predef)
            return (optLevel != 0 ? ZSTD_fracWeight(mlBase) : ZSTD_bitWeight(mlBase))
                + (16 + offCode) * (1 << 8);
        price =
            offCode * (1 << 8)
            + (
                optPtr->offCodeSumBasePrice
                - (
                    optLevel != 0
                        ? ZSTD_fracWeight(optPtr->offCodeFreq[offCode])
                        : ZSTD_bitWeight(optPtr->offCodeFreq[offCode])
                )
            );
        if (optLevel < 2 && offCode >= 20)
            price += (offCode - 19) * 2 * (1 << 8);
        {
            uint mlCode = ZSTD_MLcode(mlBase);
            price +=
                (uint)(ML_bits[mlCode] * (1 << 8))
                + (
                    optPtr->matchLengthSumBasePrice
                    - (
                        optLevel != 0
                            ? ZSTD_fracWeight(optPtr->matchLengthFreq[mlCode])
                            : ZSTD_bitWeight(optPtr->matchLengthFreq[mlCode])
                    )
                );
        }

        price += (1 << 8) / 5;
        return price;
    }

    /* ZSTD_updateStats() :
     * assumption : literals + litLength <= iend */
    private static void ZSTD_updateStats(
        optState_t* optPtr,
        uint litLength,
        byte* literals,
        uint offBase,
        uint matchLength
    )
    {
        if (ZSTD_compressedLiterals(optPtr) != 0)
        {
            uint u;
            for (u = 0; u < litLength; u++)
                optPtr->litFreq[literals[u]] += 2;
            optPtr->litSum += litLength * 2;
        }

        {
            uint llCode = ZSTD_LLcode(litLength);
            optPtr->litLengthFreq[llCode]++;
            optPtr->litLengthSum++;
        }

        {
            uint offCode = ZSTD_highbit32(offBase);
            assert(offCode <= 31);
            optPtr->offCodeFreq[offCode]++;
            optPtr->offCodeSum++;
        }

        {
            uint mlBase = matchLength - 3;
            uint mlCode = ZSTD_MLcode(mlBase);
            optPtr->matchLengthFreq[mlCode]++;
            optPtr->matchLengthSum++;
        }
    }

    /* ZSTD_readMINMATCH() :
     * function safe only for comparisons
     * assumption : memPtr must be at least 4 bytes before end of buffer */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_readMINMATCH(void* memPtr, uint length)
    {
        switch (length)
        {
            default:
            case 4:
                return MEM_read32(memPtr);
            case 3:
                if (BitConverter.IsLittleEndian)
                    return MEM_read32(memPtr) << 8;
                else
                    return MEM_read32(memPtr) >> 8;
        }
    }

    /* Update hashTable3 up to ip (excluded)
    Assumption : always within prefix (i.e. not within extDict) */
    private static uint ZSTD_insertAndFindFirstIndexHash3(
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip
    )
    {
        uint* hashTable3 = ms->hashTable3;
        uint hashLog3 = ms->hashLog3;
        byte* @base = ms->window.@base;
        uint idx = *nextToUpdate3;
        uint target = (uint)(ip - @base);
        nuint hash3 = ZSTD_hash3Ptr(ip, hashLog3);
        assert(hashLog3 > 0);
        while (idx < target)
        {
            hashTable3[ZSTD_hash3Ptr(@base + idx, hashLog3)] = idx;
            idx++;
        }

        *nextToUpdate3 = target;
        return hashTable3[hash3];
    }

    /*-*************************************
     *  Binary Tree search
     ***************************************/
    /** ZSTD_insertBt1() : add one or multiple positions to tree.
     * @param ip assumed <= iend-8 .
     * @param target The target of ZSTD_updateTree_internal() - we are filling to this position
     * @return : nb of positions added */
    private static uint ZSTD_insertBt1(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iend,
        uint target,
        uint mls,
        int extDict
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hashLog = cParams->hashLog;
        nuint h = ZSTD_hashPtr(ip, hashLog, mls);
        uint* bt = ms->chainTable;
        uint btLog = cParams->chainLog - 1;
        uint btMask = (uint)((1 << (int)btLog) - 1);
        uint matchIndex = hashTable[h];
        nuint commonLengthSmaller = 0,
            commonLengthLarger = 0;
        byte* @base = ms->window.@base;
        byte* dictBase = ms->window.dictBase;
        uint dictLimit = ms->window.dictLimit;
        byte* dictEnd = dictBase + dictLimit;
        byte* prefixStart = @base + dictLimit;
        byte* match;
        uint curr = (uint)(ip - @base);
        uint btLow = btMask >= curr ? 0 : curr - btMask;
        uint* smallerPtr = bt + 2 * (curr & btMask);
        uint* largerPtr = smallerPtr + 1;
        /* to be nullified at the end */
        uint dummy32;
        /* windowLow is based on target because
         * we only need positions that will be in the window at the end of the tree update.
         */
        uint windowLow = ZSTD_getLowestMatchIndex(ms, target, cParams->windowLog);
        uint matchEndIdx = curr + 8 + 1;
        nuint bestLength = 8;
        uint nbCompares = 1U << (int)cParams->searchLog;
        assert(curr <= target);
        assert(ip <= iend - 8);
        hashTable[h] = curr;
        assert(windowLow > 0);
        for (; nbCompares != 0 && matchIndex >= windowLow; --nbCompares)
        {
            uint* nextPtr = bt + 2 * (matchIndex & btMask);
            /* guaranteed minimum nb of common bytes */
            nuint matchLength =
                commonLengthSmaller < commonLengthLarger ? commonLengthSmaller : commonLengthLarger;
            assert(matchIndex < curr);
            if (extDict == 0 || matchIndex + matchLength >= dictLimit)
            {
                assert(matchIndex + matchLength >= dictLimit);
                match = @base + matchIndex;
                matchLength += ZSTD_count(ip + matchLength, match + matchLength, iend);
            }
            else
            {
                match = dictBase + matchIndex;
                matchLength += ZSTD_count_2segments(
                    ip + matchLength,
                    match + matchLength,
                    iend,
                    dictEnd,
                    prefixStart
                );
                if (matchIndex + matchLength >= dictLimit)
                    match = @base + matchIndex;
            }

            if (matchLength > bestLength)
            {
                bestLength = matchLength;
                if (matchLength > matchEndIdx - matchIndex)
                    matchEndIdx = matchIndex + (uint)matchLength;
            }

            if (ip + matchLength == iend)
            {
                break;
            }

            if (match[matchLength] < ip[matchLength])
            {
                *smallerPtr = matchIndex;
                commonLengthSmaller = matchLength;
                if (matchIndex <= btLow)
                {
                    smallerPtr = &dummy32;
                    break;
                }

                smallerPtr = nextPtr + 1;
                matchIndex = nextPtr[1];
            }
            else
            {
                *largerPtr = matchIndex;
                commonLengthLarger = matchLength;
                if (matchIndex <= btLow)
                {
                    largerPtr = &dummy32;
                    break;
                }

                largerPtr = nextPtr;
                matchIndex = nextPtr[0];
            }
        }

        *smallerPtr = *largerPtr = 0;
        {
            uint positions = 0;
            if (bestLength > 384)
                positions = 192 < (uint)(bestLength - 384) ? 192 : (uint)(bestLength - 384);
            assert(matchEndIdx > curr + 8);
            return positions > matchEndIdx - (curr + 8) ? positions : matchEndIdx - (curr + 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_updateTree_internal(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iend,
        uint mls,
        ZSTD_dictMode_e dictMode
    )
    {
        byte* @base = ms->window.@base;
        uint target = (uint)(ip - @base);
        uint idx = ms->nextToUpdate;
        while (idx < target)
        {
            uint forward = ZSTD_insertBt1(
                ms,
                @base + idx,
                iend,
                target,
                mls,
                dictMode == ZSTD_dictMode_e.ZSTD_extDict ? 1 : 0
            );
            assert(idx < idx + forward);
            idx += forward;
        }

        assert((nuint)(ip - @base) <= unchecked((uint)-1));
        assert((nuint)(iend - @base) <= unchecked((uint)-1));
        ms->nextToUpdate = target;
    }

    /* used in ZSTD_loadDictionaryContent() */
    private static void ZSTD_updateTree(ZSTD_MatchState_t* ms, byte* ip, byte* iend)
    {
        ZSTD_updateTree_internal(ms, ip, iend, ms->cParams.minMatch, ZSTD_dictMode_e.ZSTD_noDict);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_insertBtAndGetAllMatches(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iLimit,
        ZSTD_dictMode_e dictMode,
        uint* rep,
        uint ll0,
        uint lengthToBeat,
        uint mls
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint sufficient_len =
            cParams->targetLength < (1 << 12) - 1 ? cParams->targetLength : (1 << 12) - 1;
        byte* @base = ms->window.@base;
        uint curr = (uint)(ip - @base);
        uint hashLog = cParams->hashLog;
        uint minMatch = (uint)(mls == 3 ? 3 : 4);
        uint* hashTable = ms->hashTable;
        nuint h = ZSTD_hashPtr(ip, hashLog, mls);
        uint matchIndex = hashTable[h];
        uint* bt = ms->chainTable;
        uint btLog = cParams->chainLog - 1;
        uint btMask = (1U << (int)btLog) - 1;
        nuint commonLengthSmaller = 0,
            commonLengthLarger = 0;
        byte* dictBase = ms->window.dictBase;
        uint dictLimit = ms->window.dictLimit;
        byte* dictEnd = dictBase + dictLimit;
        byte* prefixStart = @base + dictLimit;
        uint btLow = btMask >= curr ? 0 : curr - btMask;
        uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, cParams->windowLog);
        uint matchLow = windowLow != 0 ? windowLow : 1;
        uint* smallerPtr = bt + 2 * (curr & btMask);
        uint* largerPtr = bt + 2 * (curr & btMask) + 1;
        /* farthest referenced position of any match => detects repetitive patterns */
        uint matchEndIdx = curr + 8 + 1;
        /* to be nullified at the end */
        uint dummy32;
        uint mnum = 0;
        uint nbCompares = 1U << (int)cParams->searchLog;
        ZSTD_MatchState_t* dms =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? ms->dictMatchState : null;
        ZSTD_compressionParameters* dmsCParams =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? &dms->cParams : null;
        byte* dmsBase = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.@base : null;
        byte* dmsEnd = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.nextSrc : null;
        uint dmsHighLimit =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? (uint)(dmsEnd - dmsBase) : 0;
        uint dmsLowLimit =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.lowLimit : 0;
        uint dmsIndexDelta =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? windowLow - dmsHighLimit : 0;
        uint dmsHashLog =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dmsCParams->hashLog : hashLog;
        uint dmsBtLog =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dmsCParams->chainLog - 1 : btLog;
        uint dmsBtMask =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? (1U << (int)dmsBtLog) - 1 : 0;
        uint dmsBtLow =
            dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState
            && dmsBtMask < dmsHighLimit - dmsLowLimit
                ? dmsHighLimit - dmsBtMask
                : dmsLowLimit;
        nuint bestLength = lengthToBeat - 1;
        assert(ll0 <= 1);
        {
            uint lastR = 3 + ll0;
            uint repCode;
            for (repCode = ll0; repCode < lastR; repCode++)
            {
                uint repOffset = repCode == 3 ? rep[0] - 1 : rep[repCode];
                uint repIndex = curr - repOffset;
                uint repLen = 0;
                assert(curr >= dictLimit);
                if (repOffset - 1 < curr - dictLimit)
                {
                    if (
                        repIndex >= windowLow
                        && ZSTD_readMINMATCH(ip, minMatch)
                            == ZSTD_readMINMATCH(ip - repOffset, minMatch)
                    )
                    {
                        repLen =
                            (uint)ZSTD_count(ip + minMatch, ip + minMatch - repOffset, iLimit)
                            + minMatch;
                    }
                }
                else
                {
                    byte* repMatch =
                        dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState
                            ? dmsBase + repIndex - dmsIndexDelta
                            : dictBase + repIndex;
                    assert(curr >= windowLow);
                    if (
                        dictMode == ZSTD_dictMode_e.ZSTD_extDict
                        && (
                            (repOffset - 1 < curr - windowLow ? 1 : 0)
                            & ZSTD_index_overlap_check(dictLimit, repIndex)
                        ) != 0
                        && ZSTD_readMINMATCH(ip, minMatch) == ZSTD_readMINMATCH(repMatch, minMatch)
                    )
                    {
                        repLen =
                            (uint)ZSTD_count_2segments(
                                ip + minMatch,
                                repMatch + minMatch,
                                iLimit,
                                dictEnd,
                                prefixStart
                            ) + minMatch;
                    }

                    if (
                        dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState
                        && (
                            (repOffset - 1 < curr - (dmsLowLimit + dmsIndexDelta) ? 1 : 0)
                            & ZSTD_index_overlap_check(dictLimit, repIndex)
                        ) != 0
                        && ZSTD_readMINMATCH(ip, minMatch) == ZSTD_readMINMATCH(repMatch, minMatch)
                    )
                    {
                        repLen =
                            (uint)ZSTD_count_2segments(
                                ip + minMatch,
                                repMatch + minMatch,
                                iLimit,
                                dmsEnd,
                                prefixStart
                            ) + minMatch;
                    }
                }

                if (repLen > bestLength)
                {
                    bestLength = repLen;
                    assert(repCode - ll0 + 1 >= 1);
                    assert(repCode - ll0 + 1 <= 3);
                    matches[mnum].off = repCode - ll0 + 1;
                    matches[mnum].len = repLen;
                    mnum++;
                    if (repLen > sufficient_len || ip + repLen == iLimit)
                    {
                        return mnum;
                    }
                }
            }
        }

        if (mls == 3 && bestLength < mls)
        {
            uint matchIndex3 = ZSTD_insertAndFindFirstIndexHash3(ms, nextToUpdate3, ip);
            if (matchIndex3 >= matchLow && curr - matchIndex3 < 1 << 18)
            {
                nuint mlen;
                if (
                    dictMode == ZSTD_dictMode_e.ZSTD_noDict
                    || dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState
                    || matchIndex3 >= dictLimit
                )
                {
                    byte* match = @base + matchIndex3;
                    mlen = ZSTD_count(ip, match, iLimit);
                }
                else
                {
                    byte* match = dictBase + matchIndex3;
                    mlen = ZSTD_count_2segments(ip, match, iLimit, dictEnd, prefixStart);
                }

                if (mlen >= mls)
                {
                    bestLength = mlen;
                    assert(curr > matchIndex3);
                    assert(mnum == 0);
                    assert(curr - matchIndex3 > 0);
                    matches[0].off = curr - matchIndex3 + 3;
                    matches[0].len = (uint)mlen;
                    mnum = 1;
                    if (mlen > sufficient_len || ip + mlen == iLimit)
                    {
                        ms->nextToUpdate = curr + 1;
                        return 1;
                    }
                }
            }
        }

        hashTable[h] = curr;
        for (; nbCompares != 0 && matchIndex >= matchLow; --nbCompares)
        {
            uint* nextPtr = bt + 2 * (matchIndex & btMask);
            byte* match;
            /* guaranteed minimum nb of common bytes */
            nuint matchLength =
                commonLengthSmaller < commonLengthLarger ? commonLengthSmaller : commonLengthLarger;
            assert(curr > matchIndex);
            if (
                dictMode == ZSTD_dictMode_e.ZSTD_noDict
                || dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState
                || matchIndex + matchLength >= dictLimit
            )
            {
                assert(matchIndex + matchLength >= dictLimit);
                match = @base + matchIndex;
#if DEBUG
                if (matchIndex >= dictLimit)
                    assert(memcmp(match, ip, matchLength) == 0);
#endif
                matchLength += ZSTD_count(ip + matchLength, match + matchLength, iLimit);
            }
            else
            {
                match = dictBase + matchIndex;
                assert(memcmp(match, ip, matchLength) == 0);
                matchLength += ZSTD_count_2segments(
                    ip + matchLength,
                    match + matchLength,
                    iLimit,
                    dictEnd,
                    prefixStart
                );
                if (matchIndex + matchLength >= dictLimit)
                    match = @base + matchIndex;
            }

            if (matchLength > bestLength)
            {
                assert(matchEndIdx > matchIndex);
                if (matchLength > matchEndIdx - matchIndex)
                    matchEndIdx = matchIndex + (uint)matchLength;
                bestLength = matchLength;
                assert(curr - matchIndex > 0);
                matches[mnum].off = curr - matchIndex + 3;
                matches[mnum].len = (uint)matchLength;
                mnum++;
                if (matchLength > 1 << 12 || ip + matchLength == iLimit)
                {
                    if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
                        nbCompares = 0;
                    break;
                }
            }

            if (match[matchLength] < ip[matchLength])
            {
                *smallerPtr = matchIndex;
                commonLengthSmaller = matchLength;
                if (matchIndex <= btLow)
                {
                    smallerPtr = &dummy32;
                    break;
                }

                smallerPtr = nextPtr + 1;
                matchIndex = nextPtr[1];
            }
            else
            {
                *largerPtr = matchIndex;
                commonLengthLarger = matchLength;
                if (matchIndex <= btLow)
                {
                    largerPtr = &dummy32;
                    break;
                }

                largerPtr = nextPtr;
                matchIndex = nextPtr[0];
            }
        }

        *smallerPtr = *largerPtr = 0;
        assert(nbCompares <= 1U << (sizeof(nuint) == 4 ? 30 : 31) - 1);
        if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && nbCompares != 0)
        {
            nuint dmsH = ZSTD_hashPtr(ip, dmsHashLog, mls);
            uint dictMatchIndex = dms->hashTable[dmsH];
            uint* dmsBt = dms->chainTable;
            commonLengthSmaller = commonLengthLarger = 0;
            for (; nbCompares != 0 && dictMatchIndex > dmsLowLimit; --nbCompares)
            {
                uint* nextPtr = dmsBt + 2 * (dictMatchIndex & dmsBtMask);
                /* guaranteed minimum nb of common bytes */
                nuint matchLength =
                    commonLengthSmaller < commonLengthLarger
                        ? commonLengthSmaller
                        : commonLengthLarger;
                byte* match = dmsBase + dictMatchIndex;
                matchLength += ZSTD_count_2segments(
                    ip + matchLength,
                    match + matchLength,
                    iLimit,
                    dmsEnd,
                    prefixStart
                );
                if (dictMatchIndex + matchLength >= dmsHighLimit)
                    match = @base + dictMatchIndex + dmsIndexDelta;
                if (matchLength > bestLength)
                {
                    matchIndex = dictMatchIndex + dmsIndexDelta;
                    if (matchLength > matchEndIdx - matchIndex)
                        matchEndIdx = matchIndex + (uint)matchLength;
                    bestLength = matchLength;
                    assert(curr - matchIndex > 0);
                    matches[mnum].off = curr - matchIndex + 3;
                    matches[mnum].len = (uint)matchLength;
                    mnum++;
                    if (matchLength > 1 << 12 || ip + matchLength == iLimit)
                    {
                        break;
                    }
                }

                if (dictMatchIndex <= dmsBtLow)
                {
                    break;
                }

                if (match[matchLength] < ip[matchLength])
                {
                    commonLengthSmaller = matchLength;
                    dictMatchIndex = nextPtr[1];
                }
                else
                {
                    commonLengthLarger = matchLength;
                    dictMatchIndex = nextPtr[0];
                }
            }
        }

        assert(matchEndIdx > curr + 8);
        ms->nextToUpdate = matchEndIdx - 8;
        return mnum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_btGetAllMatches_internal(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat,
        ZSTD_dictMode_e dictMode,
        uint mls
    )
    {
        assert(
            (
                ms->cParams.minMatch <= 3 ? 3
                : ms->cParams.minMatch <= 6 ? ms->cParams.minMatch
                : 6
            ) == mls
        );
        if (ip < ms->window.@base + ms->nextToUpdate)
            return 0;
        ZSTD_updateTree_internal(ms, ip, iHighLimit, mls, dictMode);
        return ZSTD_insertBtAndGetAllMatches(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            dictMode,
            rep,
            ll0,
            lengthToBeat,
            mls
        );
    }

    private static uint ZSTD_btGetAllMatches_noDict_3(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_noDict,
            3
        );
    }

    private static uint ZSTD_btGetAllMatches_noDict_4(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_noDict,
            4
        );
    }

    private static uint ZSTD_btGetAllMatches_noDict_5(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_noDict,
            5
        );
    }

    private static uint ZSTD_btGetAllMatches_noDict_6(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_noDict,
            6
        );
    }

    private static uint ZSTD_btGetAllMatches_extDict_3(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_extDict,
            3
        );
    }

    private static uint ZSTD_btGetAllMatches_extDict_4(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_extDict,
            4
        );
    }

    private static uint ZSTD_btGetAllMatches_extDict_5(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_extDict,
            5
        );
    }

    private static uint ZSTD_btGetAllMatches_extDict_6(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_extDict,
            6
        );
    }

    private static uint ZSTD_btGetAllMatches_dictMatchState_3(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            3
        );
    }

    private static uint ZSTD_btGetAllMatches_dictMatchState_4(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            4
        );
    }

    private static uint ZSTD_btGetAllMatches_dictMatchState_5(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            5
        );
    }

    private static uint ZSTD_btGetAllMatches_dictMatchState_6(
        ZSTD_match_t* matches,
        ZSTD_MatchState_t* ms,
        uint* nextToUpdate3,
        byte* ip,
        byte* iHighLimit,
        uint* rep,
        uint ll0,
        uint lengthToBeat
    )
    {
        return ZSTD_btGetAllMatches_internal(
            matches,
            ms,
            nextToUpdate3,
            ip,
            iHighLimit,
            rep,
            ll0,
            lengthToBeat,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            6
        );
    }

    private static readonly ZSTD_getAllMatchesFn[][] getAllMatchesFns = new ZSTD_getAllMatchesFn[
        3
    ][]
    {
        new ZSTD_getAllMatchesFn[4]
        {
            ZSTD_btGetAllMatches_noDict_3,
            ZSTD_btGetAllMatches_noDict_4,
            ZSTD_btGetAllMatches_noDict_5,
            ZSTD_btGetAllMatches_noDict_6,
        },
        new ZSTD_getAllMatchesFn[4]
        {
            ZSTD_btGetAllMatches_extDict_3,
            ZSTD_btGetAllMatches_extDict_4,
            ZSTD_btGetAllMatches_extDict_5,
            ZSTD_btGetAllMatches_extDict_6,
        },
        new ZSTD_getAllMatchesFn[4]
        {
            ZSTD_btGetAllMatches_dictMatchState_3,
            ZSTD_btGetAllMatches_dictMatchState_4,
            ZSTD_btGetAllMatches_dictMatchState_5,
            ZSTD_btGetAllMatches_dictMatchState_6,
        },
    };

    private static ZSTD_getAllMatchesFn ZSTD_selectBtGetAllMatches(
        ZSTD_MatchState_t* ms,
        ZSTD_dictMode_e dictMode
    )
    {
        uint mls =
            ms->cParams.minMatch <= 3 ? 3
            : ms->cParams.minMatch <= 6 ? ms->cParams.minMatch
            : 6;
        assert((uint)dictMode < 3);
        assert(mls - 3 < 4);
        return getAllMatchesFns[(int)dictMode][mls - 3];
    }

    /* ZSTD_optLdm_skipRawSeqStoreBytes():
     * Moves forward in @rawSeqStore by @nbBytes,
     * which will update the fields 'pos' and 'posInSequence'.
     */
    private static void ZSTD_optLdm_skipRawSeqStoreBytes(RawSeqStore_t* rawSeqStore, nuint nbBytes)
    {
        uint currPos = (uint)(rawSeqStore->posInSequence + nbBytes);
        while (currPos != 0 && rawSeqStore->pos < rawSeqStore->size)
        {
            rawSeq currSeq = rawSeqStore->seq[rawSeqStore->pos];
            if (currPos >= currSeq.litLength + currSeq.matchLength)
            {
                currPos -= currSeq.litLength + currSeq.matchLength;
                rawSeqStore->pos++;
            }
            else
            {
                rawSeqStore->posInSequence = currPos;
                break;
            }
        }

        if (currPos == 0 || rawSeqStore->pos == rawSeqStore->size)
        {
            rawSeqStore->posInSequence = 0;
        }
    }

    /* ZSTD_opt_getNextMatchAndUpdateSeqStore():
     * Calculates the beginning and end of the next match in the current block.
     * Updates 'pos' and 'posInSequence' of the ldmSeqStore.
     */
    private static void ZSTD_opt_getNextMatchAndUpdateSeqStore(
        ZSTD_optLdm_t* optLdm,
        uint currPosInBlock,
        uint blockBytesRemaining
    )
    {
        rawSeq currSeq;
        uint currBlockEndPos;
        uint literalsBytesRemaining;
        uint matchBytesRemaining;
        if (optLdm->seqStore.size == 0 || optLdm->seqStore.pos >= optLdm->seqStore.size)
        {
            optLdm->startPosInBlock = 0xffffffff;
            optLdm->endPosInBlock = 0xffffffff;
            return;
        }

        currSeq = optLdm->seqStore.seq[optLdm->seqStore.pos];
        assert(optLdm->seqStore.posInSequence <= currSeq.litLength + currSeq.matchLength);
        currBlockEndPos = currPosInBlock + blockBytesRemaining;
        literalsBytesRemaining =
            optLdm->seqStore.posInSequence < currSeq.litLength
                ? currSeq.litLength - (uint)optLdm->seqStore.posInSequence
                : 0;
        matchBytesRemaining =
            literalsBytesRemaining == 0
                ? currSeq.matchLength - ((uint)optLdm->seqStore.posInSequence - currSeq.litLength)
                : currSeq.matchLength;
        if (literalsBytesRemaining >= blockBytesRemaining)
        {
            optLdm->startPosInBlock = 0xffffffff;
            optLdm->endPosInBlock = 0xffffffff;
            ZSTD_optLdm_skipRawSeqStoreBytes(&optLdm->seqStore, blockBytesRemaining);
            return;
        }

        optLdm->startPosInBlock = currPosInBlock + literalsBytesRemaining;
        optLdm->endPosInBlock = optLdm->startPosInBlock + matchBytesRemaining;
        optLdm->offset = currSeq.offset;
        if (optLdm->endPosInBlock > currBlockEndPos)
        {
            optLdm->endPosInBlock = currBlockEndPos;
            ZSTD_optLdm_skipRawSeqStoreBytes(&optLdm->seqStore, currBlockEndPos - currPosInBlock);
        }
        else
        {
            ZSTD_optLdm_skipRawSeqStoreBytes(
                &optLdm->seqStore,
                literalsBytesRemaining + matchBytesRemaining
            );
        }
    }

    /* ZSTD_optLdm_maybeAddMatch():
     * Adds a match if it's long enough,
     * based on it's 'matchStartPosInBlock' and 'matchEndPosInBlock',
     * into 'matches'. Maintains the correct ordering of 'matches'.
     */
    private static void ZSTD_optLdm_maybeAddMatch(
        ZSTD_match_t* matches,
        uint* nbMatches,
        ZSTD_optLdm_t* optLdm,
        uint currPosInBlock,
        uint minMatch
    )
    {
        uint posDiff = currPosInBlock - optLdm->startPosInBlock;
        /* Note: ZSTD_match_t actually contains offBase and matchLength (before subtracting MINMATCH) */
        uint candidateMatchLength = optLdm->endPosInBlock - optLdm->startPosInBlock - posDiff;
        if (
            currPosInBlock < optLdm->startPosInBlock
            || currPosInBlock >= optLdm->endPosInBlock
            || candidateMatchLength < minMatch
        )
        {
            return;
        }

        if (
            *nbMatches == 0
            || candidateMatchLength > matches[*nbMatches - 1].len && *nbMatches < 1 << 12
        )
        {
            assert(optLdm->offset > 0);
            uint candidateOffBase = optLdm->offset + 3;
            matches[*nbMatches].len = candidateMatchLength;
            matches[*nbMatches].off = candidateOffBase;
            (*nbMatches)++;
        }
    }

    /* ZSTD_optLdm_processMatchCandidate():
     * Wrapper function to update ldm seq store and call ldm functions as necessary.
     */
    private static void ZSTD_optLdm_processMatchCandidate(
        ZSTD_optLdm_t* optLdm,
        ZSTD_match_t* matches,
        uint* nbMatches,
        uint currPosInBlock,
        uint remainingBytes,
        uint minMatch
    )
    {
        if (optLdm->seqStore.size == 0 || optLdm->seqStore.pos >= optLdm->seqStore.size)
        {
            return;
        }

        if (currPosInBlock >= optLdm->endPosInBlock)
        {
            if (currPosInBlock > optLdm->endPosInBlock)
            {
                /* The position at which ZSTD_optLdm_processMatchCandidate() is called is not necessarily
                 * at the end of a match from the ldm seq store, and will often be some bytes
                 * over beyond matchEndPosInBlock. As such, we need to correct for these "overshoots"
                 */
                uint posOvershoot = currPosInBlock - optLdm->endPosInBlock;
                ZSTD_optLdm_skipRawSeqStoreBytes(&optLdm->seqStore, posOvershoot);
            }

            ZSTD_opt_getNextMatchAndUpdateSeqStore(optLdm, currPosInBlock, remainingBytes);
        }

        ZSTD_optLdm_maybeAddMatch(matches, nbMatches, optLdm, currPosInBlock, minMatch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_opt_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        int optLevel,
        ZSTD_dictMode_e dictMode
    )
    {
        optState_t* optStatePtr = &ms->opt;
        byte* istart = (byte*)src;
        byte* ip = istart;
        byte* anchor = istart;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        byte* @base = ms->window.@base;
        byte* prefixStart = @base + ms->window.dictLimit;
        ZSTD_compressionParameters* cParams = &ms->cParams;
        ZSTD_getAllMatchesFn getAllMatches = ZSTD_selectBtGetAllMatches(ms, dictMode);
        uint sufficient_len =
            cParams->targetLength < (1 << 12) - 1 ? cParams->targetLength : (1 << 12) - 1;
        uint minMatch = (uint)(cParams->minMatch == 3 ? 3 : 4);
        uint nextToUpdate3 = ms->nextToUpdate;
        ZSTD_optimal_t* opt = optStatePtr->priceTable;
        ZSTD_match_t* matches = optStatePtr->matchTable;
        ZSTD_optimal_t lastStretch;
        ZSTD_optLdm_t optLdm;
        lastStretch = new ZSTD_optimal_t();
        optLdm.seqStore = ms->ldmSeqStore != null ? *ms->ldmSeqStore : kNullRawSeqStore;
        optLdm.endPosInBlock = optLdm.startPosInBlock = optLdm.offset = 0;
        ZSTD_opt_getNextMatchAndUpdateSeqStore(&optLdm, (uint)(ip - istart), (uint)(iend - ip));
        assert(optLevel <= 2);
        ZSTD_rescaleFreqs(optStatePtr, (byte*)src, srcSize, optLevel);
        ip += ip == prefixStart ? 1 : 0;
        while (ip < ilimit)
        {
            uint cur,
                last_pos = 0;
            {
                uint litlen = (uint)(ip - anchor);
                uint ll0 = litlen == 0 ? 1U : 0U;
                uint nbMatches = getAllMatches(
                    matches,
                    ms,
                    &nextToUpdate3,
                    ip,
                    iend,
                    rep,
                    ll0,
                    minMatch
                );
                ZSTD_optLdm_processMatchCandidate(
                    &optLdm,
                    matches,
                    &nbMatches,
                    (uint)(ip - istart),
                    (uint)(iend - ip),
                    minMatch
                );
                if (nbMatches == 0)
                {
                    ip++;
                    continue;
                }

                opt[0].mlen = 0;
                opt[0].litlen = litlen;
                opt[0].price = (int)ZSTD_litLengthPrice(litlen, optStatePtr, optLevel);
                memcpy(&opt[0].rep[0], rep, sizeof(uint) * 3);
                {
                    uint maxML = matches[nbMatches - 1].len;
                    uint maxOffBase = matches[nbMatches - 1].off;
                    if (maxML > sufficient_len)
                    {
                        lastStretch.litlen = 0;
                        lastStretch.mlen = maxML;
                        lastStretch.off = maxOffBase;
                        cur = 0;
                        last_pos = maxML;
                        goto _shortestPath;
                    }
                }

                assert(opt[0].price >= 0);
                {
                    uint pos;
                    uint matchNb;
                    for (pos = 1; pos < minMatch; pos++)
                    {
                        opt[pos].price = 1 << 30;
                        opt[pos].mlen = 0;
                        opt[pos].litlen = litlen + pos;
                    }

                    for (matchNb = 0; matchNb < nbMatches; matchNb++)
                    {
                        uint offBase = matches[matchNb].off;
                        uint end = matches[matchNb].len;
                        for (; pos <= end; pos++)
                        {
                            int matchPrice = (int)ZSTD_getMatchPrice(
                                offBase,
                                pos,
                                optStatePtr,
                                optLevel
                            );
                            int sequencePrice = opt[0].price + matchPrice;
                            opt[pos].mlen = pos;
                            opt[pos].off = offBase;
                            opt[pos].litlen = 0;
                            opt[pos].price =
                                sequencePrice + (int)ZSTD_litLengthPrice(0, optStatePtr, optLevel);
                        }
                    }

                    last_pos = pos - 1;
                    opt[pos].price = 1 << 30;
                }
            }

            for (cur = 1; cur <= last_pos; cur++)
            {
                byte* inr = ip + cur;
                assert(cur <= 1 << 12);
                {
                    uint litlen = opt[cur - 1].litlen + 1;
                    int price =
                        opt[cur - 1].price
                        + (int)ZSTD_rawLiteralsCost(ip + cur - 1, 1, optStatePtr, optLevel)
                        + (
                            (int)ZSTD_litLengthPrice(litlen, optStatePtr, optLevel)
                            - (int)ZSTD_litLengthPrice(litlen - 1, optStatePtr, optLevel)
                        );
                    assert(price < 1000000000);
                    if (price <= opt[cur].price)
                    {
                        ZSTD_optimal_t prevMatch = opt[cur];
                        opt[cur] = opt[cur - 1];
                        opt[cur].litlen = litlen;
                        opt[cur].price = price;
                        if (
                            optLevel >= 1
                            && prevMatch.litlen == 0
                            && (int)ZSTD_litLengthPrice(1, optStatePtr, optLevel)
                                - (int)ZSTD_litLengthPrice(1 - 1, optStatePtr, optLevel)
                                < 0
                            && ip + cur < iend
                        )
                        {
                            /* check next position, in case it would be cheaper */
                            int with1literal =
                                prevMatch.price
                                + (int)ZSTD_rawLiteralsCost(ip + cur, 1, optStatePtr, optLevel)
                                + (
                                    (int)ZSTD_litLengthPrice(1, optStatePtr, optLevel)
                                    - (int)ZSTD_litLengthPrice(1 - 1, optStatePtr, optLevel)
                                );
                            int withMoreLiterals =
                                price
                                + (int)ZSTD_rawLiteralsCost(ip + cur, 1, optStatePtr, optLevel)
                                + (
                                    (int)ZSTD_litLengthPrice(litlen + 1, optStatePtr, optLevel)
                                    - (int)ZSTD_litLengthPrice(
                                        litlen + 1 - 1,
                                        optStatePtr,
                                        optLevel
                                    )
                                );
                            if (
                                with1literal < withMoreLiterals
                                && with1literal < opt[cur + 1].price
                            )
                            {
                                /* update offset history - before it disappears */
                                uint prev = cur - prevMatch.mlen;
                                repcodes_s newReps = ZSTD_newRep(
                                    opt[prev].rep,
                                    prevMatch.off,
                                    opt[prev].litlen == 0 ? 1U : 0U
                                );
                                assert(cur >= prevMatch.mlen);
                                opt[cur + 1] = prevMatch;
                                memcpy(opt[cur + 1].rep, &newReps, (uint)sizeof(repcodes_s));
                                opt[cur + 1].litlen = 1;
                                opt[cur + 1].price = with1literal;
                                if (last_pos < cur + 1)
                                    last_pos = cur + 1;
                            }
                        }
                    }
                }

                assert(cur >= opt[cur].mlen);
                if (opt[cur].litlen == 0)
                {
                    /* just finished a match => alter offset history */
                    uint prev = cur - opt[cur].mlen;
                    repcodes_s newReps = ZSTD_newRep(
                        opt[prev].rep,
                        opt[cur].off,
                        opt[prev].litlen == 0 ? 1U : 0U
                    );
                    memcpy(opt[cur].rep, &newReps, (uint)sizeof(repcodes_s));
                }

                if (inr > ilimit)
                    continue;
                if (cur == last_pos)
                    break;
                if (optLevel == 0 && opt[cur + 1].price <= opt[cur].price + (1 << 8) / 2)
                {
                    continue;
                }

                assert(opt[cur].price >= 0);
                {
                    uint ll0 = opt[cur].litlen == 0 ? 1U : 0U;
                    int previousPrice = opt[cur].price;
                    int basePrice =
                        previousPrice + (int)ZSTD_litLengthPrice(0, optStatePtr, optLevel);
                    uint nbMatches = getAllMatches(
                        matches,
                        ms,
                        &nextToUpdate3,
                        inr,
                        iend,
                        opt[cur].rep,
                        ll0,
                        minMatch
                    );
                    uint matchNb;
                    ZSTD_optLdm_processMatchCandidate(
                        &optLdm,
                        matches,
                        &nbMatches,
                        (uint)(inr - istart),
                        (uint)(iend - inr),
                        minMatch
                    );
                    if (nbMatches == 0)
                    {
                        continue;
                    }

                    {
                        uint longestML = matches[nbMatches - 1].len;
                        if (
                            longestML > sufficient_len
                            || cur + longestML >= 1 << 12
                            || ip + cur + longestML >= iend
                        )
                        {
                            lastStretch.mlen = longestML;
                            lastStretch.off = matches[nbMatches - 1].off;
                            lastStretch.litlen = 0;
                            last_pos = cur + longestML;
                            goto _shortestPath;
                        }
                    }

                    for (matchNb = 0; matchNb < nbMatches; matchNb++)
                    {
                        uint offset = matches[matchNb].off;
                        uint lastML = matches[matchNb].len;
                        uint startML = matchNb > 0 ? matches[matchNb - 1].len + 1 : minMatch;
                        uint mlen;
                        for (mlen = lastML; mlen >= startML; mlen--)
                        {
                            uint pos = cur + mlen;
                            int price =
                                basePrice
                                + (int)ZSTD_getMatchPrice(offset, mlen, optStatePtr, optLevel);
                            if (pos > last_pos || price < opt[pos].price)
                            {
                                while (last_pos < pos)
                                {
                                    last_pos++;
                                    opt[last_pos].price = 1 << 30;
                                    opt[last_pos].litlen = 0 == 0 ? 1U : 0U;
                                }

                                opt[pos].mlen = mlen;
                                opt[pos].off = offset;
                                opt[pos].litlen = 0;
                                opt[pos].price = price;
                            }
                            else
                            {
                                if (optLevel == 0)
                                    break;
                            }
                        }
                    }
                }

                opt[last_pos + 1].price = 1 << 30;
            }

            lastStretch = opt[last_pos];
            assert(cur >= lastStretch.mlen);
            cur = last_pos - lastStretch.mlen;
            _shortestPath:
            assert(opt[0].mlen == 0);
            assert(last_pos >= lastStretch.mlen);
            assert(cur == last_pos - lastStretch.mlen);
            if (lastStretch.mlen == 0)
            {
                assert(lastStretch.litlen == (uint)(ip - anchor) + last_pos);
                ip += last_pos;
                continue;
            }

            assert(lastStretch.off > 0);
            if (lastStretch.litlen == 0)
            {
                /* finishing on a match : update offset history */
                repcodes_s reps = ZSTD_newRep(
                    opt[cur].rep,
                    lastStretch.off,
                    opt[cur].litlen == 0 ? 1U : 0U
                );
                memcpy(rep, &reps, (uint)sizeof(repcodes_s));
            }
            else
            {
                memcpy(rep, lastStretch.rep, (uint)sizeof(repcodes_s));
                assert(cur >= lastStretch.litlen);
                cur -= lastStretch.litlen;
            }

            {
                uint storeEnd = cur + 2;
                uint storeStart = storeEnd;
                uint stretchPos = cur;
                assert(storeEnd < (1 << 12) + 3);
                if (lastStretch.litlen > 0)
                {
                    opt[storeEnd].litlen = lastStretch.litlen;
                    opt[storeEnd].mlen = 0;
                    storeStart = storeEnd - 1;
                    opt[storeStart] = lastStretch;
                }

                {
                    opt[storeEnd] = lastStretch;
                    storeStart = storeEnd;
                }

                while (true)
                {
                    ZSTD_optimal_t nextStretch = opt[stretchPos];
                    opt[storeStart].litlen = nextStretch.litlen;
                    if (nextStretch.mlen == 0)
                    {
                        break;
                    }

                    storeStart--;
                    opt[storeStart] = nextStretch;
                    assert(nextStretch.litlen + nextStretch.mlen <= stretchPos);
                    stretchPos -= nextStretch.litlen + nextStretch.mlen;
                }

                {
                    uint storePos;
                    for (storePos = storeStart; storePos <= storeEnd; storePos++)
                    {
                        uint llen = opt[storePos].litlen;
                        uint mlen = opt[storePos].mlen;
                        uint offBase = opt[storePos].off;
                        uint advance = llen + mlen;
                        if (mlen == 0)
                        {
                            assert(storePos == storeEnd);
                            ip = anchor + llen;
                            continue;
                        }

                        assert(anchor + llen <= iend);
                        ZSTD_updateStats(optStatePtr, llen, anchor, offBase, mlen);
                        ZSTD_storeSeq(seqStore, llen, anchor, iend, offBase, mlen);
                        anchor += advance;
                        ip = anchor;
                    }
                }

                ZSTD_setBasePrices(optStatePtr, optLevel);
            }
        }

        return (nuint)(iend - anchor);
    }

    private static nuint ZSTD_compressBlock_opt0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        ZSTD_dictMode_e dictMode
    )
    {
        return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 0, dictMode);
    }

    private static nuint ZSTD_compressBlock_opt2(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        ZSTD_dictMode_e dictMode
    )
    {
        return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 2, dictMode);
    }

    private static nuint ZSTD_compressBlock_btopt(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_opt0(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    /* ZSTD_initStats_ultra():
     * make a first compression pass, just to seed stats with more accurate starting values.
     * only works on first block, with no dictionary and no ldm.
     * this function cannot error out, its narrow contract must be respected.
     */
    private static void ZSTD_initStats_ultra(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        /* updated rep codes will sink here */
        uint* tmpRep = stackalloc uint[3];
        memcpy(tmpRep, rep, sizeof(uint) * 3);
        assert(ms->opt.litLengthSum == 0);
        assert(seqStore->sequences == seqStore->sequencesStart);
        assert(ms->window.dictLimit == ms->window.lowLimit);
        assert(ms->window.dictLimit - ms->nextToUpdate <= 1);
        ZSTD_compressBlock_opt2(ms, seqStore, tmpRep, src, srcSize, ZSTD_dictMode_e.ZSTD_noDict);
        ZSTD_resetSeqStore(seqStore);
        ms->window.@base -= srcSize;
        ms->window.dictLimit += (uint)srcSize;
        ms->window.lowLimit = ms->window.dictLimit;
        ms->nextToUpdate = ms->window.dictLimit;
    }

    private static nuint ZSTD_compressBlock_btultra(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_opt2(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    /* note : no btultra2 variant for extDict nor dictMatchState,
     * because btultra2 is not meant to work with dictionaries
     * and is only specific for the first block (no prefix) */
    private static nuint ZSTD_compressBlock_btultra2(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint curr = (uint)((byte*)src - ms->window.@base);
        assert(srcSize <= 1 << 17);
        if (
            ms->opt.litLengthSum == 0
            && seqStore->sequences == seqStore->sequencesStart
            && ms->window.dictLimit == ms->window.lowLimit
            && curr == ms->window.dictLimit
            && srcSize > 8
        )
        {
            ZSTD_initStats_ultra(ms, seqStore, rep, src, srcSize);
        }

        return ZSTD_compressBlock_opt2(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_btopt_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_opt0(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_btopt_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_opt0(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_extDict
        );
    }

    private static nuint ZSTD_compressBlock_btultra_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_opt2(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_btultra_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_opt2(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            ZSTD_dictMode_e.ZSTD_extDict
        );
    }
}
