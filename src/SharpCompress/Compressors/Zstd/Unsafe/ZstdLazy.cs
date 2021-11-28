using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
#endif
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /*-*************************************
        *  Binary Tree search
        ***************************************/
        [InlineMethod.Inline]
        private static void ZSTD_updateDUBT(ZSTD_matchState_t* ms, byte* ip, byte* iend, uint mls)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hashLog = cParams->hashLog;
            uint* bt = ms->chainTable;
            uint btLog = cParams->chainLog - 1;
            uint btMask = (uint)((1 << (int)btLog) - 1);
            byte* @base = ms->window.@base;
            uint target = (uint)(ip - @base);
            uint idx = ms->nextToUpdate;

            if (idx != target)
            {
        ;
            }

            assert(ip + 8 <= iend);
            assert(idx >= ms->window.dictLimit);
            for (; idx < target; idx++)
            {
                nuint h = ZSTD_hashPtr((void*)(@base + idx), hashLog, mls);
                uint matchIndex = hashTable[h];
                uint* nextCandidatePtr = bt + 2 * (idx & btMask);
                uint* sortMarkPtr = nextCandidatePtr + 1;

                hashTable[h] = idx;
                *nextCandidatePtr = matchIndex;
                *sortMarkPtr = 1;
            }

            ms->nextToUpdate = target;
        }

        /** ZSTD_insertDUBT1() :
         *  sort one already inserted but unsorted position
         *  assumption : curr >= btlow == (curr - btmask)
         *  doesn't fail */
        [InlineMethod.Inline]
        private static void ZSTD_insertDUBT1(ZSTD_matchState_t* ms, uint curr, byte* inputEnd, uint nbCompares, uint btLow, ZSTD_dictMode_e dictMode)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* bt = ms->chainTable;
            uint btLog = cParams->chainLog - 1;
            uint btMask = (uint)((1 << (int)btLog) - 1);
            nuint commonLengthSmaller = 0, commonLengthLarger = 0;
            byte* @base = ms->window.@base;
            byte* dictBase = ms->window.dictBase;
            uint dictLimit = ms->window.dictLimit;
            byte* ip = (curr >= dictLimit) ? @base + curr : dictBase + curr;
            byte* iend = (curr >= dictLimit) ? inputEnd : dictBase + dictLimit;
            byte* dictEnd = dictBase + dictLimit;
            byte* prefixStart = @base + dictLimit;
            byte* match;
            uint* smallerPtr = bt + 2 * (curr & btMask);
            uint* largerPtr = smallerPtr + 1;
            uint matchIndex = *smallerPtr;
            uint dummy32;
            uint windowValid = ms->window.lowLimit;
            uint maxDistance = 1U << (int)cParams->windowLog;
            uint windowLow = (curr - windowValid > maxDistance) ? curr - maxDistance : windowValid;

            assert(curr >= btLow);
            assert(ip < iend);
            while (nbCompares-- != 0 && (matchIndex > windowLow))
            {
                uint* nextPtr = bt + 2 * (matchIndex & btMask);
                nuint matchLength = ((commonLengthSmaller) < (commonLengthLarger) ? (commonLengthSmaller) : (commonLengthLarger));

                assert(matchIndex < curr);
                if ((dictMode != ZSTD_dictMode_e.ZSTD_extDict) || (matchIndex + matchLength >= dictLimit) || (curr < dictLimit))
                {
                    byte* mBase = ((dictMode != ZSTD_dictMode_e.ZSTD_extDict) || (matchIndex + matchLength >= dictLimit)) ? @base : dictBase;

                    assert((matchIndex + matchLength >= dictLimit) || (curr < dictLimit));
                    match = mBase + matchIndex;
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
        }

        private static nuint ZSTD_DUBT_findBetterDictMatch(ZSTD_matchState_t* ms, byte* ip, byte* iend, nuint* offsetPtr, nuint bestLength, uint nbCompares, uint mls, ZSTD_dictMode_e dictMode)
        {
            ZSTD_matchState_t* dms = ms->dictMatchState;
            ZSTD_compressionParameters* dmsCParams = &dms->cParams;
            uint* dictHashTable = dms->hashTable;
            uint hashLog = dmsCParams->hashLog;
            nuint h = ZSTD_hashPtr((void*)ip, hashLog, mls);
            uint dictMatchIndex = dictHashTable[h];
            byte* @base = ms->window.@base;
            byte* prefixStart = @base + ms->window.dictLimit;
            uint curr = (uint)(ip - @base);
            byte* dictBase = dms->window.@base;
            byte* dictEnd = dms->window.nextSrc;
            uint dictHighLimit = (uint)(dms->window.nextSrc - dms->window.@base);
            uint dictLowLimit = dms->window.lowLimit;
            uint dictIndexDelta = ms->window.lowLimit - dictHighLimit;
            uint* dictBt = dms->chainTable;
            uint btLog = dmsCParams->chainLog - 1;
            uint btMask = (uint)((1 << (int)btLog) - 1);
            uint btLow = (btMask >= dictHighLimit - dictLowLimit) ? dictLowLimit : dictHighLimit - btMask;
            nuint commonLengthSmaller = 0, commonLengthLarger = 0;

            assert(dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState);
            while (nbCompares-- != 0 && (dictMatchIndex > dictLowLimit))
            {
                uint* nextPtr = dictBt + 2 * (dictMatchIndex & btMask);
                nuint matchLength = ((commonLengthSmaller) < (commonLengthLarger) ? (commonLengthSmaller) : (commonLengthLarger));
                byte* match = dictBase + dictMatchIndex;

                matchLength += ZSTD_count_2segments(ip + matchLength, match + matchLength, iend, dictEnd, prefixStart);
                if (dictMatchIndex + matchLength >= dictHighLimit)
                {
                    match = @base + dictMatchIndex + dictIndexDelta;
                }

                if (matchLength > bestLength)
                {
                    uint matchIndex = dictMatchIndex + dictIndexDelta;

                    if ((4 * (int)(matchLength - bestLength)) > (int)(ZSTD_highbit32(curr - matchIndex + 1) - ZSTD_highbit32((uint)(offsetPtr[0]) + 1)))
                    {
                        bestLength = matchLength; *offsetPtr = (uint)((3 - 1)) + curr - matchIndex;
                    }

                    if (ip + matchLength == iend)
                    {
                        break;
                    }
                }

                if (match[matchLength] < ip[matchLength])
                {
                    if (dictMatchIndex <= btLow)
                    {
                        break;
                    }

                    commonLengthSmaller = matchLength;
                    dictMatchIndex = nextPtr[1];
                }
                else
                {
                    if (dictMatchIndex <= btLow)
                    {
                        break;
                    }

                    commonLengthLarger = matchLength;
                    dictMatchIndex = nextPtr[0];
                }
            }

            if (bestLength >= 3)
            {
                uint mIndex = curr - ((uint)(*offsetPtr) - (uint)((3 - 1)));
            }

            return bestLength;
        }

        [InlineMethod.Inline]
        private static nuint ZSTD_DUBT_findBestMatch(ZSTD_matchState_t* ms, byte* ip, byte* iend, nuint* offsetPtr, uint mls, ZSTD_dictMode_e dictMode)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hashLog = cParams->hashLog;
            nuint h = ZSTD_hashPtr((void*)ip, hashLog, mls);
            uint matchIndex = hashTable[h];
            byte* @base = ms->window.@base;
            uint curr = (uint)(ip - @base);
            uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, cParams->windowLog);
            uint* bt = ms->chainTable;
            uint btLog = cParams->chainLog - 1;
            uint btMask = (uint)((1 << (int)btLog) - 1);
            uint btLow = (uint)((btMask >= curr) ? 0 : curr - btMask);
            uint unsortLimit = ((btLow) > (windowLow) ? (btLow) : (windowLow));
            uint* nextCandidate = bt + 2 * (matchIndex & btMask);
            uint* unsortedMark = bt + 2 * (matchIndex & btMask) + 1;
            uint nbCompares = 1U << (int)cParams->searchLog;
            uint nbCandidates = nbCompares;
            uint previousCandidate = 0;

            assert(ip <= iend - 8);
            assert(dictMode != ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
            while ((matchIndex > unsortLimit) && (*unsortedMark == 1) && (nbCandidates > 1))
            {
                *unsortedMark = previousCandidate;
                previousCandidate = matchIndex;
                matchIndex = *nextCandidate;
                nextCandidate = bt + 2 * (matchIndex & btMask);
                unsortedMark = bt + 2 * (matchIndex & btMask) + 1;
                nbCandidates--;
            }

            if ((matchIndex > unsortLimit) && (*unsortedMark == 1))
            {
                *nextCandidate = *unsortedMark = 0;
            }

            matchIndex = previousCandidate;
            while (matchIndex != 0)
            {
                uint* nextCandidateIdxPtr = bt + 2 * (matchIndex & btMask) + 1;
                uint nextCandidateIdx = *nextCandidateIdxPtr;

                ZSTD_insertDUBT1(ms, matchIndex, iend, nbCandidates, unsortLimit, dictMode);
                matchIndex = nextCandidateIdx;
                nbCandidates++;
            }


            {
                nuint commonLengthSmaller = 0, commonLengthLarger = 0;
                byte* dictBase = ms->window.dictBase;
                uint dictLimit = ms->window.dictLimit;
                byte* dictEnd = dictBase + dictLimit;
                byte* prefixStart = @base + dictLimit;
                uint* smallerPtr = bt + 2 * (curr & btMask);
                uint* largerPtr = bt + 2 * (curr & btMask) + 1;
                uint matchEndIdx = curr + 8 + 1;
                uint dummy32;
                nuint bestLength = 0;

                matchIndex = hashTable[h];
                hashTable[h] = curr;
                while (nbCompares-- != 0 && (matchIndex > windowLow))
                {
                    uint* nextPtr = bt + 2 * (matchIndex & btMask);
                    nuint matchLength = ((commonLengthSmaller) < (commonLengthLarger) ? (commonLengthSmaller) : (commonLengthLarger));
                    byte* match;

                    if ((dictMode != ZSTD_dictMode_e.ZSTD_extDict) || (matchIndex + matchLength >= dictLimit))
                    {
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
                        if (matchLength > matchEndIdx - matchIndex)
                        {
                            matchEndIdx = matchIndex + (uint)(matchLength);
                        }

                        if ((4 * (int)(matchLength - bestLength)) > (int)(ZSTD_highbit32(curr - matchIndex + 1) - ZSTD_highbit32((uint)(offsetPtr[0]) + 1)))
                        {
                            bestLength = matchLength; *offsetPtr = (uint)((3 - 1)) + curr - matchIndex;
                        }

                        if (ip + matchLength == iend)
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
                    bestLength = ZSTD_DUBT_findBetterDictMatch(ms, ip, iend, offsetPtr, bestLength, nbCompares, mls, dictMode);
                }

                assert(matchEndIdx > curr + 8);
                ms->nextToUpdate = matchEndIdx - 8;
                if (bestLength >= 3)
                {
                    uint mIndex = curr - ((uint)(*offsetPtr) - (uint)((3 - 1)));
                }

                return bestLength;
            }
        }

        /** ZSTD_BtFindBestMatch() : Tree updater, providing best match */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint ZSTD_BtFindBestMatch(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr, uint mls, ZSTD_dictMode_e dictMode)
        {
            if (ip < ms->window.@base + ms->nextToUpdate)
            {
                return 0;
            }

            ZSTD_updateDUBT(ms, ip, iLimit, mls);
            return ZSTD_DUBT_findBestMatch(ms, ip, iLimit, offsetPtr, mls, dictMode);
        }

        private static nuint ZSTD_BtFindBestMatch_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_BtFindBestMatch(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_noDict);
        }

        private static nuint ZSTD_BtFindBestMatch_dictMatchState_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_BtFindBestMatch(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        private static nuint ZSTD_BtFindBestMatch_extDict_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_BtFindBestMatch(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_extDict);
        }

        /***********************************
        * Dedicated dict search
        ***********************************/
        public static void ZSTD_dedicatedDictSearch_lazy_loadDictionary(ZSTD_matchState_t* ms, byte* ip)
        {
            byte* @base = ms->window.@base;
            uint target = (uint)(ip - @base);
            uint* hashTable = ms->hashTable;
            uint* chainTable = ms->chainTable;
            uint chainSize = (uint)(1 << (int)ms->cParams.chainLog);
            uint idx = ms->nextToUpdate;
            uint minChain = chainSize < target ? target - chainSize : idx;
            uint bucketSize = (uint)(1 << 2);
            uint cacheSize = bucketSize - 1;
            uint chainAttempts = (uint)((1 << (int)ms->cParams.searchLog)) - cacheSize;
            uint chainLimit = (uint)(chainAttempts > 255 ? 255 : chainAttempts);
            uint hashLog = ms->cParams.hashLog - 2;
            uint* tmpHashTable = hashTable;
            uint* tmpChainTable = hashTable + ((nuint)(1) << (int)hashLog);
            uint tmpChainSize = (uint)(((1 << 2) - 1) << (int)hashLog);
            uint tmpMinChain = tmpChainSize < target ? target - tmpChainSize : idx;
            uint hashIdx;

            assert(ms->cParams.chainLog <= 24);
            assert(ms->cParams.hashLog > ms->cParams.chainLog);
            assert(idx != 0);
            assert(tmpMinChain <= minChain);
            for (; idx < target; idx++)
            {
                uint h = (uint)(ZSTD_hashPtr((void*)(@base + idx), hashLog, ms->cParams.minMatch));

                if (idx >= tmpMinChain)
                {
                    tmpChainTable[idx - tmpMinChain] = hashTable[h];
                }

                tmpHashTable[h] = idx;
            }


            {
                uint chainPos = 0;

                for (hashIdx = 0; hashIdx < (1U << (int)hashLog); hashIdx++)
                {
                    uint count;
                    uint countBeyondMinChain = 0;
                    uint i = tmpHashTable[hashIdx];

                    for (count = 0; i >= tmpMinChain && count < cacheSize; count++)
                    {
                        if (i < minChain)
                        {
                            countBeyondMinChain++;
                        }

                        i = tmpChainTable[i - tmpMinChain];
                    }

                    if (count == cacheSize)
                    {
                        for (count = 0; count < chainLimit;)
                        {
                            if (i < minChain)
                            {
                                if (i == 0 || ++countBeyondMinChain > cacheSize)
                                {
                                    break;
                                }
                            }

                            chainTable[chainPos++] = i;
                            count++;
                            if (i < tmpMinChain)
                            {
                                break;
                            }

                            i = tmpChainTable[i - tmpMinChain];
                        }
                    }
                    else
                    {
                        count = 0;
                    }

                    if (count != 0)
                    {
                        tmpHashTable[hashIdx] = ((chainPos - count) << 8) + count;
                    }
                    else
                    {
                        tmpHashTable[hashIdx] = 0;
                    }
                }

                assert(chainPos <= chainSize);
            }

            for (hashIdx = (uint)((1 << (int)hashLog)); hashIdx != 0;)
            {
                uint bucketIdx = --hashIdx << 2;
                uint chainPackedPointer = tmpHashTable[hashIdx];
                uint i;

                for (i = 0; i < cacheSize; i++)
                {
                    hashTable[bucketIdx + i] = 0;
                }

                hashTable[bucketIdx + bucketSize - 1] = chainPackedPointer;
            }

            for (idx = ms->nextToUpdate; idx < target; idx++)
            {
                uint h = (uint)(ZSTD_hashPtr((void*)(@base + idx), hashLog, ms->cParams.minMatch)) << 2;
                uint i;

                for (i = cacheSize - 1; i != 0; i--)
                {
                    hashTable[h + i] = hashTable[h + i - 1];
                }

                hashTable[h] = idx;
            }

            ms->nextToUpdate = target;
        }

        /* Returns the longest match length found in the dedicated dict search structure.
         * If none are longer than the argument ml, then ml will be returned.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_dedicatedDictSearch_lazy_search(nuint* offsetPtr, nuint ml, uint nbAttempts, ZSTD_matchState_t* dms, byte* ip, byte* iLimit, byte* prefixStart, uint curr, uint dictLimit, nuint ddsIdx)
        {
            uint ddsLowestIndex = dms->window.dictLimit;
            byte* ddsBase = dms->window.@base;
            byte* ddsEnd = dms->window.nextSrc;
            uint ddsSize = (uint)(ddsEnd - ddsBase);
            uint ddsIndexDelta = dictLimit - ddsSize;
            uint bucketSize = (uint)((1 << 2));
            uint bucketLimit = nbAttempts < bucketSize - 1 ? nbAttempts : bucketSize - 1;
            uint ddsAttempt;
            uint matchIndex;

            for (ddsAttempt = 0; ddsAttempt < bucketSize - 1; ddsAttempt++)
            {
                Prefetch0((void*)(ddsBase + dms->hashTable[ddsIdx + ddsAttempt]));
            }


            {
                uint chainPackedPointer = dms->hashTable[ddsIdx + bucketSize - 1];
                uint chainIndex = chainPackedPointer >> 8;

                Prefetch0((void*)(&dms->chainTable[chainIndex]));
            }

            for (ddsAttempt = 0; ddsAttempt < bucketLimit; ddsAttempt++)
            {
                nuint currentMl = 0;
                byte* match;

                matchIndex = dms->hashTable[ddsIdx + ddsAttempt];
                match = ddsBase + matchIndex;
                if (matchIndex == 0)
                {
                    return ml;
                }

                assert(matchIndex >= ddsLowestIndex);
                assert(match + 4 <= ddsEnd);
                if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                {
                    currentMl = ZSTD_count_2segments(ip + 4, match + 4, iLimit, ddsEnd, prefixStart) + 4;
                }

                if (currentMl > ml)
                {
                    ml = currentMl;
                    *offsetPtr = curr - (matchIndex + ddsIndexDelta) + (uint)((3 - 1));
                    if (ip + currentMl == iLimit)
                    {
                        return ml;
                    }
                }
            }


            {
                uint chainPackedPointer = dms->hashTable[ddsIdx + bucketSize - 1];
                uint chainIndex = chainPackedPointer >> 8;
                uint chainLength = chainPackedPointer & 0xFF;
                uint chainAttempts = nbAttempts - ddsAttempt;
                uint chainLimit = chainAttempts > chainLength ? chainLength : chainAttempts;
                uint chainAttempt;

                for (chainAttempt = 0; chainAttempt < chainLimit; chainAttempt++)
                {
                    Prefetch0((void*)(ddsBase + dms->chainTable[chainIndex + chainAttempt]));
                }

                for (chainAttempt = 0; chainAttempt < chainLimit; chainAttempt++ , chainIndex++)
                {
                    nuint currentMl = 0;
                    byte* match;

                    matchIndex = dms->chainTable[chainIndex];
                    match = ddsBase + matchIndex;
                    assert(matchIndex >= ddsLowestIndex);
                    assert(match + 4 <= ddsEnd);
                    if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                    {
                        currentMl = ZSTD_count_2segments(ip + 4, match + 4, iLimit, ddsEnd, prefixStart) + 4;
                    }

                    if (currentMl > ml)
                    {
                        ml = currentMl;
                        *offsetPtr = curr - (matchIndex + ddsIndexDelta) + (uint)((3 - 1));
                        if (ip + currentMl == iLimit)
                        {
                            break;
                        }
                    }
                }
            }

            return ml;
        }

        /* Update chains up to ip (excluded)
           Assumption : always within prefix (i.e. not within extDict) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ZSTD_insertAndFindFirstIndex_internal(ZSTD_matchState_t* ms, ZSTD_compressionParameters* cParams, byte* ip, uint mls)
        {
            uint* hashTable = ms->hashTable;
            uint hashLog = cParams->hashLog;
            uint* chainTable = ms->chainTable;
            uint chainMask = (uint)((1 << (int)cParams->chainLog) - 1);
            byte* @base = ms->window.@base;
            uint target = (uint)(ip - @base);
            uint idx = ms->nextToUpdate;

            while (idx < target)
            {
                nuint h = ZSTD_hashPtr((void*)(@base + idx), hashLog, mls);

                chainTable[(idx) & (chainMask)] = hashTable[h];
                hashTable[h] = idx;
                idx++;
            }

            ms->nextToUpdate = target;
            return hashTable[ZSTD_hashPtr((void*)ip, hashLog, mls)];
        }

        public static uint ZSTD_insertAndFindFirstIndex(ZSTD_matchState_t* ms, byte* ip)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;

            return ZSTD_insertAndFindFirstIndex_internal(ms, cParams, ip, ms->cParams.minMatch);
        }

        /* inlining is important to hardwire a hot branch (template emulation) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_HcFindBestMatch_generic(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr, uint mls, ZSTD_dictMode_e dictMode)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* chainTable = ms->chainTable;
            uint chainSize = (uint)((1 << (int)cParams->chainLog));
            uint chainMask = chainSize - 1;
            byte* @base = ms->window.@base;
            byte* dictBase = ms->window.dictBase;
            uint dictLimit = ms->window.dictLimit;
            byte* prefixStart = @base + dictLimit;
            byte* dictEnd = dictBase + dictLimit;
            uint curr = (uint)(ip - @base);
            uint maxDistance = 1U << (int)cParams->windowLog;
            uint lowestValid = ms->window.lowLimit;
            uint withinMaxDistance = (curr - lowestValid > maxDistance) ? curr - maxDistance : lowestValid;
            uint isDictionary = (((ms->loadedDictEnd != 0)) ? 1U : 0U);
            uint lowLimit = isDictionary != 0 ? lowestValid : withinMaxDistance;
            uint minChain = curr > chainSize ? curr - chainSize : 0;
            uint nbAttempts = 1U << (int)cParams->searchLog;
            nuint ml = (nuint)(4 - 1);
            ZSTD_matchState_t* dms = ms->dictMatchState;
            uint ddsHashLog = dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch ? dms->cParams.hashLog - 2 : 0;
            nuint ddsIdx = dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch ? ZSTD_hashPtr((void*)ip, ddsHashLog, mls) << 2 : 0;
            uint matchIndex;

            if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
            {
                uint* entry = &dms->hashTable[ddsIdx];

                Prefetch0((void*)entry);
            }

            matchIndex = ZSTD_insertAndFindFirstIndex_internal(ms, cParams, ip, mls);
            for (; ((matchIndex >= lowLimit) && (nbAttempts > 0)); nbAttempts--)
            {
                nuint currentMl = 0;

                if ((dictMode != ZSTD_dictMode_e.ZSTD_extDict) || matchIndex >= dictLimit)
                {
                    byte* match = @base + matchIndex;

                    assert(matchIndex >= dictLimit);
                    if (match[ml] == ip[ml])
                    {
                        currentMl = ZSTD_count(ip, match, iLimit);
                    }
                }
                else
                {
                    byte* match = dictBase + matchIndex;

                    assert(match + 4 <= dictEnd);
                    if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                    {
                        currentMl = ZSTD_count_2segments(ip + 4, match + 4, iLimit, dictEnd, prefixStart) + 4;
                    }
                }

                if (currentMl > ml)
                {
                    ml = currentMl;
                    *offsetPtr = curr - matchIndex + (uint)((3 - 1));
                    if (ip + currentMl == iLimit)
                    {
                        break;
                    }
                }

                if (matchIndex <= minChain)
                {
                    break;
                }

                matchIndex = chainTable[(matchIndex) & (chainMask)];
            }

            if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
            {
                ml = ZSTD_dedicatedDictSearch_lazy_search(offsetPtr, ml, nbAttempts, dms, ip, iLimit, prefixStart, curr, dictLimit, ddsIdx);
            }
            else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
            {
                uint* dmsChainTable = dms->chainTable;
                uint dmsChainSize = (uint)((1 << (int)dms->cParams.chainLog));
                uint dmsChainMask = dmsChainSize - 1;
                uint dmsLowestIndex = dms->window.dictLimit;
                byte* dmsBase = dms->window.@base;
                byte* dmsEnd = dms->window.nextSrc;
                uint dmsSize = (uint)(dmsEnd - dmsBase);
                uint dmsIndexDelta = dictLimit - dmsSize;
                uint dmsMinChain = dmsSize > dmsChainSize ? dmsSize - dmsChainSize : 0;

                matchIndex = dms->hashTable[ZSTD_hashPtr((void*)ip, dms->cParams.hashLog, mls)];
                for (; ((matchIndex >= dmsLowestIndex) && (nbAttempts > 0)); nbAttempts--)
                {
                    nuint currentMl = 0;
                    byte* match = dmsBase + matchIndex;

                    assert(match + 4 <= dmsEnd);
                    if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                    {
                        currentMl = ZSTD_count_2segments(ip + 4, match + 4, iLimit, dmsEnd, prefixStart) + 4;
                    }

                    if (currentMl > ml)
                    {
                        ml = currentMl;
                        *offsetPtr = curr - (matchIndex + dmsIndexDelta) + (uint)((3 - 1));
                        if (ip + currentMl == iLimit)
                        {
                            break;
                        }
                    }

                    if (matchIndex <= dmsMinChain)
                    {
                        break;
                    }

                    matchIndex = dmsChainTable[matchIndex & dmsChainMask];
                }
            }

            return ml;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_HcFindBestMatch_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_HcFindBestMatch_generic(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_noDict);
        }

        private static nuint ZSTD_HcFindBestMatch_dictMatchState_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_HcFindBestMatch_generic(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        private static nuint ZSTD_HcFindBestMatch_dedicatedDictSearch_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_HcFindBestMatch_generic(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_HcFindBestMatch_extDict_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_HcFindBestMatch_generic(ms, ip, iLimit, offsetPtr, mls, ZSTD_dictMode_e.ZSTD_extDict);
        }

        /* Returns a 128-bit container with 128-bits from src */
        [InlineMethod.Inline]
        private static Vector128<byte> ZSTD_Vec128_read(void* src)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Sse2.IsSupported)
            {
                return Sse2.LoadVector128((byte*) src);
            }
