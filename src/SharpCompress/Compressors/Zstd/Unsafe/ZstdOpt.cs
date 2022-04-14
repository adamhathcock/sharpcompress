using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_bitWeight(uint stat)
        {
            return (ZSTD_highbit32(stat + 1) * (uint)((1 << 8)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_fracWeight(uint rawStat)
        {
            uint stat = rawStat + 1;
            uint hb = ZSTD_highbit32(stat);
            uint BWeight = hb * (uint)((1 << 8));
            uint FWeight = (stat << 8) >> (int)hb;
            uint weight = BWeight + FWeight;

            assert(hb + 8 < 31);
            return weight;
        }

        private static int ZSTD_compressedLiterals(optState_t* optPtr)
        {
            return ((optPtr->literalCompressionMode != ZSTD_literalCompressionMode_e.ZSTD_lcm_uncompressed) ? 1 : 0);
        }

        private static void ZSTD_setBasePrices(optState_t* optPtr, int optLevel)
        {
            if ((ZSTD_compressedLiterals(optPtr)) != 0)
            {
                optPtr->litSumBasePrice = (optLevel != 0 ? ZSTD_fracWeight(optPtr->litSum) : ZSTD_bitWeight(optPtr->litSum));
            }

            optPtr->litLengthSumBasePrice = (optLevel != 0 ? ZSTD_fracWeight(optPtr->litLengthSum) : ZSTD_bitWeight(optPtr->litLengthSum));
            optPtr->matchLengthSumBasePrice = (optLevel != 0 ? ZSTD_fracWeight(optPtr->matchLengthSum) : ZSTD_bitWeight(optPtr->matchLengthSum));
            optPtr->offCodeSumBasePrice = (optLevel != 0 ? ZSTD_fracWeight(optPtr->offCodeSum) : ZSTD_bitWeight(optPtr->offCodeSum));
        }

        /* ZSTD_downscaleStat() :
         * reduce all elements in table by a factor 2^(ZSTD_FREQ_DIV+malus)
         * return the resulting sum of elements */
        private static uint ZSTD_downscaleStat(uint* table, uint lastEltIndex, int malus)
        {
            uint s, sum = 0;

            assert(4 + malus > 0 && 4 + malus < 31);
            for (s = 0; s < lastEltIndex + 1; s++)
            {
                table[s] = 1 + (table[s] >> (4 + malus));
                sum += table[s];
            }

            return sum;
        }

        /* ZSTD_rescaleFreqs() :
         * if first block (detected by optPtr->litLengthSum == 0) : init statistics
         *    take hints from dictionary if there is one
         *    or init from zero, using src for literals stats, or flat 1 for match symbols
         * otherwise downscale existing stats, to be used as seed for next block.
         */
        private static void ZSTD_rescaleFreqs(optState_t* optPtr, byte* src, nuint srcSize, int optLevel)
        {
            int compressedLiterals = ZSTD_compressedLiterals(optPtr);

            optPtr->priceType = ZSTD_OptPrice_e.zop_dynamic;
            if (optPtr->litLengthSum == 0)
            {
                if (srcSize <= 1024)
                {
                    optPtr->priceType = ZSTD_OptPrice_e.zop_predef;
                }

                assert(optPtr->symbolCosts != null);
                if (optPtr->symbolCosts->huf.repeatMode == HUF_repeat.HUF_repeat_valid)
                {
                    optPtr->priceType = ZSTD_OptPrice_e.zop_dynamic;
                    if (compressedLiterals != 0)
                    {
                        uint lit;

                        assert(optPtr->litFreq != null);
                        optPtr->litSum = 0;
                        for (lit = 0; lit <= (uint)(((1 << 8) - 1)); lit++)
                        {
                            uint scaleLog = 11;
                            uint bitCost = HUF_getNbBits((void*)optPtr->symbolCosts->huf.CTable, lit);

                            assert(bitCost <= scaleLog);
                            optPtr->litFreq[lit] = (uint)(bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1);
                            optPtr->litSum += optPtr->litFreq[lit];
                        }
                    }


                    {
                        uint ll;
                        FSE_CState_t llstate;

                        FSE_initCState(&llstate, (uint*)optPtr->symbolCosts->fse.litlengthCTable);
                        optPtr->litLengthSum = 0;
                        for (ll = 0; ll <= 35; ll++)
                        {
                            uint scaleLog = 10;
                            uint bitCost = FSE_getMaxNbBits(llstate.symbolTT, ll);

                            assert(bitCost < scaleLog);
                            optPtr->litLengthFreq[ll] = (uint)(bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1);
                            optPtr->litLengthSum += optPtr->litLengthFreq[ll];
                        }
                    }


                    {
                        uint ml;
                        FSE_CState_t mlstate;

                        FSE_initCState(&mlstate, (uint*)optPtr->symbolCosts->fse.matchlengthCTable);
                        optPtr->matchLengthSum = 0;
                        for (ml = 0; ml <= 52; ml++)
                        {
                            uint scaleLog = 10;
                            uint bitCost = FSE_getMaxNbBits(mlstate.symbolTT, ml);

                            assert(bitCost < scaleLog);
                            optPtr->matchLengthFreq[ml] = (uint)(bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1);
                            optPtr->matchLengthSum += optPtr->matchLengthFreq[ml];
                        }
                    }


                    {
                        uint of;
                        FSE_CState_t ofstate;

                        FSE_initCState(&ofstate, (uint*)optPtr->symbolCosts->fse.offcodeCTable);
                        optPtr->offCodeSum = 0;
                        for (of = 0; of <= 31; of++)
                        {
                            uint scaleLog = 10;
                            uint bitCost = FSE_getMaxNbBits(ofstate.symbolTT, of);

                            assert(bitCost < scaleLog);
                            optPtr->offCodeFreq[of] = (uint)(bitCost != 0 ? 1 << (int)(scaleLog - bitCost) : 1);
                            optPtr->offCodeSum += optPtr->offCodeFreq[of];
                        }
                    }
                }
                else
                {
                    assert(optPtr->litFreq != null);
                    if (compressedLiterals != 0)
                    {
                        uint lit = (uint)(((1 << 8) - 1));

                        HIST_count_simple(optPtr->litFreq, &lit, (void*)src, srcSize);
                        optPtr->litSum = ZSTD_downscaleStat(optPtr->litFreq, (uint)(((1 << 8) - 1)), 1);
                    }


                    {
                        uint ll;

                        for (ll = 0; ll <= 35; ll++)
                        {
                            optPtr->litLengthFreq[ll] = 1;
                        }
                    }

                    optPtr->litLengthSum = (uint)(35 + 1);

                    {
                        uint ml;

                        for (ml = 0; ml <= 52; ml++)
                        {
                            optPtr->matchLengthFreq[ml] = 1;
                        }
                    }

                    optPtr->matchLengthSum = (uint)(52 + 1);

                    {
                        uint of;

                        for (of = 0; of <= 31; of++)
                        {
                            optPtr->offCodeFreq[of] = 1;
                        }
                    }

                    optPtr->offCodeSum = (uint)(31 + 1);
                }
            }
            else
            {
                if (compressedLiterals != 0)
                {
                    optPtr->litSum = ZSTD_downscaleStat(optPtr->litFreq, (uint)(((1 << 8) - 1)), 1);
                }

                optPtr->litLengthSum = ZSTD_downscaleStat(optPtr->litLengthFreq, 35, 0);
                optPtr->matchLengthSum = ZSTD_downscaleStat(optPtr->matchLengthFreq, 52, 0);
                optPtr->offCodeSum = ZSTD_downscaleStat(optPtr->offCodeFreq, 31, 0);
            }

            ZSTD_setBasePrices(optPtr, optLevel);
        }

        /* ZSTD_rawLiteralsCost() :
         * price of literals (only) in specified segment (which length can be 0).
         * does not include price of literalLength symbol */
        private static uint ZSTD_rawLiteralsCost(byte* literals, uint litLength, optState_t* optPtr, int optLevel)
        {
            if (litLength == 0)
            {
                return 0;
            }

            if ((ZSTD_compressedLiterals(optPtr)) == 0)
            {
                return (litLength << 3) * (uint)((1 << 8));
            }

            if (optPtr->priceType == ZSTD_OptPrice_e.zop_predef)
            {
                return (litLength * 6) * (uint)((1 << 8));
            }


            {
                uint price = litLength * optPtr->litSumBasePrice;
                uint u;

                for (u = 0; u < litLength; u++)
                {
                    assert((optLevel != 0 ? ZSTD_fracWeight(optPtr->litFreq[literals[u]]) : ZSTD_bitWeight(optPtr->litFreq[literals[u]])) <= optPtr->litSumBasePrice);
                    price -= (optLevel != 0 ? ZSTD_fracWeight(optPtr->litFreq[literals[u]]) : ZSTD_bitWeight(optPtr->litFreq[literals[u]]));
                }

                return price;
            }
        }

        /* ZSTD_litLengthPrice() :
         * cost of literalLength symbol */
        private static uint ZSTD_litLengthPrice(uint litLength, optState_t* optPtr, int optLevel)
        {
            if (optPtr->priceType == ZSTD_OptPrice_e.zop_predef)
            {
                return (optLevel != 0 ? ZSTD_fracWeight(litLength) : ZSTD_bitWeight(litLength));
            }


            {
                uint llCode = ZSTD_LLcode(litLength);

                return (LL_bits[llCode] * (uint)((1 << 8))) + optPtr->litLengthSumBasePrice - (optLevel != 0 ? ZSTD_fracWeight(optPtr->litLengthFreq[llCode]) : ZSTD_bitWeight(optPtr->litLengthFreq[llCode]));
            }
        }

        /* ZSTD_getMatchPrice() :
         * Provides the cost of the match part (offset + matchLength) of a sequence
         * Must be combined with ZSTD_fullLiteralsCost() to get the full cost of a sequence.
         * optLevel: when <2, favors small offset for decompression speed (improved cache efficiency) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_getMatchPrice(uint offset, uint matchLength, optState_t* optPtr, int optLevel)
        {
            uint price;
            uint offCode = ZSTD_highbit32(offset + 1);
            uint mlBase = matchLength - 3;

            assert(matchLength >= 3);
            if (optPtr->priceType == ZSTD_OptPrice_e.zop_predef)
            {
                return (optLevel != 0 ? ZSTD_fracWeight(mlBase) : ZSTD_bitWeight(mlBase)) + ((16 + offCode) * (uint)((1 << 8)));
            }

            price = (offCode * (uint)((1 << 8))) + (optPtr->offCodeSumBasePrice - (optLevel != 0 ? ZSTD_fracWeight(optPtr->offCodeFreq[offCode]) : ZSTD_bitWeight(optPtr->offCodeFreq[offCode])));
            if ((optLevel < 2) && offCode >= 20)
            {
                price += (offCode - 19) * 2 * (uint)((1 << 8));
            }


            {
                uint mlCode = ZSTD_MLcode(mlBase);

                price += (ML_bits[mlCode] * (uint)((1 << 8))) + (optPtr->matchLengthSumBasePrice - (optLevel != 0 ? ZSTD_fracWeight(optPtr->matchLengthFreq[mlCode]) : ZSTD_bitWeight(optPtr->matchLengthFreq[mlCode])));
            }

            price += (uint)((1 << 8) / 5);
            return price;
        }

        /* ZSTD_updateStats() :
         * assumption : literals + litLengtn <= iend */
        private static void ZSTD_updateStats(optState_t* optPtr, uint litLength, byte* literals, uint offsetCode, uint matchLength)
        {
            if ((ZSTD_compressedLiterals(optPtr)) != 0)
            {
                uint u;

                for (u = 0; u < litLength; u++)
                {
                    optPtr->litFreq[literals[u]] += 2;
                }

                optPtr->litSum += litLength * 2;
            }


            {
                uint llCode = ZSTD_LLcode(litLength);

                optPtr->litLengthFreq[llCode]++;
                optPtr->litLengthSum++;
            }


            {
                uint offCode = ZSTD_highbit32(offsetCode + 1);

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
                {
                    return MEM_read32(memPtr);
                }

                case 3:
                {
                    if (BitConverter.IsLittleEndian)
                    {
                        return MEM_read32(memPtr) << 8;
                    }
                    else
                    {
                        return MEM_read32(memPtr) >> 8;
                    }
                }
            }
        }

        /* Update hashTable3 up to ip (excluded)
           Assumption : always within prefix (i.e. not within extDict) */
        private static uint ZSTD_insertAndFindFirstIndexHash3(ZSTD_matchState_t* ms, uint* nextToUpdate3, byte* ip)
        {
            uint* hashTable3 = ms->hashTable3;
            uint hashLog3 = ms->hashLog3;
            byte* @base = ms->window.@base;
            uint idx = *nextToUpdate3;
            uint target = (uint)(ip - @base);
            nuint hash3 = ZSTD_hash3Ptr((void*)ip, hashLog3);

            assert(hashLog3 > 0);
            while (idx < target)
            {
                hashTable3[ZSTD_hash3Ptr((void*)(@base + idx), hashLog3)] = idx;
                idx++;
            }

            *nextToUpdate3 = target;
            return hashTable3[hash3];
        }

        /*-*************************************
        *  Binary Tree search
        ***************************************/
        /** ZSTD_insertBt1() : add one or multiple positions to tree.
         *  ip : assumed <= iend-8 .
         * @return : nb of positions added */
        private static uint ZSTD_insertBt1(ZSTD_matchState_t* ms, byte* ip, byte* iend, uint mls, int extDict)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hashLog = cParams->hashLog;
            nuint h = ZSTD_hashPtr((void*)ip, hashLog, mls);
            uint* bt = ms->chainTable;
            uint btLog = cParams->chainLog - 1;
            uint btMask = (uint)((1 << (int)btLog) - 1);
            uint matchIndex = hashTable[h];
            nuint commonLengthSmaller = 0, commonLengthLarger = 0;
            byte* @base = ms->window.@base;
            byte* dictBase = ms->window.dictBase;
            uint dictLimit = ms->window.dictLimit;
            byte* dictEnd = dictBase + dictLimit;
            byte* prefixStart = @base + dictLimit;
            byte* match;
            uint curr = (uint)(ip - @base);
            uint btLow = (uint)(btMask >= curr ? 0 : curr - btMask);
            uint* smallerPtr = bt + 2 * (curr & btMask);
            uint* largerPtr = smallerPtr + 1;
            uint dummy32;
            uint windowLow = ms->window.lowLimit;
            uint matchEndIdx = curr + 8 + 1;
            nuint bestLength = 8;
            uint nbCompares = 1U << (int)cParams->searchLog;

            assert(ip <= iend - 8);
            hashTable[h] = curr;
            assert(windowLow > 0);
            while (nbCompares-- != 0 && (matchIndex >= windowLow))
            {
                uint* nextPtr = bt + 2 * (matchIndex & btMask);
                nuint matchLength = ((commonLengthSmaller) < (commonLengthLarger) ? (commonLengthSmaller) : (commonLengthLarger));

                assert(matchIndex < curr);
                if (extDict == 0 || (matchIndex + matchLength >= dictLimit))
                {
                    assert(matchIndex + matchLength >= dictLimit);
                    match = @base + matchIndex;
                    matchLength += ZSTD_count(ip + matchLength, match + matchLength, iend);
                }
                else
                {
                    match = dictBase + matchIndex;
                    matchLength += ZSTD_count_2segments(ip + matchLength, match + matchLength, iend, dictEnd, prefixStart);
                    if (matchIndex + matchLength >= dictLimit)
                    {
                        match = @base + matchIndex;
                    }
                }

                if (matchLength > bestLength)
                {
                    bestLength = matchLength;
                    if (matchLength > matchEndIdx - matchIndex)
                    {
                        matchEndIdx = matchIndex + (uint)(matchLength);
                    }
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
                {
                    positions = (uint)((192) < ((uint)(bestLength - 384)) ? (192) : ((uint)(bestLength - 384)));
                }

                assert(matchEndIdx > curr + 8);
                return ((positions) > (matchEndIdx - (curr + 8)) ? (positions) : (matchEndIdx - (curr + 8)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_updateTree_internal(ZSTD_matchState_t* ms, byte* ip, byte* iend, uint mls, ZSTD_dictMode_e dictMode)
        {
            byte* @base = ms->window.@base;
            uint target = (uint)(ip - @base);
            uint idx = ms->nextToUpdate;

            while (idx < target)
            {
                uint forward = ZSTD_insertBt1(ms, @base + idx, iend, mls, ((dictMode == ZSTD_dictMode_e.ZSTD_extDict) ? 1 : 0));

                assert(idx < (uint)(idx + forward));
                idx += forward;
            }

            assert((nuint)(ip - @base) <= (nuint)(unchecked((uint)(-1))));
            assert((nuint)(iend - @base) <= (nuint)(unchecked((uint)(-1))));
            ms->nextToUpdate = target;
        }

        /* used in ZSTD_loadDictionaryContent() */
        public static void ZSTD_updateTree(ZSTD_matchState_t* ms, byte* ip, byte* iend)
        {
            ZSTD_updateTree_internal(ms, ip, iend, ms->cParams.minMatch, ZSTD_dictMode_e.ZSTD_noDict);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_insertBtAndGetAllMatches(ZSTD_match_t* matches, ZSTD_matchState_t* ms, uint* nextToUpdate3, byte* ip, byte* iLimit, ZSTD_dictMode_e dictMode, uint* rep, uint ll0, uint lengthToBeat, uint mls)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint sufficient_len = ((cParams->targetLength) < (uint)(((1 << 12) - 1)) ? (cParams->targetLength) : ((1 << 12) - 1));
            byte* @base = ms->window.@base;
            uint curr = (uint)(ip - @base);
            uint hashLog = cParams->hashLog;
            uint minMatch = (uint)((mls == 3) ? 3 : 4);
            uint* hashTable = ms->hashTable;
            nuint h = ZSTD_hashPtr((void*)ip, hashLog, mls);
            uint matchIndex = hashTable[h];
            uint* bt = ms->chainTable;
            uint btLog = cParams->chainLog - 1;
            uint btMask = (1U << (int)btLog) - 1;
            nuint commonLengthSmaller = 0, commonLengthLarger = 0;
            byte* dictBase = ms->window.dictBase;
            uint dictLimit = ms->window.dictLimit;
            byte* dictEnd = dictBase + dictLimit;
            byte* prefixStart = @base + dictLimit;
            uint btLow = (uint)((btMask >= curr) ? 0 : curr - btMask);
            uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, cParams->windowLog);
            uint matchLow = windowLow != 0 ? windowLow : 1;
            uint* smallerPtr = bt + 2 * (curr & btMask);
            uint* largerPtr = bt + 2 * (curr & btMask) + 1;
            uint matchEndIdx = curr + 8 + 1;
            uint dummy32;
            uint mnum = 0;
            uint nbCompares = 1U << (int)cParams->searchLog;
            ZSTD_matchState_t* dms = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? ms->dictMatchState : null;
            ZSTD_compressionParameters* dmsCParams = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? &dms->cParams : null;
            byte* dmsBase = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.@base : null;
            byte* dmsEnd = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.nextSrc : null;
            uint dmsHighLimit = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? (uint)(dmsEnd - dmsBase) : 0;
            uint dmsLowLimit = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.lowLimit : 0;
            uint dmsIndexDelta = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? windowLow - dmsHighLimit : 0;
            uint dmsHashLog = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dmsCParams->hashLog : hashLog;
            uint dmsBtLog = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dmsCParams->chainLog - 1 : btLog;
            uint dmsBtMask = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? (1U << (int)dmsBtLog) - 1 : 0;
            uint dmsBtLow = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && dmsBtMask < dmsHighLimit - dmsLowLimit ? dmsHighLimit - dmsBtMask : dmsLowLimit;
            nuint bestLength = lengthToBeat - 1;

            assert(ll0 <= 1);

            {
                uint lastR = 3 + ll0;
                uint repCode;

                for (repCode = ll0; repCode < lastR; repCode++)
                {
                    uint repOffset = (repCode == 3) ? (rep[0] - 1) : rep[repCode];
                    uint repIndex = curr - repOffset;
                    uint repLen = 0;

                    assert(curr >= dictLimit);
                    if (repOffset - 1 < curr - dictLimit)
                    {
                        if (((repIndex >= windowLow) && (ZSTD_readMINMATCH((void*)ip, minMatch) == ZSTD_readMINMATCH((void*)(ip - repOffset), minMatch))))
                        {
                            repLen = (uint)(ZSTD_count(ip + minMatch, ip + minMatch - repOffset, iLimit)) + minMatch;
                        }
                    }
                    else
                    {
                        byte* repMatch = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dmsBase + repIndex - dmsIndexDelta : dictBase + repIndex;

                        assert(curr >= windowLow);
                        if (dictMode == ZSTD_dictMode_e.ZSTD_extDict && ((((repOffset - 1) < curr - windowLow) && (((uint)((dictLimit - 1) - repIndex) >= 3)))) && (ZSTD_readMINMATCH((void*)ip, minMatch) == ZSTD_readMINMATCH((void*)repMatch, minMatch)))
                        {
                            repLen = (uint)(ZSTD_count_2segments(ip + minMatch, repMatch + minMatch, iLimit, dictEnd, prefixStart)) + minMatch;
                        }

                        if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && ((((repOffset - 1) < curr - (dmsLowLimit + dmsIndexDelta)) && ((uint)((dictLimit - 1) - repIndex) >= 3))) && (ZSTD_readMINMATCH((void*)ip, minMatch) == ZSTD_readMINMATCH((void*)repMatch, minMatch)))
                        {
                            repLen = (uint)(ZSTD_count_2segments(ip + minMatch, repMatch + minMatch, iLimit, dmsEnd, prefixStart)) + minMatch;
                        }
                    }

                    if (repLen > bestLength)
                    {
                        bestLength = repLen;
                        matches[mnum].off = repCode - ll0;
                        matches[mnum].len = (uint)(repLen);
                        mnum++;
                        if (((repLen > sufficient_len) || (ip + repLen == iLimit)))
                        {
                            return mnum;
                        }
                    }
                }
            }

            if ((mls == 3) && (bestLength < mls))
            {
                uint matchIndex3 = ZSTD_insertAndFindFirstIndexHash3(ms, nextToUpdate3, ip);

                if (((matchIndex3 >= matchLow) && (curr - matchIndex3 < (uint)((1 << 18)))))
                {
                    nuint mlen;

                    if ((dictMode == ZSTD_dictMode_e.ZSTD_noDict) || (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState) || (matchIndex3 >= dictLimit))
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
                        matches[0].off = (curr - matchIndex3) + (uint)((3 - 1));
                        matches[0].len = (uint)(mlen);
                        mnum = 1;
                        if (((mlen > sufficient_len) || (ip + mlen == iLimit)))
                        {
                            ms->nextToUpdate = curr + 1;
                            return 1;
                        }
                    }
                }
            }

            hashTable[h] = curr;
            while (nbCompares-- != 0 && (matchIndex >= matchLow))
            {
                uint* nextPtr = bt + 2 * (matchIndex & btMask);
                byte* match;
                nuint matchLength = ((commonLengthSmaller) < (commonLengthLarger) ? (commonLengthSmaller) : (commonLengthLarger));

                assert(curr > matchIndex);
                if ((dictMode == ZSTD_dictMode_e.ZSTD_noDict) || (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState) || (matchIndex + matchLength >= dictLimit))
                {
                    assert(matchIndex + matchLength >= dictLimit);
                    match = @base + matchIndex;
                    if (matchIndex >= dictLimit)
                    {
                        assert(memcmp((void*)match, (void*)ip, matchLength) == 0);
                    }

                    matchLength += ZSTD_count(ip + matchLength, match + matchLength, iLimit);
                }
                else
                {
                    match = dictBase + matchIndex;
                    assert(memcmp((void*)match, (void*)ip, matchLength) == 0);
                    matchLength += ZSTD_count_2segments(ip + matchLength, match + matchLength, iLimit, dictEnd, prefixStart);
                    if (matchIndex + matchLength >= dictLimit)
                    {
                        match = @base + matchIndex;
                    }
                }

                if (matchLength > bestLength)
                {
                    assert(matchEndIdx > matchIndex);
                    if (matchLength > matchEndIdx - matchIndex)
                    {
                        matchEndIdx = matchIndex + (uint)(matchLength);
                    }

                    bestLength = matchLength;
                    matches[mnum].off = (curr - matchIndex) + (uint)((3 - 1));
                    matches[mnum].len = (uint)(matchLength);
                    mnum++;
                    if (((matchLength > (uint)((1 << 12))) || (ip + matchLength == iLimit)))
                    {
                        if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
                        {
                            nbCompares = 0;
                        }

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
            if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && nbCompares != 0)
            {
                nuint dmsH = ZSTD_hashPtr((void*)ip, dmsHashLog, mls);
                uint dictMatchIndex = dms->hashTable[dmsH];
                uint* dmsBt = dms->chainTable;

                commonLengthSmaller = commonLengthLarger = 0;
                while (nbCompares-- != 0 && (dictMatchIndex > dmsLowLimit))
                {
                    uint* nextPtr = dmsBt + 2 * (dictMatchIndex & dmsBtMask);
                    nuint matchLength = ((commonLengthSmaller) < (commonLengthLarger) ? (commonLengthSmaller) : (commonLengthLarger));
                    byte* match = dmsBase + dictMatchIndex;

                    matchLength += ZSTD_count_2segments(ip + matchLength, match + matchLength, iLimit, dmsEnd, prefixStart);
                    if (dictMatchIndex + matchLength >= dmsHighLimit)
                    {
                        match = @base + dictMatchIndex + dmsIndexDelta;
                    }

                    if (matchLength > bestLength)
                    {
                        matchIndex = dictMatchIndex + dmsIndexDelta;
                        if (matchLength > matchEndIdx - matchIndex)
                        {
                            matchEndIdx = matchIndex + (uint)(matchLength);
                        }

                        bestLength = matchLength;
                        matches[mnum].off = (curr - matchIndex) + (uint)((3 - 1));
                        matches[mnum].len = (uint)(matchLength);
                        mnum++;
                        if (((matchLength > (uint)((1 << 12))) || (ip + matchLength == iLimit)))
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
        private static uint ZSTD_BtGetAllMatches(ZSTD_match_t* matches, ZSTD_matchState_t* ms, uint* nextToUpdate3, byte* ip, byte* iHighLimit, ZSTD_dictMode_e dictMode, uint* rep, uint ll0, uint lengthToBeat)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint matchLengthSearch = cParams->minMatch;

            if (ip < ms->window.@base + ms->nextToUpdate)
            {
                return 0;
            }

            ZSTD_updateTree_internal(ms, ip, iHighLimit, matchLengthSearch, dictMode);
            switch (matchLengthSearch)
            {
                case 3:
                {
                    return ZSTD_insertBtAndGetAllMatches(matches, ms, nextToUpdate3, ip, iHighLimit, dictMode, rep, ll0, lengthToBeat, 3);
                }

                default:
                case 4:
                {
                    return ZSTD_insertBtAndGetAllMatches(matches, ms, nextToUpdate3, ip, iHighLimit, dictMode, rep, ll0, lengthToBeat, 4);
                }

                case 5:
                {
                    return ZSTD_insertBtAndGetAllMatches(matches, ms, nextToUpdate3, ip, iHighLimit, dictMode, rep, ll0, lengthToBeat, 5);
                }

                case 7:
                case 6:
                {
                    return ZSTD_insertBtAndGetAllMatches(matches, ms, nextToUpdate3, ip, iHighLimit, dictMode, rep, ll0, lengthToBeat, 6);
                }
            }
        }

        /* ZSTD_optLdm_skipRawSeqStoreBytes():
         * Moves forward in rawSeqStore by nbBytes, which will update the fields 'pos' and 'posInSequence'.
         */
        private static void ZSTD_optLdm_skipRawSeqStoreBytes(rawSeqStore_t* rawSeqStore, nuint nbBytes)
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
        private static void ZSTD_opt_getNextMatchAndUpdateSeqStore(ZSTD_optLdm_t* optLdm, uint currPosInBlock, uint blockBytesRemaining)
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
            literalsBytesRemaining = (optLdm->seqStore.posInSequence < currSeq.litLength) ? currSeq.litLength - (uint)(optLdm->seqStore.posInSequence) : 0;
            matchBytesRemaining = (literalsBytesRemaining == 0) ? currSeq.matchLength - ((uint)(optLdm->seqStore.posInSequence) - currSeq.litLength) : currSeq.matchLength;
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
                ZSTD_optLdm_skipRawSeqStoreBytes(&optLdm->seqStore, literalsBytesRemaining + matchBytesRemaining);
            }
        }

        /* ZSTD_optLdm_maybeAddMatch():
         * Adds a match if it's long enough, based on it's 'matchStartPosInBlock'
         * and 'matchEndPosInBlock', into 'matches'. Maintains the correct ordering of 'matches'
         */
        private static void ZSTD_optLdm_maybeAddMatch(ZSTD_match_t* matches, uint* nbMatches, ZSTD_optLdm_t* optLdm, uint currPosInBlock)
        {
            uint posDiff = currPosInBlock - optLdm->startPosInBlock;
            uint candidateMatchLength = optLdm->endPosInBlock - optLdm->startPosInBlock - posDiff;
            uint candidateOffCode = optLdm->offset + (uint)((3 - 1));

            if (currPosInBlock < optLdm->startPosInBlock || currPosInBlock >= optLdm->endPosInBlock || candidateMatchLength < 3)
            {
                return;
            }

            if (*nbMatches == 0 || ((candidateMatchLength > matches[*nbMatches - 1].len) && *nbMatches < (uint)((1 << 12))))
            {
                matches[*nbMatches].len = candidateMatchLength;
                matches[*nbMatches].off = candidateOffCode;
                (*nbMatches)++;
            }
        }

        /* ZSTD_optLdm_processMatchCandidate():
         * Wrapper function to update ldm seq store and call ldm functions as necessary.
         */
        private static void ZSTD_optLdm_processMatchCandidate(ZSTD_optLdm_t* optLdm, ZSTD_match_t* matches, uint* nbMatches, uint currPosInBlock, uint remainingBytes)
        {
            if (optLdm->seqStore.size == 0 || optLdm->seqStore.pos >= optLdm->seqStore.size)
            {
                return;
            }

            if (currPosInBlock >= optLdm->endPosInBlock)
            {
                if (currPosInBlock > optLdm->endPosInBlock)
                {
                    uint posOvershoot = currPosInBlock - optLdm->endPosInBlock;

                    ZSTD_optLdm_skipRawSeqStoreBytes(&optLdm->seqStore, posOvershoot);
                }

                ZSTD_opt_getNextMatchAndUpdateSeqStore(optLdm, currPosInBlock, remainingBytes);
            }

            ZSTD_optLdm_maybeAddMatch(matches, nbMatches, optLdm, currPosInBlock);
        }

        /*-*******************************
        *  Optimal parser
        *********************************/
        private static uint ZSTD_totalLen(ZSTD_optimal_t sol)
        {
            return sol.litlen + sol.mlen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_compressBlock_opt_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, int optLevel, ZSTD_dictMode_e dictMode)
        {
            optState_t* optStatePtr = &ms->opt;
            byte* istart = (byte*)(src);
            byte* ip = istart;
            byte* anchor = istart;
            byte* iend = istart + srcSize;
            byte* ilimit = iend - 8;
            byte* @base = ms->window.@base;
            byte* prefixStart = @base + ms->window.dictLimit;
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint sufficient_len = ((cParams->targetLength) < (uint)(((1 << 12) - 1)) ? (cParams->targetLength) : ((1 << 12) - 1));
            uint minMatch = (uint)((cParams->minMatch == 3) ? 3 : 4);
            uint nextToUpdate3 = ms->nextToUpdate;
            ZSTD_optimal_t* opt = optStatePtr->priceTable;
            ZSTD_match_t* matches = optStatePtr->matchTable;
            ZSTD_optimal_t lastSequence;
            var _ = &lastSequence;
            ZSTD_optLdm_t optLdm;

            optLdm.seqStore = ms->ldmSeqStore != null ? *ms->ldmSeqStore : kNullRawSeqStore;
            optLdm.endPosInBlock = optLdm.startPosInBlock = optLdm.offset = 0;
            ZSTD_opt_getNextMatchAndUpdateSeqStore(&optLdm, (uint)(ip - istart), (uint)(iend - ip));
            assert(optLevel <= 2);
            ZSTD_rescaleFreqs(optStatePtr, (byte*)(src), srcSize, optLevel);
            ip += ((ip == prefixStart) ? 1 : 0);
            while (ip < ilimit)
            {
                uint cur, last_pos = 0;


                {
                    uint litlen = (uint)(ip - anchor);
                    uint ll0 = (litlen == 0 ? 1U : 0U);
                    uint nbMatches = ZSTD_BtGetAllMatches(matches, ms, &nextToUpdate3, ip, iend, dictMode, rep, ll0, minMatch);

                    ZSTD_optLdm_processMatchCandidate(&optLdm, matches, &nbMatches, (uint)(ip - istart), (uint)(iend - ip));
                    if (nbMatches == 0)
                    {
                        ip++;
                        continue;
                    }


                    {
                        uint i;

                        for (i = 0; i < 3; i++)
                        {
                            opt[0].rep[i] = rep[i];
                        }
                    }

                    opt[0].mlen = 0;
                    opt[0].litlen = litlen;
                    opt[0].price = (int)(ZSTD_litLengthPrice(litlen, optStatePtr, optLevel));

                    {
                        uint maxML = matches[nbMatches - 1].len;
                        uint maxOffset = matches[nbMatches - 1].off;

                        if (maxML > sufficient_len)
                        {
                            lastSequence.litlen = litlen;
                            lastSequence.mlen = maxML;
                            lastSequence.off = maxOffset;
                            cur = 0;
                            last_pos = ZSTD_totalLen(lastSequence);
                            goto _shortestPath;
                        }
                    }


                    {
                        uint literalsPrice = (uint)(opt[0].price) + ZSTD_litLengthPrice(0, optStatePtr, optLevel);
                        uint pos;
                        uint matchNb;

                        for (pos = 1; pos < minMatch; pos++)
                        {
                            opt[pos].price = (1 << 30);
                        }

                        for (matchNb = 0; matchNb < nbMatches; matchNb++)
                        {
                            uint offset = matches[matchNb].off;
                            uint end = matches[matchNb].len;

                            for (; pos <= end; pos++)
                            {
                                uint matchPrice = ZSTD_getMatchPrice(offset, pos, optStatePtr, optLevel);
                                uint sequencePrice = literalsPrice + matchPrice;

                                opt[pos].mlen = pos;
                                opt[pos].off = offset;
                                opt[pos].litlen = litlen;
                                opt[pos].price = (int)sequencePrice;
                            }
                        }

                        last_pos = pos - 1;
                    }
                }

                for (cur = 1; cur <= last_pos; cur++)
                {
                    byte* inr = ip + cur;

                    assert(cur < (uint)((1 << 12)));

                    {
                        uint litlen = (opt[cur - 1].mlen == 0) ? opt[cur - 1].litlen + 1 : 1;
                        int price = (int)((uint)(opt[cur - 1].price) + ZSTD_rawLiteralsCost(ip + cur - 1, 1, optStatePtr, optLevel) + ZSTD_litLengthPrice(litlen, optStatePtr, optLevel) - ZSTD_litLengthPrice(litlen - 1, optStatePtr, optLevel));

                        assert(price < 1000000000);
                        if (price <= opt[cur].price)
                        {
                            opt[cur].mlen = 0;
                            opt[cur].off = 0;
                            opt[cur].litlen = litlen;
                            opt[cur].price = price;
                        }
                        else
                        {
                        }
                    }

                    assert(cur >= opt[cur].mlen);
                    if (opt[cur].mlen != 0)
                    {
                        uint prev = cur - opt[cur].mlen;
                        repcodes_s newReps = ZSTD_updateRep(opt[prev].rep, opt[cur].off, ((opt[cur].litlen == 0) ? 1U : 0U));

                        memcpy((void*)((opt[cur].rep)), (void*)(&newReps), ((nuint)(sizeof(repcodes_s))));
                    }
                    else
                    {
                        memcpy((void*)((opt[cur].rep)), (void*)((opt[cur - 1].rep)), ((nuint)(sizeof(repcodes_s))));
                    }

                    if (inr > ilimit)
                    {
                        continue;
                    }

                    if (cur == last_pos)
                    {
                        break;
                    }

                    if ((optLevel == 0) && (opt[cur + 1].price <= opt[cur].price + ((1 << 8) / 2)))
                    {
                        continue;
                    }


                    {
                        uint ll0 = (((opt[cur].mlen != 0)) ? 1U : 0U);
                        uint litlen = (opt[cur].mlen == 0) ? opt[cur].litlen : 0;
                        uint previousPrice = (uint)(opt[cur].price);
                        uint basePrice = previousPrice + ZSTD_litLengthPrice(0, optStatePtr, optLevel);
                        uint nbMatches = ZSTD_BtGetAllMatches(matches, ms, &nextToUpdate3, inr, iend, dictMode, opt[cur].rep, ll0, minMatch);
                        uint matchNb;

                        ZSTD_optLdm_processMatchCandidate(&optLdm, matches, &nbMatches, (uint)(inr - istart), (uint)(iend - inr));
                        if (nbMatches == 0)
                        {
                            continue;
                        }


                        {
                            uint maxML = matches[nbMatches - 1].len;

                            if ((maxML > sufficient_len) || (cur + maxML >= (uint)((1 << 12))))
                            {
                                lastSequence.mlen = maxML;
                                lastSequence.off = matches[nbMatches - 1].off;
                                lastSequence.litlen = litlen;
                                cur -= (opt[cur].mlen == 0) ? opt[cur].litlen : 0;
                                last_pos = cur + ZSTD_totalLen(lastSequence);
                                if (cur > (uint)((1 << 12)))
                                {
                                    cur = 0;
                                }

                                goto _shortestPath;
                            }
                        }

                        for (matchNb = 0; matchNb < nbMatches; matchNb++)
                        {
                            uint offset = matches[matchNb].off;
                            uint lastML = matches[matchNb].len;
                            uint startML = (matchNb > 0) ? matches[matchNb - 1].len + 1 : minMatch;
                            uint mlen;

                            for (mlen = lastML; mlen >= startML; mlen--)
                            {
                                uint pos = cur + mlen;
                                int price = (int)(basePrice + ZSTD_getMatchPrice(offset, mlen, optStatePtr, optLevel));

                                if ((pos > last_pos) || (price < opt[pos].price))
                                {
                                    while (last_pos < pos)
                                    {
                                        opt[last_pos + 1].price = (1 << 30);
                                        last_pos++;
                                    }

                                    opt[pos].mlen = mlen;
                                    opt[pos].off = offset;
                                    opt[pos].litlen = litlen;
                                    opt[pos].price = price;
                                }
                                else
                                {
                                    if (optLevel == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                lastSequence = opt[last_pos];
                cur = last_pos > ZSTD_totalLen(lastSequence) ? last_pos - ZSTD_totalLen(lastSequence) : 0;
                assert(cur < (uint)((1 << 12)));
                _shortestPath:
                assert(opt[0].mlen == 0);
                if (lastSequence.mlen != 0)
                {
                    repcodes_s reps = ZSTD_updateRep(opt[cur].rep, lastSequence.off, ((lastSequence.litlen == 0) ? 1U : 0U));

                    memcpy((void*)(rep), (void*)(&reps), ((nuint)(sizeof(repcodes_s))));
                }
                else
                {
                    memcpy((void*)(rep), (void*)((opt[cur].rep)), ((nuint)(sizeof(repcodes_s))));
                }


                {
                    uint storeEnd = cur + 1;
                    uint storeStart = storeEnd;
                    uint seqPos = cur;

                    assert(storeEnd < (uint)((1 << 12)));
                    opt[storeEnd] = lastSequence;
                    while (seqPos > 0)
                    {
                        uint backDist = ZSTD_totalLen(opt[seqPos]);

                        storeStart--;
                        opt[storeStart] = opt[seqPos];
                        seqPos = (seqPos > backDist) ? seqPos - backDist : 0;
                    }


                    {
                        uint storePos;

                        for (storePos = storeStart; storePos <= storeEnd; storePos++)
                        {
                            uint llen = opt[storePos].litlen;
                            uint mlen = opt[storePos].mlen;
                            uint offCode = opt[storePos].off;
                            uint advance = llen + mlen;

                            if (mlen == 0)
                            {
                                assert(storePos == storeEnd);
                                ip = anchor + llen;
                                continue;
                            }

                            assert(anchor + llen <= iend);
                            ZSTD_updateStats(optStatePtr, llen, anchor, offCode, mlen);
                            ZSTD_storeSeq(seqStore, llen, anchor, iend, offCode, mlen - 3);
                            anchor += advance;
                            ip = anchor;
                        }
                    }

                    ZSTD_setBasePrices(optStatePtr, optLevel);
                }
            }

            return (nuint)(iend - anchor);
        }

        public static nuint ZSTD_compressBlock_btopt(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 0, ZSTD_dictMode_e.ZSTD_noDict);
        }

        /* used in 2-pass strategy */
        private static uint ZSTD_upscaleStat(uint* table, uint lastEltIndex, int bonus)
        {
            uint s, sum = 0;

            assert(4 + bonus >= 0);
            for (s = 0; s < lastEltIndex + 1; s++)
            {
                table[s] <<= 4 + bonus;
                table[s]--;
                sum += table[s];
            }

            return sum;
        }

        /* used in 2-pass strategy */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_upscaleStats(optState_t* optPtr)
        {
            if ((ZSTD_compressedLiterals(optPtr)) != 0)
            {
                optPtr->litSum = ZSTD_upscaleStat(optPtr->litFreq, (uint)(((1 << 8) - 1)), 0);
            }

            optPtr->litLengthSum = ZSTD_upscaleStat(optPtr->litLengthFreq, 35, 0);
            optPtr->matchLengthSum = ZSTD_upscaleStat(optPtr->matchLengthFreq, 52, 0);
            optPtr->offCodeSum = ZSTD_upscaleStat(optPtr->offCodeFreq, 31, 0);
        }

        /* ZSTD_initStats_ultra():
         * make a first compression pass, just to seed stats with more accurate starting values.
         * only works on first block, with no dictionary and no ldm.
         * this function cannot error, hence its contract must be respected.
         */
        private static void ZSTD_initStats_ultra(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint* tmpRep = stackalloc uint[3];

            memcpy((void*)(tmpRep), (void*)(rep), ((nuint)(sizeof(uint) * 3)));
            assert(ms->opt.litLengthSum == 0);
            assert(seqStore->sequences == seqStore->sequencesStart);
            assert(ms->window.dictLimit == ms->window.lowLimit);
            assert(ms->window.dictLimit - ms->nextToUpdate <= 1);
            ZSTD_compressBlock_opt_generic(ms, seqStore, tmpRep, src, srcSize, 2, ZSTD_dictMode_e.ZSTD_noDict);
            ZSTD_resetSeqStore(seqStore);
            ms->window.@base -= srcSize;
            ms->window.dictLimit += (uint)(srcSize);
            ms->window.lowLimit = ms->window.dictLimit;
            ms->nextToUpdate = ms->window.dictLimit;
            ZSTD_upscaleStats(&ms->opt);
        }

        public static nuint ZSTD_compressBlock_btultra(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 2, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_btultra2(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint curr = (uint)((byte*)(src) - ms->window.@base);

            assert(srcSize <= (uint)((1 << 17)));
            if ((ms->opt.litLengthSum == 0) && (seqStore->sequences == seqStore->sequencesStart) && (ms->window.dictLimit == ms->window.lowLimit) && (curr == ms->window.dictLimit) && (srcSize > 1024))
            {
                ZSTD_initStats_ultra(ms, seqStore, rep, src, srcSize);
            }

            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 2, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_btopt_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 0, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_btultra_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 2, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_btopt_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 0, ZSTD_dictMode_e.ZSTD_extDict);
        }

        public static nuint ZSTD_compressBlock_btultra_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_opt_generic(ms, seqStore, rep, src, srcSize, 2, ZSTD_dictMode_e.ZSTD_extDict);
        }
    }
}