#endif
#if NET5_0_OR_GREATER
            if (AdvSimd.IsSupported)
            {
                return AdvSimd.LoadVector128((byte*) src);
            }
#endif
            Vector128<byte> ret;
            memcpy(&ret, src, sizeof(Vector128<byte>));
            return ret;
        }

        /* Returns a ZSTD_Vec128 with the byte "val" packed 16 times */
        [InlineMethod.Inline]
        private static Vector128<byte> ZSTD_Vec128_set8(byte val)
        {
            return Vector128.Create(val);
        }

#if NET5_0_OR_GREATER
        /* Mimics '_mm_movemask_epi8()' from SSE */
        static uint ZSTD_vmovmaskq_u8(Vector128<byte> val)
        {
            /* Shift out everything but the MSB bits in each byte */
            Vector128<UInt16> highBits = AdvSimd.ShiftRightLogical(val, 7).As<byte, UInt16>();
            /* Merge the even lanes together with vsra (right shift and add) */
            Vector128<UInt32> paired16 = AdvSimd.ShiftRightLogicalAdd(highBits, highBits, 7).As<UInt16, UInt32>();
            Vector128<UInt64> paired32 = AdvSimd.ShiftRightLogicalAdd(paired16, paired16, 14).As<UInt32, UInt64>();
            Vector128<byte> paired64 = AdvSimd.ShiftRightLogicalAdd(paired32, paired32, 28).As<UInt64, byte>();
            /* Extract the low 8 bits from each lane, merge */
            return AdvSimd.Extract(paired64, 0) | ((uint) AdvSimd.Extract(paired64, 8) << 8);
        }
#endif

        /* Do byte-by-byte comparison result of x and y. Then collapse 128-bit resultant mask
         * into a 32-bit mask that is the MSB of each byte.
         * */
        [InlineMethod.Inline]
        private static uint ZSTD_Vec128_cmpMask8(Vector128<byte> x, Vector128<byte> y)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Sse2.IsSupported)
            {
                return (uint) Sse2.MoveMask(Sse2.CompareEqual(x, y));
            }
#endif
#if NET5_0_OR_GREATER
            if (AdvSimd.IsSupported)
            {
                return ZSTD_vmovmaskq_u8(AdvSimd.CompareEqual(x, y));
            }
#endif
            uint res = 0;
            int i = 0;
            int l = 0;
            for (; i < Vector128<nuint>.Count; ++i)
            {
                nuint cmp1 = x.As<byte, nuint>().GetElement(i);
                nuint cmp2 = y.As<byte, nuint>().GetElement(i);
                int j = 0;
                for (; j < sizeof(nuint); ++j, ++l)
                {
                    if (((cmp1 >> j * 8) & 0xFF) == ((cmp2 >> j * 8) & 0xFF))
                    {
                        res |= ((uint)1 << (j + i * sizeof(nuint)));
                    }
                }
            }
            return res;
        }

        [InlineMethod.Inline]
        private static ZSTD_Vec256 ZSTD_Vec256_read(void* ptr)
        {
            ZSTD_Vec256 v;

            v.fst = ZSTD_Vec128_read(ptr);
            v.snd = ZSTD_Vec128_read((Vector128<byte>*) ptr + 1);
            return v;
        }

        [InlineMethod.Inline]
        private static ZSTD_Vec256 ZSTD_Vec256_set8(byte val)
        {
            ZSTD_Vec256 v;

            v.fst = ZSTD_Vec128_set8(val);
            v.snd = ZSTD_Vec128_set8(val);
            return v;
        }

        [InlineMethod.Inline]
        private static uint ZSTD_Vec256_cmpMask8(ZSTD_Vec256 x, ZSTD_Vec256 y)
        {
            uint fstMask;
            uint sndMask;

            fstMask = ZSTD_Vec128_cmpMask8(x.fst, y.fst);
            sndMask = ZSTD_Vec128_cmpMask8(x.snd, y.snd);
            return fstMask | (sndMask << 16);
        }

        /* ZSTD_VecMask_next():
         * Starting from the LSB, returns the idx of the next non-zero bit.
         * Basically counting the nb of trailing zeroes.
         */
        [InlineMethod.Inline]
        private static uint ZSTD_VecMask_next(uint val)
        {
            if (val == 0)
            {
                return 0;
            }

            return (uint)BitOperations.TrailingZeroCount(val);
        }

        /* ZSTD_VecMask_rotateRight():
         * Rotates a bitfield to the right by "rotation" bits.
         * If the rotation is greater than totalBits, the returned mask is 0.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint ZSTD_VecMask_rotateRight(uint mask, uint rotation, uint totalBits)
        {
            if (rotation == 0)
            {
                return mask;
            }

            switch (totalBits)
            {
                default:
                {
                    assert(0 != 0);
                }


                goto case 16;
                case 16:
                {
                    return (mask >> (int)rotation) | (ushort)(mask << (int)(16 - rotation));
                }

                case 32:
                {
                    return (mask >> (int)rotation) | (uint)(mask << (int)(32 - rotation));
                }
            }
        }

        /* ZSTD_row_nextIndex():
         * Returns the next index to insert at within a tagTable row, and updates the "head"
         * value to reflect the update. Essentially cycles backwards from [0, {entries per row})
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint ZSTD_row_nextIndex(byte* tagRow, uint rowMask)
        {
            uint next = (uint)((*tagRow - 1)) & rowMask;

            *tagRow = (byte)(next);
            return next;
        }

        /* ZSTD_isAligned():
         * Checks that a pointer is aligned to "align" bytes which must be a power of 2.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ZSTD_isAligned(void* ptr, nuint align)
        {
            assert((align & (align - 1)) == 0);
            return (((((nuint)(ptr)) & (align - 1)) == 0) ? 1 : 0);
        }

        /* ZSTD_row_prefetch():
         * Performs prefetching for the hashTable and tagTable at a given row.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void ZSTD_row_prefetch(uint* hashTable, ushort* tagTable, uint relRow, uint rowLog)
        {
            Prefetch0((void*)(hashTable + relRow));
            if (rowLog == 5)
            {
                Prefetch0((void*)(hashTable + relRow + 16));
            }

            Prefetch0((void*)(tagTable + relRow));
            assert(rowLog == 4 || rowLog == 5);
            assert((ZSTD_isAligned((void*)(hashTable + relRow), 64)) != 0);
            assert((ZSTD_isAligned((void*)(tagTable + relRow), (nuint)(1) << (int)rowLog)) != 0);
        }

        /* ZSTD_row_fillHashCache():
         * Fill up the hash cache starting at idx, prefetching up to ZSTD_ROW_HASH_CACHE_SIZE entries,
         * but not beyond iLimit.
         */
        private static void ZSTD_row_fillHashCache(ZSTD_matchState_t* ms, byte* @base, uint rowLog, uint mls, uint idx, byte* iLimit)
        {
            uint* hashTable = ms->hashTable;
            ushort* tagTable = ms->tagTable;
            uint hashLog = ms->rowHashLog;
            uint maxElemsToPrefetch = (uint)((@base + idx) > iLimit ? 0 : (uint)(iLimit - (@base + idx) + 1));
            uint lim = idx + (uint)((8) < (maxElemsToPrefetch) ? (8) : (maxElemsToPrefetch));

            for (; idx < lim; ++idx)
            {
                uint hash = (uint)(ZSTD_hashPtr((void*)(@base + idx), hashLog + 8, mls));
                uint row = (hash >> 8) << (int)rowLog;

                ZSTD_row_prefetch(hashTable, tagTable, row, rowLog);
                ms->hashCache[idx & (uint)((8 - 1))] = hash;
            }

        }

        /* ZSTD_row_nextCachedHash():
         * Returns the hash of base + idx, and replaces the hash in the hash cache with the byte at
         * base + idx + ZSTD_ROW_HASH_CACHE_SIZE. Also prefetches the appropriate rows from hashTable and tagTable.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint ZSTD_row_nextCachedHash(uint* cache, uint* hashTable, ushort* tagTable, byte* @base, uint idx, uint hashLog, uint rowLog, uint mls)
        {
            uint newHash = (uint)(ZSTD_hashPtr((void*)(@base + idx + 8), hashLog + 8, mls));
            uint row = (newHash >> 8) << (int)rowLog;

            ZSTD_row_prefetch(hashTable, tagTable, row, rowLog);

            {
                uint hash = cache[idx & (uint)((8 - 1))];

                cache[idx & (uint)((8 - 1))] = newHash;
                return hash;
            }
        }

        /* ZSTD_row_update_internal():
         * Inserts the byte at ip into the appropriate position in the hash table.
         * Determines the relative row, and the position within the {16, 32} entry row to insert at.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static void ZSTD_row_update_internal(ZSTD_matchState_t* ms, byte* ip, uint mls, uint rowLog, uint rowMask, uint useCache)
        {
            uint* hashTable = ms->hashTable;
            ushort* tagTable = ms->tagTable;
            uint hashLog = ms->rowHashLog;
            byte* @base = ms->window.@base;
            uint target = (uint)(ip - @base);
            uint idx = ms->nextToUpdate;

            for (; idx < target; ++idx)
            {
                uint hash = useCache != 0 ? ZSTD_row_nextCachedHash((uint*)ms->hashCache, hashTable, tagTable, @base, idx, hashLog, rowLog, mls) : (uint)(ZSTD_hashPtr((void*)(@base + idx), hashLog + 8, mls));
                uint relRow = (hash >> 8) << (int)rowLog;
                uint* row = hashTable + relRow;
                byte* tagRow = (byte*)(tagTable + relRow);
                uint pos = ZSTD_row_nextIndex(tagRow, rowMask);

                assert(hash == ZSTD_hashPtr((void*)(@base + idx), hashLog + 8, mls));
                ((byte*)(tagRow))[pos + 1] = (byte)(hash & ((1U << 8) - 1));
                row[pos] = idx;
            }

            ms->nextToUpdate = target;
        }

        /* ZSTD_row_update():
         * External wrapper for ZSTD_row_update_internal(). Used for filling the hashtable during dictionary
         * processing.
         */
        public static void ZSTD_row_update(ZSTD_matchState_t* ms, byte* ip)
        {
            uint rowLog = (uint)(ms->cParams.searchLog < 5 ? 4 : 5);
            uint rowMask = (1U << (int)rowLog) - 1;
            uint mls = ((ms->cParams.minMatch) < (6) ? (ms->cParams.minMatch) : (6));

            ZSTD_row_update_internal(ms, ip, mls, rowLog, rowMask, 0);
        }

        /* Returns a ZSTD_VecMask (U32) that has the nth bit set to 1 if the newly-computed "tag" matches
         * the hash at the nth position in a row of the tagTable.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static uint ZSTD_row_getMatchMask(byte* tagRow, byte tag, uint head, uint rowEntries)
        {
            uint matches = 0;

            if (rowEntries == 16)
            {
                var hashes = ZSTD_Vec128_read((void*)(tagRow + 1));
                var expandedTags = ZSTD_Vec128_set8(tag);

                matches = ZSTD_Vec128_cmpMask8(hashes, expandedTags);
            }
            else if (rowEntries == 32)
            {
                ZSTD_Vec256 hashes = ZSTD_Vec256_read((void*)(tagRow + 1));
                ZSTD_Vec256 expandedTags = ZSTD_Vec256_set8(tag);

                matches = ZSTD_Vec256_cmpMask8(hashes, expandedTags);
            }
            else
            {
                assert(0 != 0);
            }

            return ZSTD_VecMask_rotateRight(matches, head, rowEntries);
        }

        /* The high-level approach of the SIMD row based match finder is as follows:
         * - Figure out where to insert the new entry:
         *      - Generate a hash from a byte along with an additional 1-byte "short hash". The additional byte is our "tag"
         *      - The hashTable is effectively split into groups or "rows" of 16 or 32 entries of U32, and the hash determines
         *        which row to insert into.
         *      - Determine the correct position within the row to insert the entry into. Each row of 16 or 32 can
         *        be considered as a circular buffer with a "head" index that resides in the tagTable.
         *      - Also insert the "tag" into the equivalent row and position in the tagTable.
         *          - Note: The tagTable has 17 or 33 1-byte entries per row, due to 16 or 32 tags, and 1 "head" entry.
         *                  The 17 or 33 entry rows are spaced out to occur every 32 or 64 bytes, respectively,
         *                  for alignment/performance reasons, leaving some bytes unused.
         * - Use SIMD to efficiently compare the tags in the tagTable to the 1-byte "short hash" and
         *   generate a bitfield that we can cycle through to check the collisions in the hash table.
         * - Pick the longest match.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint ZSTD_RowFindBestMatch_generic(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr, uint mls, ZSTD_dictMode_e dictMode, uint rowLog)
        {
            uint* hashTable = ms->hashTable;
            ushort* tagTable = ms->tagTable;
            uint* hashCache = (uint*)ms->hashCache;
            uint hashLog = ms->rowHashLog;
            ZSTD_compressionParameters* cParams = &ms->cParams;
            byte* @base = ms->window.@base;
            byte* dictBase = ms->window.dictBase;
            uint dictLimit = ms->window.dictLimit;
            byte* prefixStart = @base + dictLimit;
            byte* dictEnd = dictBase + dictLimit;
            uint curr = (uint)(ip - @base);
            uint maxDistance = 1U << (int)cParams->windowLog;
            uint lowestValid = ms->window.lowLimit;
            uint withinMaxDistance = (curr - lowestValid > maxDistance) ? curr - maxDistance : lowestValid;
            uint isDictionary = (((ms->loadedDictEnd != 0)) ? 1U : 0U);
            uint lowLimit = isDictionary != 0 ? lowestValid : withinMaxDistance;
            uint rowEntries = (1U << (int)rowLog);
            uint rowMask = rowEntries - 1;
            uint cappedSearchLog = ((cParams->searchLog) < (rowLog) ? (cParams->searchLog) : (rowLog));
            uint nbAttempts = 1U << (int)cappedSearchLog;
            nuint ml = (nuint)(4 - 1);
            ZSTD_matchState_t* dms = ms->dictMatchState;
            nuint ddsIdx = default;
            uint ddsExtraAttempts = default;
            uint dmsTag = default;
            uint* dmsRow = default;
            byte* dmsTagRow = default;

            if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
            {
                uint ddsHashLog = dms->cParams.hashLog - 2;


                {
                    ddsIdx = ZSTD_hashPtr((void*)ip, ddsHashLog, mls) << 2;
                    Prefetch0((void*)(&dms->hashTable[ddsIdx]));
                }

                ddsExtraAttempts = cParams->searchLog > rowLog ? 1U << (int)(cParams->searchLog - rowLog) : 0;
            }

            if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
            {
                uint* dmsHashTable = dms->hashTable;
                ushort* dmsTagTable = dms->tagTable;
                uint dmsHash = (uint)(ZSTD_hashPtr((void*)ip, dms->rowHashLog + 8, mls));
                uint dmsRelRow = (dmsHash >> 8) << (int)rowLog;

                dmsTag = dmsHash & ((1U << 8) - 1);
                dmsTagRow = (byte*)(dmsTagTable + dmsRelRow);
                dmsRow = dmsHashTable + dmsRelRow;
                ZSTD_row_prefetch(dmsHashTable, dmsTagTable, dmsRelRow, rowLog);
            }

            ZSTD_row_update_internal(ms, ip, mls, rowLog, rowMask, 1);

            {
                uint hash = ZSTD_row_nextCachedHash(hashCache, hashTable, tagTable, @base, curr, hashLog, rowLog, mls);
                uint relRow = (hash >> 8) << (int)rowLog;
                uint tag = hash & ((1U << 8) - 1);
                uint* row = hashTable + relRow;
                byte* tagRow = (byte*)(tagTable + relRow);
                uint head = *tagRow & rowMask;
                uint* matchBuffer = stackalloc uint[32];
                nuint numMatches = 0;
                nuint currMatch = 0;
                uint matches = ZSTD_row_getMatchMask(tagRow, (byte)(tag), head, rowEntries);

                for (; (matches > 0) && (nbAttempts > 0); --nbAttempts , matches &= (matches - 1))
                {
                    uint matchPos = (head + ZSTD_VecMask_next(matches)) & rowMask;
                    uint matchIndex = row[matchPos];

                    assert(numMatches < rowEntries);
                    if (matchIndex < lowLimit)
                    {
                        break;
                    }

                    if ((dictMode != ZSTD_dictMode_e.ZSTD_extDict) || matchIndex >= dictLimit)
                    {
                        Prefetch0((void*)(@base + matchIndex));
                    }
                    else
                    {
                        Prefetch0((void*)(dictBase + matchIndex));
                    }

                    matchBuffer[numMatches++] = matchIndex;
                }


                {
                    uint pos = ZSTD_row_nextIndex(tagRow, rowMask);

                    tagRow[pos + 1] = (byte)(tag);
                    row[pos] = ms->nextToUpdate++;
                }

                for (; currMatch < numMatches; ++currMatch)
                {
                    uint matchIndex = matchBuffer[currMatch];
                    nuint currentMl = 0;

                    assert(matchIndex < curr);
                    assert(matchIndex >= lowLimit);
                    if ((dictMode != ZSTD_dictMode_e.ZSTD_extDict) || matchIndex >= dictLimit)
                    {
                        byte* match = @base + matchIndex;

                        assert(matchIndex >= dictLimit);
                        if (match[ml] == ip[ml])
                        {
                            currentMl = ZSTD_count(ip, match, iLimit);
                        }
                    }
                    else
                    {
                        byte* match = dictBase + matchIndex;

                        assert(match + 4 <= dictEnd);
                        if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                        {
                            currentMl = ZSTD_count_2segments(ip + 4, match + 4, iLimit, dictEnd, prefixStart) + 4;
                        }
                    }

                    if (currentMl > ml)
                    {
                        ml = currentMl;
                        *offsetPtr = curr - matchIndex + (uint)((3 - 1));
                        if (ip + currentMl == iLimit)
                        {
                            break;
                        }
                    }
                }
            }

            if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
            {
                ml = ZSTD_dedicatedDictSearch_lazy_search(offsetPtr, ml, nbAttempts + ddsExtraAttempts, dms, ip, iLimit, prefixStart, curr, dictLimit, ddsIdx);
            }
            else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
            {
                uint dmsLowestIndex = dms->window.dictLimit;
                byte* dmsBase = dms->window.@base;
                byte* dmsEnd = dms->window.nextSrc;
                uint dmsSize = (uint)(dmsEnd - dmsBase);
                uint dmsIndexDelta = dictLimit - dmsSize;


                {
                    uint head = *dmsTagRow & rowMask;
                    uint* matchBuffer = stackalloc uint[32];
                    nuint numMatches = 0;
                    nuint currMatch = 0;
                    uint matches = ZSTD_row_getMatchMask(dmsTagRow, (byte)(dmsTag), head, rowEntries);

                    for (; (matches > 0) && (nbAttempts > 0); --nbAttempts , matches &= (matches - 1))
                    {
                        uint matchPos = (head + ZSTD_VecMask_next(matches)) & rowMask;
                        uint matchIndex = dmsRow[matchPos];

                        if (matchIndex < dmsLowestIndex)
                        {
                            break;
                        }

                        Prefetch0((void*)(dmsBase + matchIndex));
                        matchBuffer[numMatches++] = matchIndex;
                    }

                    for (; currMatch < numMatches; ++currMatch)
                    {
                        uint matchIndex = matchBuffer[currMatch];
                        nuint currentMl = 0;

                        assert(matchIndex >= dmsLowestIndex);
                        assert(matchIndex < curr);

                        {
                            byte* match = dmsBase + matchIndex;

                            assert(match + 4 <= dmsEnd);
                            if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                            {
                                currentMl = ZSTD_count_2segments(ip + 4, match + 4, iLimit, dmsEnd, prefixStart) + 4;
                            }
                        }

                        if (currentMl > ml)
                        {
                            ml = currentMl;
                            *offsetPtr = curr - (matchIndex + dmsIndexDelta) + (uint)((3 - 1));
                            if (ip + currentMl == iLimit)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return ml;
        }

        /* Inlining is important to hardwire a hot branch (template emulation) */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static nuint ZSTD_RowFindBestMatch_selectMLS(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, ZSTD_dictMode_e dictMode, nuint* offsetPtr, uint rowLog)
        {
            uint mls;
            switch (ms->cParams.minMatch)
            {
                default:
                case 4:
                {
                    mls = 4;
                    break;
                }

                case 5:
                {
                    mls = 5;
                    break;
                }

                case 7:
                case 6:
                {
                    mls = 6;
                    break;
                }
            }
            return ZSTD_RowFindBestMatch_generic(ms, ip, iLimit, offsetPtr, mls, dictMode, rowLog);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_RowFindBestMatch_selectRowLog(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint cappedSearchLog = ((ms->cParams.searchLog) < (5) ? (ms->cParams.searchLog) : (5));

            return ZSTD_RowFindBestMatch_selectMLS(ms, ip, iLimit, ZSTD_dictMode_e.ZSTD_noDict, offsetPtr, cappedSearchLog == 5U ? 5U : 4U);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_RowFindBestMatch_dictMatchState_selectRowLog(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint cappedSearchLog = ((ms->cParams.searchLog) < (5) ? (ms->cParams.searchLog) : (5));
            
            return ZSTD_RowFindBestMatch_selectMLS(ms, ip, iLimit, ZSTD_dictMode_e.ZSTD_dictMatchState, offsetPtr, cappedSearchLog == 5U ? 5U : 4U);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_selectRowLog(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint cappedSearchLog = ((ms->cParams.searchLog) < (5) ? (ms->cParams.searchLog) : (5));

            return ZSTD_RowFindBestMatch_selectMLS(ms, ip, iLimit, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch, offsetPtr, cappedSearchLog == 5U ? 5U : 4U);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_RowFindBestMatch_extDict_selectRowLog(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr)
        {
            uint cappedSearchLog = ((ms->cParams.searchLog) < (5) ? (ms->cParams.searchLog) : (5));

            return ZSTD_RowFindBestMatch_selectMLS(ms, ip, iLimit, ZSTD_dictMode_e.ZSTD_extDict, offsetPtr, cappedSearchLog == 5U ? 5U : 4U);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_compressBlock_lazy_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, searchMethod_e searchMethod, uint depth, ZSTD_dictMode_e dictMode)
        {
            byte* istart = (byte*)(src);
            byte* ip = istart;
            byte* anchor = istart;
            byte* iend = istart + srcSize;
            byte* ilimit = searchMethod == searchMethod_e.search_rowHash ? iend - 8 - 8 : iend - 8;
            byte* @base = ms->window.@base;
            uint prefixLowestIndex = ms->window.dictLimit;
            byte* prefixLowest = @base + prefixLowestIndex;
            uint rowLog = (uint)(ms->cParams.searchLog < 5 ? 4 : 5);
            ;

            var searchMax = searchFuncs[(int)dictMode][(int)(searchMethod)] ?? throw new InvalidOperationException();
            uint offset_1 = rep[0], offset_2 = rep[1], savedOffset = 0;
            int isDMS = ((dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState) ? 1 : 0);
            int isDDS = ((dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch) ? 1 : 0);
            int isDxS = ((isDMS != 0 || isDDS != 0) ? 1 : 0);
            ZSTD_matchState_t* dms = ms->dictMatchState;
            uint dictLowestIndex = isDxS != 0 ? dms->window.dictLimit : 0;
            byte* dictBase = isDxS != 0 ? dms->window.@base : null;
            byte* dictLowest = isDxS != 0 ? dictBase + dictLowestIndex : null;
            byte* dictEnd = isDxS != 0 ? dms->window.nextSrc : null;
            uint dictIndexDelta = isDxS != 0 ? prefixLowestIndex - (uint)(dictEnd - dictBase) : 0;
            uint dictAndPrefixLength = (uint)((ip - prefixLowest) + (dictEnd - dictLowest));

            assert(searchMax != null);
            ip += ((dictAndPrefixLength == 0) ? 1 : 0);
            if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
            {
                uint curr = (uint)(ip - @base);
                uint windowLow = ZSTD_getLowestPrefixIndex(ms, curr, ms->cParams.windowLog);
                uint maxRep = curr - windowLow;

                if (offset_2 > maxRep)
                {
                    savedOffset = offset_2; offset_2 = 0;
                }

                if (offset_1 > maxRep)
                {
                    savedOffset = offset_1; offset_1 = 0;
                }
            }

            if (isDxS != 0)
            {
                assert(offset_1 <= dictAndPrefixLength);
                assert(offset_2 <= dictAndPrefixLength);
            }

            if (searchMethod == searchMethod_e.search_rowHash)
            {
                ZSTD_row_fillHashCache(ms, @base, rowLog, ((ms->cParams.minMatch) < (6) ? (ms->cParams.minMatch) : (6)), ms->nextToUpdate, ilimit);
            }

            while (ip < ilimit)
            {
                nuint matchLength = 0;
                nuint offset = 0;
                byte* start = ip + 1;

                if (isDxS != 0)
                {
                    uint repIndex = (uint)(ip - @base) + 1 - offset_1;
                    byte* repMatch = ((dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState || dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch) && repIndex < prefixLowestIndex) ? dictBase + (repIndex - dictIndexDelta) : @base + repIndex;

                    if (((uint)((prefixLowestIndex - 1) - repIndex) >= 3) && (MEM_read32((void*)repMatch) == MEM_read32((void*)(ip + 1))))
                    {
                        byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;

                        matchLength = ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixLowest) + 4;
                        if (depth == 0)
                        {
                            goto _storeSequence;
                        }
                    }
                }

                if (dictMode == ZSTD_dictMode_e.ZSTD_noDict && (((offset_1 > 0) && (MEM_read32((void*)(ip + 1 - offset_1)) == MEM_read32((void*)(ip + 1))))))
                {
                    matchLength = ZSTD_count(ip + 1 + 4, ip + 1 + 4 - offset_1, iend) + 4;
                    if (depth == 0)
                    {
                        goto _storeSequence;
                    }
                }


                {
                    nuint offsetFound = 999999999;
                    searchMax  = searchMax ?? throw new InvalidOperationException();
                    nuint ml2 = searchMax(ms, ip, iend, &offsetFound);

                    if (ml2 > matchLength)
                    {
                        matchLength = ml2; start = ip; offset = offsetFound;
                    }
                }

                if (matchLength < 4)
                {
                    ip += ((ip - anchor) >> 8) + 1;
                    continue;
                }

                if (depth >= 1)
                {
                    while (ip < ilimit)
                    {
                        ip++;
                        if ((dictMode == ZSTD_dictMode_e.ZSTD_noDict) && (offset) != 0 && (((offset_1 > 0) && (MEM_read32((void*)ip) == MEM_read32((void*)(ip - offset_1))))))
                        {
                            nuint mlRep = ZSTD_count(ip + 4, ip + 4 - offset_1, iend) + 4;
                            int gain2 = (int)(mlRep * 3);
                            int gain1 = (int)(matchLength * 3 - ZSTD_highbit32((uint)(offset) + 1) + 1);

                            if ((mlRep >= 4) && (gain2 > gain1))
                            {
                                matchLength = mlRep; offset = 0; start = ip;
                            }
                        }

                        if (isDxS != 0)
                        {
                            uint repIndex = (uint)(ip - @base) - offset_1;
                            byte* repMatch = repIndex < prefixLowestIndex ? dictBase + (repIndex - dictIndexDelta) : @base + repIndex;

                            if (((uint)((prefixLowestIndex - 1) - repIndex) >= 3) && (MEM_read32((void*)repMatch) == MEM_read32((void*)ip)))
                            {
                                byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;
                                nuint mlRep = ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repMatchEnd, prefixLowest) + 4;
                                int gain2 = (int)(mlRep * 3);
                                int gain1 = (int)(matchLength * 3 - ZSTD_highbit32((uint)(offset) + 1) + 1);

                                if ((mlRep >= 4) && (gain2 > gain1))
                                {
                                    matchLength = mlRep; offset = 0; start = ip;
                                }
                            }
                        }


                        {
                            nuint offset2 = 999999999;
                            nuint ml2 = searchMax(ms, ip, iend, &offset2);
                            int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)(offset2) + 1));
                            int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 4);

                            if ((ml2 >= 4) && (gain2 > gain1))
                            {
                                matchLength = ml2; offset = offset2; start = ip;
                                continue;
                            }
                        }

                        if ((depth == 2) && (ip < ilimit))
                        {
                            ip++;
                            if ((dictMode == ZSTD_dictMode_e.ZSTD_noDict) && (offset) != 0 && (((offset_1 > 0) && (MEM_read32((void*)ip) == MEM_read32((void*)(ip - offset_1))))))
                            {
                                nuint mlRep = ZSTD_count(ip + 4, ip + 4 - offset_1, iend) + 4;
                                int gain2 = (int)(mlRep * 4);
                                int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 1);

                                if ((mlRep >= 4) && (gain2 > gain1))
                                {
                                    matchLength = mlRep; offset = 0; start = ip;
                                }
                            }

                            if (isDxS != 0)
                            {
                                uint repIndex = (uint)(ip - @base) - offset_1;
                                byte* repMatch = repIndex < prefixLowestIndex ? dictBase + (repIndex - dictIndexDelta) : @base + repIndex;

                                if (((uint)((prefixLowestIndex - 1) - repIndex) >= 3) && (MEM_read32((void*)repMatch) == MEM_read32((void*)ip)))
                                {
                                    byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;
                                    nuint mlRep = ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repMatchEnd, prefixLowest) + 4;
                                    int gain2 = (int)(mlRep * 4);
                                    int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 1);

                                    if ((mlRep >= 4) && (gain2 > gain1))
                                    {
                                        matchLength = mlRep; offset = 0; start = ip;
                                    }
                                }
                            }


                            {
                                nuint offset2 = 999999999;
                                nuint ml2 = searchMax(ms, ip, iend, &offset2);
                                int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)(offset2) + 1));
                                int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 7);

                                if ((ml2 >= 4) && (gain2 > gain1))
                                {
                                    matchLength = ml2; offset = offset2; start = ip;
                                    continue;
                                }
                            }
                        }

                        break;
                    }
                }

                if (offset != 0)
                {
                    if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
                    {
                        while ((((start > anchor) && (start - (offset - (uint)((3 - 1))) > prefixLowest))) && (start[-1] == (start - (offset - (uint)((3 - 1))))[-1]))
                        {
                            start--;
                            matchLength++;
                        }
                    }

                    if (isDxS != 0)
                    {
                        uint matchIndex = (uint)((ulong)((start - @base)) - (offset - (uint)((3 - 1))));
                        byte* match = (matchIndex < prefixLowestIndex) ? dictBase + matchIndex - dictIndexDelta : @base + matchIndex;
                        byte* mStart = (matchIndex < prefixLowestIndex) ? dictLowest : prefixLowest;

                        while ((start > anchor) && (match > mStart) && (start[-1] == match[-1]))
                        {
                            start--;
                            match--;
                            matchLength++;
                        }
                    }

                    offset_2 = offset_1;
                    offset_1 = (uint)(offset - (uint)((3 - 1)));
                }

                _storeSequence:
                        {
                    nuint litLength = (nuint)(start - anchor);

                    ZSTD_storeSeq(seqStore, litLength, anchor, iend, (uint)(offset), matchLength - 3);
                    anchor = ip = start + matchLength;
                }

                if (isDxS != 0)
                {
                    while (ip <= ilimit)
                    {
                        uint current2 = (uint)(ip - @base);
                        uint repIndex = current2 - offset_2;
                        byte* repMatch = repIndex < prefixLowestIndex ? dictBase - dictIndexDelta + repIndex : @base + repIndex;

                        if (((uint)((prefixLowestIndex - 1) - (uint)(repIndex)) >= 3) && (MEM_read32((void*)repMatch) == MEM_read32((void*)ip)))
                        {
                            byte* repEnd2 = repIndex < prefixLowestIndex ? dictEnd : iend;

                            matchLength = ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repEnd2, prefixLowest) + 4;
                            offset = offset_2;
                            offset_2 = offset_1;
                            offset_1 = (uint)(offset);
                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, matchLength - 3);
                            ip += matchLength;
                            anchor = ip;
                            continue;
                        }

                        break;
                    }
                }

                if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
                {
                    while ((((ip <= ilimit) && (offset_2 > 0))) && (MEM_read32((void*)ip) == MEM_read32((void*)(ip - offset_2))))
                    {
                        matchLength = ZSTD_count(ip + 4, ip + 4 - offset_2, iend) + 4;
                        offset = offset_2;
                        offset_2 = offset_1;
                        offset_1 = (uint)(offset);
                        ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, matchLength - 3);
                        ip += matchLength;
                        anchor = ip;
                        continue;
                    }
                }
            }

            rep[0] = offset_1 != 0 ? offset_1 : savedOffset;
            rep[1] = offset_2 != 0 ? offset_2 : savedOffset;
            return (nuint)(iend - anchor);
        }

        /*! used in ZSTD_reduceIndex(). preemptively increase value of ZSTD_DUBT_UNSORTED_MARK */
        public static nuint ZSTD_compressBlock_btlazy2(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_binaryTree, 2, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_lazy2(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 2, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_lazy(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 1, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_greedy(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 0, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_btlazy2_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_binaryTree, 2, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_lazy2_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 2, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_lazy_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 1, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_greedy_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 0, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_lazy2_dedicatedDictSearch(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 2, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        public static nuint ZSTD_compressBlock_lazy_dedicatedDictSearch(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 1, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        public static nuint ZSTD_compressBlock_greedy_dedicatedDictSearch(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 0, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        /* Row-based matchfinder */
        public static nuint ZSTD_compressBlock_lazy2_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 2, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_lazy_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 1, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_greedy_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 0, ZSTD_dictMode_e.ZSTD_noDict);
        }

        public static nuint ZSTD_compressBlock_lazy2_dictMatchState_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 2, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_lazy_dictMatchState_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 1, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_greedy_dictMatchState_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 0, ZSTD_dictMode_e.ZSTD_dictMatchState);
        }

        public static nuint ZSTD_compressBlock_lazy2_dedicatedDictSearch_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 2, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        public static nuint ZSTD_compressBlock_lazy_dedicatedDictSearch_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 1, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        public static nuint ZSTD_compressBlock_greedy_dedicatedDictSearch_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 0, ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_compressBlock_lazy_extDict_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, searchMethod_e searchMethod, uint depth)
        {
            byte* istart = (byte*)(src);
            byte* ip = istart;
            byte* anchor = istart;
            byte* iend = istart + srcSize;
            byte* ilimit = searchMethod == searchMethod_e.search_rowHash ? iend - 8 - 8 : iend - 8;
            byte* @base = ms->window.@base;
            uint dictLimit = ms->window.dictLimit;
            byte* prefixStart = @base + dictLimit;
            byte* dictBase = ms->window.dictBase;
            byte* dictEnd = dictBase + dictLimit;
            byte* dictStart = dictBase + ms->window.lowLimit;
            uint windowLog = ms->cParams.windowLog;
            uint rowLog = (uint)(ms->cParams.searchLog < 5 ? 4 : 5);
            ;

            searchMax_f searchMax = searchFuncsExtGeneric[(int)(searchMethod)];
            uint offset_1 = rep[0], offset_2 = rep[1];

            ip += ((ip == prefixStart) ? 1 : 0);
            if (searchMethod == searchMethod_e.search_rowHash)
            {
                ZSTD_row_fillHashCache(ms, @base, rowLog, ((ms->cParams.minMatch) < (6) ? (ms->cParams.minMatch) : (6)), ms->nextToUpdate, ilimit);
            }

            while (ip < ilimit)
            {
                nuint matchLength = 0;
                nuint offset = 0;
                byte* start = ip + 1;
                uint curr = (uint)(ip - @base);


                {
                    uint windowLow = ZSTD_getLowestMatchIndex(ms, curr + 1, windowLog);
                    uint repIndex = (uint)(curr + 1 - offset_1);
                    byte* repBase = repIndex < dictLimit ? dictBase : @base;
                    byte* repMatch = repBase + repIndex;

                    if ((((uint)((dictLimit - 1) - repIndex) >= 3) && (offset_1 < curr + 1 - windowLow)))
                    {
                        if (MEM_read32((void*)(ip + 1)) == MEM_read32((void*)repMatch))
                        {
                            byte* repEnd = repIndex < dictLimit ? dictEnd : iend;

                            matchLength = ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repEnd, prefixStart) + 4;
                            if (depth == 0)
                            {
                                goto _storeSequence;
                            }
                        }
                    }
                }


                {
                    nuint offsetFound = 999999999;
                    nuint ml2 = searchMax(ms, ip, iend, &offsetFound);

                    if (ml2 > matchLength)
                    {
                        matchLength = ml2; start = ip; offset = offsetFound;
                    }
                }

                if (matchLength < 4)
                {
                    ip += ((ip - anchor) >> 8) + 1;
                    continue;
                }

                if (depth >= 1)
                {
                    while (ip < ilimit)
                    {
                        ip++;
                        curr++;
                        if (offset != 0)
                        {
                            uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, windowLog);
                            uint repIndex = (uint)(curr - offset_1);
                            byte* repBase = repIndex < dictLimit ? dictBase : @base;
                            byte* repMatch = repBase + repIndex;

                            if ((((uint)((dictLimit - 1) - repIndex) >= 3) && (offset_1 < curr - windowLow)))
                            {
                                if (MEM_read32((void*)ip) == MEM_read32((void*)repMatch))
                                {
                                    byte* repEnd = repIndex < dictLimit ? dictEnd : iend;
                                    nuint repLength = ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repEnd, prefixStart) + 4;
                                    int gain2 = (int)(repLength * 3);
                                    int gain1 = (int)(matchLength * 3 - ZSTD_highbit32((uint)(offset) + 1) + 1);

                                    if ((repLength >= 4) && (gain2 > gain1))
                                    {
                                        matchLength = repLength; offset = 0; start = ip;
                                    }
                                }
                            }
                        }


                        {
                            nuint offset2 = 999999999;
                            nuint ml2 = searchMax(ms, ip, iend, &offset2);
                            int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)(offset2) + 1));
                            int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 4);

                            if ((ml2 >= 4) && (gain2 > gain1))
                            {
                                matchLength = ml2; offset = offset2; start = ip;
                                continue;
                            }
                        }

                        if ((depth == 2) && (ip < ilimit))
                        {
                            ip++;
                            curr++;
                            if (offset != 0)
                            {
                                uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, windowLog);
                                uint repIndex = (uint)(curr - offset_1);
                                byte* repBase = repIndex < dictLimit ? dictBase : @base;
                                byte* repMatch = repBase + repIndex;

                                if ((((uint)((dictLimit - 1) - repIndex) >= 3) && (offset_1 < curr - windowLow)))
                                {
                                    if (MEM_read32((void*)ip) == MEM_read32((void*)repMatch))
                                    {
                                        byte* repEnd = repIndex < dictLimit ? dictEnd : iend;
                                        nuint repLength = ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repEnd, prefixStart) + 4;
                                        int gain2 = (int)(repLength * 4);
                                        int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 1);

                                        if ((repLength >= 4) && (gain2 > gain1))
                                        {
                                            matchLength = repLength; offset = 0; start = ip;
                                        }
                                    }
                                }
                            }


                            {
                                nuint offset2 = 999999999;
                                nuint ml2 = searchMax(ms, ip, iend, &offset2);
                                int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)(offset2) + 1));
                                int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)(offset) + 1) + 7);

                                if ((ml2 >= 4) && (gain2 > gain1))
                                {
                                    matchLength = ml2; offset = offset2; start = ip;
                                    continue;
                                }
                            }
                        }

                        break;
                    }
                }

                if (offset != 0)
                {
                    uint matchIndex = (uint)((ulong)((start - @base)) - (offset - (uint)((3 - 1))));
                    byte* match = (matchIndex < dictLimit) ? dictBase + matchIndex : @base + matchIndex;
                    byte* mStart = (matchIndex < dictLimit) ? dictStart : prefixStart;

                    while ((start > anchor) && (match > mStart) && (start[-1] == match[-1]))
                    {
                        start--;
                        match--;
                        matchLength++;
                    }

                    offset_2 = offset_1;
                    offset_1 = (uint)(offset - (uint)((3 - 1)));
                }

                _storeSequence:
                        {
                    nuint litLength = (nuint)(start - anchor);

                    ZSTD_storeSeq(seqStore, litLength, anchor, iend, (uint)(offset), matchLength - 3);
                    anchor = ip = start + matchLength;
                }

                while (ip <= ilimit)
                {
                    uint repCurrent = (uint)(ip - @base);
                    uint windowLow = ZSTD_getLowestMatchIndex(ms, repCurrent, windowLog);
                    uint repIndex = repCurrent - offset_2;
                    byte* repBase = repIndex < dictLimit ? dictBase : @base;
                    byte* repMatch = repBase + repIndex;

                    if ((((uint)((dictLimit - 1) - repIndex) >= 3) && (offset_2 < repCurrent - windowLow)))
                    {
                        if (MEM_read32((void*)ip) == MEM_read32((void*)repMatch))
                        {
                            byte* repEnd = repIndex < dictLimit ? dictEnd : iend;

                            matchLength = ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repEnd, prefixStart) + 4;
                            offset = offset_2;
                            offset_2 = offset_1;
                            offset_1 = (uint)(offset);
                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, matchLength - 3);
                            ip += matchLength;
                            anchor = ip;
                            continue;
                        }
                    }

                    break;
                }
            }

            rep[0] = offset_1;
            rep[1] = offset_2;
            return (nuint)(iend - anchor);
        }

        public static nuint ZSTD_compressBlock_greedy_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 0);
        }

        public static nuint ZSTD_compressBlock_lazy_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 1);
        }

        public static nuint ZSTD_compressBlock_lazy2_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_hashChain, 2);
        }

        public static nuint ZSTD_compressBlock_btlazy2_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_binaryTree, 2);
        }

        public static nuint ZSTD_compressBlock_greedy_extDict_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 0);
        }

        public static nuint ZSTD_compressBlock_lazy_extDict_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 1);
        }

        public static nuint ZSTD_compressBlock_lazy2_extDict_row(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            return ZSTD_compressBlock_lazy_extDict_generic(ms, seqStore, rep, src, srcSize, searchMethod_e.search_rowHash, 2);
        }
    }
}
