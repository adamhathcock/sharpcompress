using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
#endif

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*-*************************************
     *  Binary Tree search
     ***************************************/
    private static void ZSTD_updateDUBT(ZSTD_MatchState_t* ms, byte* ip, byte* iend, uint mls)
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
        assert(ip + 8 <= iend);
        assert(idx >= ms->window.dictLimit);
        for (; idx < target; idx++)
        {
            /* assumption : ip + 8 <= iend */
            nuint h = ZSTD_hashPtr(@base + idx, hashLog, mls);
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
    private static void ZSTD_insertDUBT1(
        ZSTD_MatchState_t* ms,
        uint curr,
        byte* inputEnd,
        uint nbCompares,
        uint btLow,
        ZSTD_dictMode_e dictMode
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* bt = ms->chainTable;
        uint btLog = cParams->chainLog - 1;
        uint btMask = (uint)((1 << (int)btLog) - 1);
        nuint commonLengthSmaller = 0,
            commonLengthLarger = 0;
        byte* @base = ms->window.@base;
        byte* dictBase = ms->window.dictBase;
        uint dictLimit = ms->window.dictLimit;
        byte* ip = curr >= dictLimit ? @base + curr : dictBase + curr;
        byte* iend = curr >= dictLimit ? inputEnd : dictBase + dictLimit;
        byte* dictEnd = dictBase + dictLimit;
        byte* prefixStart = @base + dictLimit;
        byte* match;
        uint* smallerPtr = bt + 2 * (curr & btMask);
        uint* largerPtr = smallerPtr + 1;
        /* this candidate is unsorted : next sorted candidate is reached through *smallerPtr, while *largerPtr contains previous unsorted candidate (which is already saved and can be overwritten) */
        uint matchIndex = *smallerPtr;
        /* to be nullified at the end */
        uint dummy32;
        uint windowValid = ms->window.lowLimit;
        uint maxDistance = 1U << (int)cParams->windowLog;
        uint windowLow = curr - windowValid > maxDistance ? curr - maxDistance : windowValid;
        assert(curr >= btLow);
        assert(ip < iend);
        for (; nbCompares != 0 && matchIndex > windowLow; --nbCompares)
        {
            uint* nextPtr = bt + 2 * (matchIndex & btMask);
            /* guaranteed minimum nb of common bytes */
            nuint matchLength =
                commonLengthSmaller < commonLengthLarger ? commonLengthSmaller : commonLengthLarger;
            assert(matchIndex < curr);
            if (
                dictMode != ZSTD_dictMode_e.ZSTD_extDict
                || matchIndex + matchLength >= dictLimit
                || curr < dictLimit
            )
            {
                byte* mBase =
                    dictMode != ZSTD_dictMode_e.ZSTD_extDict
                    || matchIndex + matchLength >= dictLimit
                        ? @base
                        : dictBase;
                assert(matchIndex + matchLength >= dictLimit || curr < dictLimit);
                match = mBase + matchIndex;
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

    private static nuint ZSTD_DUBT_findBetterDictMatch(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iend,
        nuint* offsetPtr,
        nuint bestLength,
        uint nbCompares,
        uint mls,
        ZSTD_dictMode_e dictMode
    )
    {
        ZSTD_MatchState_t* dms = ms->dictMatchState;
        ZSTD_compressionParameters* dmsCParams = &dms->cParams;
        uint* dictHashTable = dms->hashTable;
        uint hashLog = dmsCParams->hashLog;
        nuint h = ZSTD_hashPtr(ip, hashLog, mls);
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
        uint btLow = btMask >= dictHighLimit - dictLowLimit ? dictLowLimit : dictHighLimit - btMask;
        nuint commonLengthSmaller = 0,
            commonLengthLarger = 0;
        assert(dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState);
        for (; nbCompares != 0 && dictMatchIndex > dictLowLimit; --nbCompares)
        {
            uint* nextPtr = dictBt + 2 * (dictMatchIndex & btMask);
            /* guaranteed minimum nb of common bytes */
            nuint matchLength =
                commonLengthSmaller < commonLengthLarger ? commonLengthSmaller : commonLengthLarger;
            byte* match = dictBase + dictMatchIndex;
            matchLength += ZSTD_count_2segments(
                ip + matchLength,
                match + matchLength,
                iend,
                dictEnd,
                prefixStart
            );
            if (dictMatchIndex + matchLength >= dictHighLimit)
                match = @base + dictMatchIndex + dictIndexDelta;
            if (matchLength > bestLength)
            {
                uint matchIndex = dictMatchIndex + dictIndexDelta;
                if (
                    4 * (int)(matchLength - bestLength)
                    > (int)(
                        ZSTD_highbit32(curr - matchIndex + 1)
                        - ZSTD_highbit32((uint)offsetPtr[0] + 1)
                    )
                )
                {
                    bestLength = matchLength;
                    assert(curr - matchIndex > 0);
                    *offsetPtr = curr - matchIndex + 3;
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
            assert(*offsetPtr > 3);
            uint mIndex = curr - (uint)(*offsetPtr - 3);
        }

        return bestLength;
    }

    private static nuint ZSTD_DUBT_findBestMatch(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iend,
        nuint* offBasePtr,
        uint mls,
        ZSTD_dictMode_e dictMode
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hashLog = cParams->hashLog;
        nuint h = ZSTD_hashPtr(ip, hashLog, mls);
        uint matchIndex = hashTable[h];
        byte* @base = ms->window.@base;
        uint curr = (uint)(ip - @base);
        uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, cParams->windowLog);
        uint* bt = ms->chainTable;
        uint btLog = cParams->chainLog - 1;
        uint btMask = (uint)((1 << (int)btLog) - 1);
        uint btLow = btMask >= curr ? 0 : curr - btMask;
        uint unsortLimit = btLow > windowLow ? btLow : windowLow;
        uint* nextCandidate = bt + 2 * (matchIndex & btMask);
        uint* unsortedMark = bt + 2 * (matchIndex & btMask) + 1;
        uint nbCompares = 1U << (int)cParams->searchLog;
        uint nbCandidates = nbCompares;
        uint previousCandidate = 0;
        assert(ip <= iend - 8);
        assert(dictMode != ZSTD_dictMode_e.ZSTD_dedicatedDictSearch);
        while (matchIndex > unsortLimit && *unsortedMark == 1 && nbCandidates > 1)
        {
            *unsortedMark = previousCandidate;
            previousCandidate = matchIndex;
            matchIndex = *nextCandidate;
            nextCandidate = bt + 2 * (matchIndex & btMask);
            unsortedMark = bt + 2 * (matchIndex & btMask) + 1;
            nbCandidates--;
        }

        if (matchIndex > unsortLimit && *unsortedMark == 1)
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
            nuint commonLengthSmaller = 0,
                commonLengthLarger = 0;
            byte* dictBase = ms->window.dictBase;
            uint dictLimit = ms->window.dictLimit;
            byte* dictEnd = dictBase + dictLimit;
            byte* prefixStart = @base + dictLimit;
            uint* smallerPtr = bt + 2 * (curr & btMask);
            uint* largerPtr = bt + 2 * (curr & btMask) + 1;
            uint matchEndIdx = curr + 8 + 1;
            /* to be nullified at the end */
            uint dummy32;
            nuint bestLength = 0;
            matchIndex = hashTable[h];
            hashTable[h] = curr;
            for (; nbCompares != 0 && matchIndex > windowLow; --nbCompares)
            {
                uint* nextPtr = bt + 2 * (matchIndex & btMask);
                /* guaranteed minimum nb of common bytes */
                nuint matchLength =
                    commonLengthSmaller < commonLengthLarger
                        ? commonLengthSmaller
                        : commonLengthLarger;
                byte* match;
                if (
                    dictMode != ZSTD_dictMode_e.ZSTD_extDict
                    || matchIndex + matchLength >= dictLimit
                )
                {
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
                    if (matchLength > matchEndIdx - matchIndex)
                        matchEndIdx = matchIndex + (uint)matchLength;
                    if (
                        4 * (int)(matchLength - bestLength)
                        > (int)(
                            ZSTD_highbit32(curr - matchIndex + 1)
                            - ZSTD_highbit32((uint)*offBasePtr)
                        )
                    )
                    {
                        bestLength = matchLength;
                        assert(curr - matchIndex > 0);
                        *offBasePtr = curr - matchIndex + 3;
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
            assert(nbCompares <= 1U << (sizeof(nuint) == 4 ? 30 : 31) - 1);
            if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && nbCompares != 0)
            {
                bestLength = ZSTD_DUBT_findBetterDictMatch(
                    ms,
                    ip,
                    iend,
                    offBasePtr,
                    bestLength,
                    nbCompares,
                    mls,
                    dictMode
                );
            }

            assert(matchEndIdx > curr + 8);
            ms->nextToUpdate = matchEndIdx - 8;
            if (bestLength >= 3)
            {
                assert(*offBasePtr > 3);
                uint mIndex = curr - (uint)(*offBasePtr - 3);
            }

            return bestLength;
        }
    }

    /** ZSTD_BtFindBestMatch() : Tree updater, providing best match */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_BtFindBestMatch(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr,
        uint mls,
        ZSTD_dictMode_e dictMode
    )
    {
        if (ip < ms->window.@base + ms->nextToUpdate)
            return 0;
        ZSTD_updateDUBT(ms, ip, iLimit, mls);
        return ZSTD_DUBT_findBestMatch(ms, ip, iLimit, offBasePtr, mls, dictMode);
    }

    /***********************************
     * Dedicated dict search
     ***********************************/
    private static void ZSTD_dedicatedDictSearch_lazy_loadDictionary(
        ZSTD_MatchState_t* ms,
        byte* ip
    )
    {
        byte* @base = ms->window.@base;
        uint target = (uint)(ip - @base);
        uint* hashTable = ms->hashTable;
        uint* chainTable = ms->chainTable;
        uint chainSize = (uint)(1 << (int)ms->cParams.chainLog);
        uint idx = ms->nextToUpdate;
        uint minChain = chainSize < target - idx ? target - chainSize : idx;
        const uint bucketSize = 1 << 2;
        uint cacheSize = bucketSize - 1;
        uint chainAttempts = (uint)(1 << (int)ms->cParams.searchLog) - cacheSize;
        uint chainLimit = chainAttempts > 255 ? 255 : chainAttempts;
        /* We know the hashtable is oversized by a factor of `bucketSize`.
         * We are going to temporarily pretend `bucketSize == 1`, keeping only a
         * single entry. We will use the rest of the space to construct a temporary
         * chaintable.
         */
        uint hashLog = ms->cParams.hashLog - 2;
        uint* tmpHashTable = hashTable;
        uint* tmpChainTable = hashTable + ((nuint)1 << (int)hashLog);
        uint tmpChainSize = (uint)((1 << 2) - 1) << (int)hashLog;
        uint tmpMinChain = tmpChainSize < target ? target - tmpChainSize : idx;
        uint hashIdx;
        assert(ms->cParams.chainLog <= 24);
        assert(ms->cParams.hashLog > ms->cParams.chainLog);
        assert(idx != 0);
        assert(tmpMinChain <= minChain);
        for (; idx < target; idx++)
        {
            uint h = (uint)ZSTD_hashPtr(@base + idx, hashLog, ms->cParams.minMatch);
            if (idx >= tmpMinChain)
            {
                tmpChainTable[idx - tmpMinChain] = hashTable[h];
            }

            tmpHashTable[h] = idx;
        }

        {
            uint chainPos = 0;
            for (hashIdx = 0; hashIdx < 1U << (int)hashLog; hashIdx++)
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
                    for (count = 0; count < chainLimit; )
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
                    tmpHashTable[hashIdx] = (chainPos - count << 8) + count;
                }
                else
                {
                    tmpHashTable[hashIdx] = 0;
                }
            }

            assert(chainPos <= chainSize);
        }

        for (hashIdx = (uint)(1 << (int)hashLog); hashIdx != 0; )
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
            uint h = (uint)ZSTD_hashPtr(@base + idx, hashLog, ms->cParams.minMatch) << 2;
            uint i;
            for (i = cacheSize - 1; i != 0; i--)
                hashTable[h + i] = hashTable[h + i - 1];
            hashTable[h] = idx;
        }

        ms->nextToUpdate = target;
    }

    /* Returns the longest match length found in the dedicated dict search structure.
     * If none are longer than the argument ml, then ml will be returned.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_dedicatedDictSearch_lazy_search(
        nuint* offsetPtr,
        nuint ml,
        uint nbAttempts,
        ZSTD_MatchState_t* dms,
        byte* ip,
        byte* iLimit,
        byte* prefixStart,
        uint curr,
        uint dictLimit,
        nuint ddsIdx
    )
    {
        uint ddsLowestIndex = dms->window.dictLimit;
        byte* ddsBase = dms->window.@base;
        byte* ddsEnd = dms->window.nextSrc;
        uint ddsSize = (uint)(ddsEnd - ddsBase);
        uint ddsIndexDelta = dictLimit - ddsSize;
        const uint bucketSize = 1 << 2;
        uint bucketLimit = nbAttempts < bucketSize - 1 ? nbAttempts : bucketSize - 1;
        uint ddsAttempt;
        uint matchIndex;
        for (ddsAttempt = 0; ddsAttempt < bucketSize - 1; ddsAttempt++)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Sse.IsSupported)
            {
                Sse.Prefetch0(ddsBase + dms->hashTable[ddsIdx + ddsAttempt]);
            }
#endif
        }

        {
            uint chainPackedPointer = dms->hashTable[ddsIdx + bucketSize - 1];
            uint chainIndex = chainPackedPointer >> 8;
#if NETCOREAPP3_0_OR_GREATER
            if (Sse.IsSupported)
            {
                Sse.Prefetch0(&dms->chainTable[chainIndex]);
            }
#endif
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
            if (MEM_read32(match) == MEM_read32(ip))
            {
                currentMl =
                    ZSTD_count_2segments(ip + 4, match + 4, iLimit, ddsEnd, prefixStart) + 4;
            }

            if (currentMl > ml)
            {
                ml = currentMl;
                assert(curr - (matchIndex + ddsIndexDelta) > 0);
                *offsetPtr = curr - (matchIndex + ddsIndexDelta) + 3;
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
#if NETCOREAPP3_0_OR_GREATER
                if (Sse.IsSupported)
                {
                    Sse.Prefetch0(ddsBase + dms->chainTable[chainIndex + chainAttempt]);
                }
#endif
            }

            for (chainAttempt = 0; chainAttempt < chainLimit; chainAttempt++, chainIndex++)
            {
                nuint currentMl = 0;
                byte* match;
                matchIndex = dms->chainTable[chainIndex];
                match = ddsBase + matchIndex;
                assert(matchIndex >= ddsLowestIndex);
                assert(match + 4 <= ddsEnd);
                if (MEM_read32(match) == MEM_read32(ip))
                {
                    currentMl =
                        ZSTD_count_2segments(ip + 4, match + 4, iLimit, ddsEnd, prefixStart) + 4;
                }

                if (currentMl > ml)
                {
                    ml = currentMl;
                    assert(curr - (matchIndex + ddsIndexDelta) > 0);
                    *offsetPtr = curr - (matchIndex + ddsIndexDelta) + 3;
                    if (ip + currentMl == iLimit)
                        break;
                }
            }
        }

        return ml;
    }

    /* Update chains up to ip (excluded)
    Assumption : always within prefix (i.e. not within extDict) */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_insertAndFindFirstIndex_internal(
        ZSTD_MatchState_t* ms,
        ZSTD_compressionParameters* cParams,
        byte* ip,
        uint mls,
        uint lazySkipping
    )
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
            nuint h = ZSTD_hashPtr(@base + idx, hashLog, mls);
            chainTable[idx & chainMask] = hashTable[h];
            hashTable[h] = idx;
            idx++;
            if (lazySkipping != 0)
                break;
        }

        ms->nextToUpdate = target;
        return hashTable[ZSTD_hashPtr(ip, hashLog, mls)];
    }

    private static uint ZSTD_insertAndFindFirstIndex(ZSTD_MatchState_t* ms, byte* ip)
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        return ZSTD_insertAndFindFirstIndex_internal(ms, cParams, ip, ms->cParams.minMatch, 0);
    }

    /* inlining is important to hardwire a hot branch (template emulation) */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_HcFindBestMatch(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr,
        uint mls,
        ZSTD_dictMode_e dictMode
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* chainTable = ms->chainTable;
        uint chainSize = (uint)(1 << (int)cParams->chainLog);
        uint chainMask = chainSize - 1;
        byte* @base = ms->window.@base;
        byte* dictBase = ms->window.dictBase;
        uint dictLimit = ms->window.dictLimit;
        byte* prefixStart = @base + dictLimit;
        byte* dictEnd = dictBase + dictLimit;
        uint curr = (uint)(ip - @base);
        uint maxDistance = 1U << (int)cParams->windowLog;
        uint lowestValid = ms->window.lowLimit;
        uint withinMaxDistance =
            curr - lowestValid > maxDistance ? curr - maxDistance : lowestValid;
        uint isDictionary = ms->loadedDictEnd != 0 ? 1U : 0U;
        uint lowLimit = isDictionary != 0 ? lowestValid : withinMaxDistance;
        uint minChain = curr > chainSize ? curr - chainSize : 0;
        uint nbAttempts = 1U << (int)cParams->searchLog;
        nuint ml = 4 - 1;
        ZSTD_MatchState_t* dms = ms->dictMatchState;
        uint ddsHashLog =
            dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch ? dms->cParams.hashLog - 2 : 0;
        nuint ddsIdx =
            dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
                ? ZSTD_hashPtr(ip, ddsHashLog, mls) << 2
                : 0;
        uint matchIndex;
        if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
        {
            uint* entry = &dms->hashTable[ddsIdx];
#if NETCOREAPP3_0_OR_GREATER
            if (Sse.IsSupported)
            {
                Sse.Prefetch0(entry);
            }
#endif
        }

        matchIndex = ZSTD_insertAndFindFirstIndex_internal(
            ms,
            cParams,
            ip,
            mls,
            (uint)ms->lazySkipping
        );
        for (; matchIndex >= lowLimit && nbAttempts > 0; nbAttempts--)
        {
            nuint currentMl = 0;
            if (dictMode != ZSTD_dictMode_e.ZSTD_extDict || matchIndex >= dictLimit)
            {
                byte* match = @base + matchIndex;
                assert(matchIndex >= dictLimit);
                if (MEM_read32(match + ml - 3) == MEM_read32(ip + ml - 3))
                    currentMl = ZSTD_count(ip, match, iLimit);
            }
            else
            {
                byte* match = dictBase + matchIndex;
                assert(match + 4 <= dictEnd);
                if (MEM_read32(match) == MEM_read32(ip))
                    currentMl =
                        ZSTD_count_2segments(ip + 4, match + 4, iLimit, dictEnd, prefixStart) + 4;
            }

            if (currentMl > ml)
            {
                ml = currentMl;
                assert(curr - matchIndex > 0);
                *offsetPtr = curr - matchIndex + 3;
                if (ip + currentMl == iLimit)
                    break;
            }

            if (matchIndex <= minChain)
                break;
            matchIndex = chainTable[matchIndex & chainMask];
        }

        assert(nbAttempts <= 1U << (sizeof(nuint) == 4 ? 30 : 31) - 1);
        if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
        {
            ml = ZSTD_dedicatedDictSearch_lazy_search(
                offsetPtr,
                ml,
                nbAttempts,
                dms,
                ip,
                iLimit,
                prefixStart,
                curr,
                dictLimit,
                ddsIdx
            );
        }
        else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
        {
            uint* dmsChainTable = dms->chainTable;
            uint dmsChainSize = (uint)(1 << (int)dms->cParams.chainLog);
            uint dmsChainMask = dmsChainSize - 1;
            uint dmsLowestIndex = dms->window.dictLimit;
            byte* dmsBase = dms->window.@base;
            byte* dmsEnd = dms->window.nextSrc;
            uint dmsSize = (uint)(dmsEnd - dmsBase);
            uint dmsIndexDelta = dictLimit - dmsSize;
            uint dmsMinChain = dmsSize > dmsChainSize ? dmsSize - dmsChainSize : 0;
            matchIndex = dms->hashTable[ZSTD_hashPtr(ip, dms->cParams.hashLog, mls)];
            for (; matchIndex >= dmsLowestIndex && nbAttempts > 0; nbAttempts--)
            {
                nuint currentMl = 0;
                byte* match = dmsBase + matchIndex;
                assert(match + 4 <= dmsEnd);
                if (MEM_read32(match) == MEM_read32(ip))
                    currentMl =
                        ZSTD_count_2segments(ip + 4, match + 4, iLimit, dmsEnd, prefixStart) + 4;
                if (currentMl > ml)
                {
                    ml = currentMl;
                    assert(curr > matchIndex + dmsIndexDelta);
                    assert(curr - (matchIndex + dmsIndexDelta) > 0);
                    *offsetPtr = curr - (matchIndex + dmsIndexDelta) + 3;
                    if (ip + currentMl == iLimit)
                        break;
                }

                if (matchIndex <= dmsMinChain)
                    break;
                matchIndex = dmsChainTable[matchIndex & dmsChainMask];
            }
        }

        return ml;
    }

    /* ZSTD_VecMask_next():
     * Starting from the LSB, returns the idx of the next non-zero bit.
     * Basically counting the nb of trailing zeroes.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_VecMask_next(ulong val)
    {
        assert(val != 0);
        return (uint)BitOperations.TrailingZeroCount(val);
    }

    /* ZSTD_row_nextIndex():
     * Returns the next index to insert at within a tagTable row, and updates the "head"
     * value to reflect the update. Essentially cycles backwards from [1, {entries per row})
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_row_nextIndex(byte* tagRow, uint rowMask)
    {
        uint next = (uint)(*tagRow - 1) & rowMask;
        next += next == 0 ? rowMask : 0;
        *tagRow = (byte)next;
        return next;
    }

    /* ZSTD_isAligned():
     * Checks that a pointer is aligned to "align" bytes which must be a power of 2.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_isAligned(void* ptr, nuint align)
    {
        assert((align & align - 1) == 0);
        return ((nuint)ptr & align - 1) == 0 ? 1 : 0;
    }

    /* ZSTD_row_prefetch():
     * Performs prefetching for the hashTable and tagTable at a given row.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_row_prefetch(uint* hashTable, byte* tagTable, uint relRow, uint rowLog)
    {
#if NETCOREAPP3_0_OR_GREATER
        if (Sse.IsSupported)
        {
            Sse.Prefetch0(hashTable + relRow);
        }
#endif

        if (rowLog >= 5)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Sse.IsSupported)
            {
                Sse.Prefetch0(hashTable + relRow + 16);
            }
#endif
        }

#if NETCOREAPP3_0_OR_GREATER
        if (Sse.IsSupported)
        {
            Sse.Prefetch0(tagTable + relRow);
        }
#endif

        if (rowLog == 6)
        {
#if NETCOREAPP3_0_OR_GREATER
            if (Sse.IsSupported)
            {
                Sse.Prefetch0(tagTable + relRow + 32);
            }
#endif
        }

        assert(rowLog == 4 || rowLog == 5 || rowLog == 6);
        assert(ZSTD_isAligned(hashTable + relRow, 64) != 0);
        assert(ZSTD_isAligned(tagTable + relRow, (nuint)1 << (int)rowLog) != 0);
    }

    /* ZSTD_row_fillHashCache():
     * Fill up the hash cache starting at idx, prefetching up to ZSTD_ROW_HASH_CACHE_SIZE entries,
     * but not beyond iLimit.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_row_fillHashCache(
        ZSTD_MatchState_t* ms,
        byte* @base,
        uint rowLog,
        uint mls,
        uint idx,
        byte* iLimit
    )
    {
        uint* hashTable = ms->hashTable;
        byte* tagTable = ms->tagTable;
        uint hashLog = ms->rowHashLog;
        uint maxElemsToPrefetch = @base + idx > iLimit ? 0 : (uint)(iLimit - (@base + idx) + 1);
        uint lim = idx + (8 < maxElemsToPrefetch ? 8 : maxElemsToPrefetch);
        for (; idx < lim; ++idx)
        {
            uint hash = (uint)ZSTD_hashPtrSalted(@base + idx, hashLog + 8, mls, ms->hashSalt);
            uint row = hash >> 8 << (int)rowLog;
            ZSTD_row_prefetch(hashTable, tagTable, row, rowLog);
            ms->hashCache[idx & 8 - 1] = hash;
        }
    }

    /* ZSTD_row_nextCachedHash():
     * Returns the hash of base + idx, and replaces the hash in the hash cache with the byte at
     * base + idx + ZSTD_ROW_HASH_CACHE_SIZE. Also prefetches the appropriate rows from hashTable and tagTable.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_row_nextCachedHash(
        uint* cache,
        uint* hashTable,
        byte* tagTable,
        byte* @base,
        uint idx,
        uint hashLog,
        uint rowLog,
        uint mls,
        ulong hashSalt
    )
    {
        uint newHash = (uint)ZSTD_hashPtrSalted(@base + idx + 8, hashLog + 8, mls, hashSalt);
        uint row = newHash >> 8 << (int)rowLog;
        ZSTD_row_prefetch(hashTable, tagTable, row, rowLog);
        {
            uint hash = cache[idx & 8 - 1];
            cache[idx & 8 - 1] = newHash;
            return hash;
        }
    }

    /* ZSTD_row_update_internalImpl():
     * Updates the hash table with positions starting from updateStartIdx until updateEndIdx.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_row_update_internalImpl(
        ZSTD_MatchState_t* ms,
        uint updateStartIdx,
        uint updateEndIdx,
        uint mls,
        uint rowLog,
        uint rowMask,
        uint useCache
    )
    {
        uint* hashTable = ms->hashTable;
        byte* tagTable = ms->tagTable;
        uint hashLog = ms->rowHashLog;
        byte* @base = ms->window.@base;
        for (; updateStartIdx < updateEndIdx; ++updateStartIdx)
        {
            uint hash =
                useCache != 0
                    ? ZSTD_row_nextCachedHash(
                        ms->hashCache,
                        hashTable,
                        tagTable,
                        @base,
                        updateStartIdx,
                        hashLog,
                        rowLog,
                        mls,
                        ms->hashSalt
                    )
                    : (uint)ZSTD_hashPtrSalted(
                        @base + updateStartIdx,
                        hashLog + 8,
                        mls,
                        ms->hashSalt
                    );
            uint relRow = hash >> 8 << (int)rowLog;
            uint* row = hashTable + relRow;
            byte* tagRow = tagTable + relRow;
            uint pos = ZSTD_row_nextIndex(tagRow, rowMask);
            assert(
                hash == ZSTD_hashPtrSalted(@base + updateStartIdx, hashLog + 8, mls, ms->hashSalt)
            );
            tagRow[pos] = (byte)(hash & (1U << 8) - 1);
            row[pos] = updateStartIdx;
        }
    }

    /* ZSTD_row_update_internal():
     * Inserts the byte at ip into the appropriate position in the hash table, and updates ms->nextToUpdate.
     * Skips sections of long matches as is necessary.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_row_update_internal(
        ZSTD_MatchState_t* ms,
        byte* ip,
        uint mls,
        uint rowLog,
        uint rowMask,
        uint useCache
    )
    {
        uint idx = ms->nextToUpdate;
        byte* @base = ms->window.@base;
        uint target = (uint)(ip - @base);
        const uint kSkipThreshold = 384;
        const uint kMaxMatchStartPositionsToUpdate = 96;
        const uint kMaxMatchEndPositionsToUpdate = 32;
        if (useCache != 0)
        {
            if (target - idx > kSkipThreshold)
            {
                uint bound = idx + kMaxMatchStartPositionsToUpdate;
                ZSTD_row_update_internalImpl(ms, idx, bound, mls, rowLog, rowMask, useCache);
                idx = target - kMaxMatchEndPositionsToUpdate;
                ZSTD_row_fillHashCache(ms, @base, rowLog, mls, idx, ip + 1);
            }
        }

        assert(target >= idx);
        ZSTD_row_update_internalImpl(ms, idx, target, mls, rowLog, rowMask, useCache);
        ms->nextToUpdate = target;
    }

    /* ZSTD_row_update():
     * External wrapper for ZSTD_row_update_internal(). Used for filling the hashtable during dictionary
     * processing.
     */
    private static void ZSTD_row_update(ZSTD_MatchState_t* ms, byte* ip)
    {
        uint rowLog =
            ms->cParams.searchLog <= 4 ? 4
            : ms->cParams.searchLog <= 6 ? ms->cParams.searchLog
            : 6;
        uint rowMask = (1U << (int)rowLog) - 1;
        /* mls caps out at 6 */
        uint mls = ms->cParams.minMatch < 6 ? ms->cParams.minMatch : 6;
        ZSTD_row_update_internal(ms, ip, mls, rowLog, rowMask, 0);
    }

    /* Returns the mask width of bits group of which will be set to 1. Given not all
     * architectures have easy movemask instruction, this helps to iterate over
     * groups of bits easier and faster.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZSTD_row_matchMaskGroupWidth(uint rowEntries)
    {
        assert(rowEntries == 16 || rowEntries == 32 || rowEntries == 64);
        assert(rowEntries <= 64);
#if NET5_0_OR_GREATER
        if (AdvSimd.IsSupported && BitConverter.IsLittleEndian)
        {
            if (rowEntries == 16)
                return 4;
#if NET9_0_OR_GREATER
            if (AdvSimd.Arm64.IsSupported)
            {
                if (rowEntries == 32)
                    return 2;
                if (rowEntries == 64)
                    return 1;
            }
#endif
        }
#endif
        return 1;
    }

#if NETCOREAPP3_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ZSTD_row_getSSEMask(int nbChunks, byte* src, byte tag, uint head)
    {
        Vector128<byte> comparisonMask = Vector128.Create(tag);
        assert(nbChunks is 1 or 2 or 4);
        if (nbChunks == 1)
        {
            Vector128<byte> chunk0 = Sse2.LoadVector128(src);
            Vector128<byte> equalMask0 = Sse2.CompareEqual(chunk0, comparisonMask);
            int matches0 = Sse2.MoveMask(equalMask0);
            return BitOperations.RotateRight((ushort)matches0, (int)head);
        }

        if (nbChunks == 2)
        {
            Vector128<byte> chunk0 = Sse2.LoadVector128(src);
            Vector128<byte> equalMask0 = Sse2.CompareEqual(chunk0, comparisonMask);
            int matches0 = Sse2.MoveMask(equalMask0);
            Vector128<byte> chunk1 = Sse2.LoadVector128(src + 16);
            Vector128<byte> equalMask1 = Sse2.CompareEqual(chunk1, comparisonMask);
            int matches1 = Sse2.MoveMask(equalMask1);
            return BitOperations.RotateRight((uint)matches1 << 16 | (uint)matches0, (int)head);
        }

        {
            Vector128<byte> chunk0 = Sse2.LoadVector128(src);
            Vector128<byte> equalMask0 = Sse2.CompareEqual(chunk0, comparisonMask);
            int matches0 = Sse2.MoveMask(equalMask0);
            Vector128<byte> chunk1 = Sse2.LoadVector128(src + 16 * 1);
            Vector128<byte> equalMask1 = Sse2.CompareEqual(chunk1, comparisonMask);
            int matches1 = Sse2.MoveMask(equalMask1);
            Vector128<byte> chunk2 = Sse2.LoadVector128(src + 16 * 2);
            Vector128<byte> equalMask2 = Sse2.CompareEqual(chunk2, comparisonMask);
            int matches2 = Sse2.MoveMask(equalMask2);
            Vector128<byte> chunk3 = Sse2.LoadVector128(src + 16 * 3);
            Vector128<byte> equalMask3 = Sse2.CompareEqual(chunk3, comparisonMask);
            int matches3 = Sse2.MoveMask(equalMask3);
            return BitOperations.RotateRight(
                (ulong)matches3 << 48
                    | (ulong)matches2 << 32
                    | (ulong)matches1 << 16
                    | (uint)matches0,
                (int)head
            );
        }
    }
#endif

    /* Returns a ZSTD_VecMask (U64) that has the nth group (determined by
     * ZSTD_row_matchMaskGroupWidth) of bits set to 1 if the newly-computed "tag"
     * matches the hash at the nth position in a row of the tagTable.
     * Each row is a circular buffer beginning at the value of "headGrouped". So we
     * must rotate the "matches" bitfield to match up with the actual layout of the
     * entries within the hashTable */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ZSTD_row_getMatchMask(
        byte* tagRow,
        byte tag,
        uint headGrouped,
        uint rowEntries
    )
    {
        byte* src = tagRow;
        assert(rowEntries == 16 || rowEntries == 32 || rowEntries == 64);
        assert(rowEntries <= 64);
        assert(ZSTD_row_matchMaskGroupWidth(rowEntries) * rowEntries <= sizeof(ulong) * 8);
#if NETCOREAPP3_0_OR_GREATER
        if (Sse2.IsSupported)
        {
            return ZSTD_row_getSSEMask((int)(rowEntries / 16), src, tag, headGrouped);
        }
#endif

#if NET5_0_OR_GREATER
        if (AdvSimd.IsSupported && BitConverter.IsLittleEndian)
        {
            if (rowEntries == 16)
            {
                /* vshrn_n_u16 shifts by 4 every u16 and narrows to 8 lower bits.
                 * After that groups of 4 bits represent the equalMask. We lower
                 * all bits except the highest in these groups by doing AND with
                 * 0x88 = 0b10001000.
                 */
                Vector128<byte> chunk = AdvSimd.LoadVector128(src);
                Vector128<ushort> equalMask = AdvSimd
                    .CompareEqual(chunk, AdvSimd.DuplicateToVector128(tag))
                    .As<byte, ushort>();
                Vector64<byte> res = AdvSimd.ShiftRightLogicalNarrowingLower(equalMask, 4);
                ulong matches = res.As<byte, ulong>().GetElement(0);
                return BitOperations.RotateRight(matches, (int)headGrouped) & 0x8888888888888888;
            }
            else if (rowEntries == 32)
            {
#if NET9_0_OR_GREATER
                if (AdvSimd.Arm64.IsSupported)
                {
                    /* Same idea as with rowEntries == 16 but doing AND with
                     * 0x55 = 0b01010101.
                     */
                    (Vector128<ushort> chunk0, Vector128<ushort> chunk1) =
                        AdvSimd.Arm64.Load2xVector128AndUnzip((ushort*)src);
                    Vector128<byte> dup = AdvSimd.DuplicateToVector128(tag);
                    Vector64<byte> t0 = AdvSimd.ShiftRightLogicalNarrowingLower(
                        AdvSimd.CompareEqual(chunk0.As<ushort, byte>(), dup).As<byte, ushort>(),
                        6
                    );
                    Vector64<byte> t1 = AdvSimd.ShiftRightLogicalNarrowingLower(
                        AdvSimd.CompareEqual(chunk1.As<ushort, byte>(), dup).As<byte, ushort>(),
                        6
                    );
                    Vector64<byte> res = AdvSimd.ShiftLeftAndInsert(t0, t1, 4);
                    ulong matches = res.As<byte, ulong>().GetElement(0);
                    return BitOperations.RotateRight(matches, (int)headGrouped)
                        & 0x5555555555555555;
                }
#endif
            }
            else
            { /* rowEntries == 64 */
#if NET9_0_OR_GREATER
                if (AdvSimd.Arm64.IsSupported)
                {
                    (
                        Vector128<byte> chunk0,
                        Vector128<byte> chunk1,
                        Vector128<byte> chunk2,
                        Vector128<byte> chunk3
                    ) = AdvSimd.Arm64.Load4xVector128AndUnzip(src);
                    Vector128<byte> dup = AdvSimd.DuplicateToVector128(tag);
                    Vector128<byte> cmp0 = AdvSimd.CompareEqual(chunk0, dup);
                    Vector128<byte> cmp1 = AdvSimd.CompareEqual(chunk1, dup);
                    Vector128<byte> cmp2 = AdvSimd.CompareEqual(chunk2, dup);
                    Vector128<byte> cmp3 = AdvSimd.CompareEqual(chunk3, dup);

                    Vector128<byte> t0 = AdvSimd.ShiftRightAndInsert(cmp1, cmp0, 1);
                    Vector128<byte> t1 = AdvSimd.ShiftRightAndInsert(cmp3, cmp2, 1);
                    Vector128<byte> t2 = AdvSimd.ShiftRightAndInsert(t1, t0, 2);
                    Vector128<byte> t3 = AdvSimd.ShiftRightAndInsert(t2, t2, 4);
                    Vector64<byte> t4 = AdvSimd.ShiftRightLogicalNarrowingLower(
                        t3.As<byte, ushort>(),
                        4
                    );
                    ulong matches = t4.As<byte, ulong>().GetElement(0);
                    return BitOperations.RotateRight(matches, (int)headGrouped);
                }
#endif
            }
        }
#endif

        {
            nuint chunkSize = (nuint)sizeof(nuint);
            nuint shiftAmount = chunkSize * 8 - chunkSize;
            nuint xFF = ~(nuint)0;
            nuint x01 = xFF / 0xFF;
            nuint x80 = x01 << 7;
            nuint splatChar = tag * x01;
            ulong matches = 0;
            int i = (int)(rowEntries - chunkSize);
            assert(sizeof(nuint) == 4 || sizeof(nuint) == 8);
            if (BitConverter.IsLittleEndian)
            {
                nuint extractMagic = xFF / 0x7F >> (int)chunkSize;
                do
                {
                    nuint chunk = MEM_readST(&src[i]);
                    chunk ^= splatChar;
                    chunk = ((chunk | x80) - x01 | chunk) & x80;
                    matches <<= (int)chunkSize;
                    matches |= chunk * extractMagic >> (int)shiftAmount;
                    i -= (int)chunkSize;
                } while (i >= 0);
            }
            else
            {
                nuint msb = xFF ^ xFF >> 1;
                nuint extractMagic = msb / 0x1FF | msb;
                do
                {
                    nuint chunk = MEM_readST(&src[i]);
                    chunk ^= splatChar;
                    chunk = ((chunk | x80) - x01 | chunk) & x80;
                    matches <<= (int)chunkSize;
                    matches |= (chunk >> 7) * extractMagic >> (int)shiftAmount;
                    i -= (int)chunkSize;
                } while (i >= 0);
            }

            matches = ~matches;
            if (rowEntries == 16)
            {
                return BitOperations.RotateRight((ushort)matches, (int)headGrouped);
            }
            else if (rowEntries == 32)
            {
                return BitOperations.RotateRight((uint)matches, (int)headGrouped);
            }
            else
            {
                return BitOperations.RotateRight(matches, (int)headGrouped);
            }
        }
    }

    /* The high-level approach of the SIMD row based match finder is as follows:
     * - Figure out where to insert the new entry:
     *      - Generate a hash for current input position and split it into a one byte of tag and `rowHashLog` bits of index.
     *           - The hash is salted by a value that changes on every context reset, so when the same table is used
     *             we will avoid collisions that would otherwise slow us down by introducing phantom matches.
     *      - The hashTable is effectively split into groups or "rows" of 15 or 31 entries of U32, and the index determines
     *        which row to insert into.
     *      - Determine the correct position within the row to insert the entry into. Each row of 15 or 31 can
     *        be considered as a circular buffer with a "head" index that resides in the tagTable (overall 16 or 32 bytes
     *        per row).
     * - Use SIMD to efficiently compare the tags in the tagTable to the 1-byte tag calculated for the position and
     *   generate a bitfield that we can cycle through to check the collisions in the hash table.
     * - Pick the longest match.
     * - Insert the tag into the equivalent row and position in the tagTable.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_RowFindBestMatch(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr,
        uint mls,
        ZSTD_dictMode_e dictMode,
        uint rowLog
    )
    {
        uint* hashTable = ms->hashTable;
        byte* tagTable = ms->tagTable;
        uint* hashCache = ms->hashCache;
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
        uint withinMaxDistance =
            curr - lowestValid > maxDistance ? curr - maxDistance : lowestValid;
        uint isDictionary = ms->loadedDictEnd != 0 ? 1U : 0U;
        uint lowLimit = isDictionary != 0 ? lowestValid : withinMaxDistance;
        uint rowEntries = 1U << (int)rowLog;
        uint rowMask = rowEntries - 1;
        /* nb of searches is capped at nb entries per row */
        uint cappedSearchLog = cParams->searchLog < rowLog ? cParams->searchLog : rowLog;
        uint groupWidth = ZSTD_row_matchMaskGroupWidth(rowEntries);
        ulong hashSalt = ms->hashSalt;
        uint nbAttempts = 1U << (int)cappedSearchLog;
        nuint ml = 4 - 1;
        uint hash;
        /* DMS/DDS variables that may be referenced laster */
        ZSTD_MatchState_t* dms = ms->dictMatchState;
        /* Initialize the following variables to satisfy static analyzer */
        nuint ddsIdx = 0;
        /* cctx hash tables are limited in searches, but allow extra searches into DDS */
        uint ddsExtraAttempts = 0;
        uint dmsTag = 0;
        uint* dmsRow = null;
        byte* dmsTagRow = null;
        if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
        {
            uint ddsHashLog = dms->cParams.hashLog - 2;
            {
                ddsIdx = ZSTD_hashPtr(ip, ddsHashLog, mls) << 2;
#if NETCOREAPP3_0_OR_GREATER
                if (Sse.IsSupported)
                {
                    Sse.Prefetch0(&dms->hashTable[ddsIdx]);
                }
#endif
            }

            ddsExtraAttempts =
                cParams->searchLog > rowLog ? 1U << (int)(cParams->searchLog - rowLog) : 0;
        }

        if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
        {
            /* Prefetch DMS rows */
            uint* dmsHashTable = dms->hashTable;
            byte* dmsTagTable = dms->tagTable;
            uint dmsHash = (uint)ZSTD_hashPtr(ip, dms->rowHashLog + 8, mls);
            uint dmsRelRow = dmsHash >> 8 << (int)rowLog;
            dmsTag = dmsHash & (1U << 8) - 1;
            dmsTagRow = dmsTagTable + dmsRelRow;
            dmsRow = dmsHashTable + dmsRelRow;
            ZSTD_row_prefetch(dmsHashTable, dmsTagTable, dmsRelRow, rowLog);
        }

        if (ms->lazySkipping == 0)
        {
            ZSTD_row_update_internal(ms, ip, mls, rowLog, rowMask, 1);
            hash = ZSTD_row_nextCachedHash(
                hashCache,
                hashTable,
                tagTable,
                @base,
                curr,
                hashLog,
                rowLog,
                mls,
                hashSalt
            );
        }
        else
        {
            hash = (uint)ZSTD_hashPtrSalted(ip, hashLog + 8, mls, hashSalt);
            ms->nextToUpdate = curr;
        }

        ms->hashSaltEntropy += hash;
        {
            uint relRow = hash >> 8 << (int)rowLog;
            uint tag = hash & (1U << 8) - 1;
            uint* row = hashTable + relRow;
            byte* tagRow = tagTable + relRow;
            uint headGrouped = (*tagRow & rowMask) * groupWidth;
            uint* matchBuffer = stackalloc uint[64];
            nuint numMatches = 0;
            nuint currMatch = 0;
            ulong matches = ZSTD_row_getMatchMask(tagRow, (byte)tag, headGrouped, rowEntries);
            for (; matches > 0 && nbAttempts > 0; matches &= matches - 1)
            {
                uint matchPos = (headGrouped + ZSTD_VecMask_next(matches)) / groupWidth & rowMask;
                uint matchIndex = row[matchPos];
                if (matchPos == 0)
                    continue;
                assert(numMatches < rowEntries);
                if (matchIndex < lowLimit)
                    break;
                if (dictMode != ZSTD_dictMode_e.ZSTD_extDict || matchIndex >= dictLimit)
                {
#if NETCOREAPP3_0_OR_GREATER
                    if (Sse.IsSupported)
                    {
                        Sse.Prefetch0(@base + matchIndex);
                    }
#endif
                }
                else
                {
#if NETCOREAPP3_0_OR_GREATER
                    if (Sse.IsSupported)
                    {
                        Sse.Prefetch0(dictBase + matchIndex);
                    }
#endif
                }

                matchBuffer[numMatches++] = matchIndex;
                --nbAttempts;
            }

            {
                uint pos = ZSTD_row_nextIndex(tagRow, rowMask);
                tagRow[pos] = (byte)tag;
                row[pos] = ms->nextToUpdate++;
            }

            for (; currMatch < numMatches; ++currMatch)
            {
                uint matchIndex = matchBuffer[currMatch];
                nuint currentMl = 0;
                assert(matchIndex < curr);
                assert(matchIndex >= lowLimit);
                if (dictMode != ZSTD_dictMode_e.ZSTD_extDict || matchIndex >= dictLimit)
                {
                    byte* match = @base + matchIndex;
                    assert(matchIndex >= dictLimit);
                    if (MEM_read32(match + ml - 3) == MEM_read32(ip + ml - 3))
                        currentMl = ZSTD_count(ip, match, iLimit);
                }
                else
                {
                    byte* match = dictBase + matchIndex;
                    assert(match + 4 <= dictEnd);
                    if (MEM_read32(match) == MEM_read32(ip))
                        currentMl =
                            ZSTD_count_2segments(ip + 4, match + 4, iLimit, dictEnd, prefixStart)
                            + 4;
                }

                if (currentMl > ml)
                {
                    ml = currentMl;
                    assert(curr - matchIndex > 0);
                    *offsetPtr = curr - matchIndex + 3;
                    if (ip + currentMl == iLimit)
                        break;
                }
            }
        }

        assert(nbAttempts <= 1U << (sizeof(nuint) == 4 ? 30 : 31) - 1);
        if (dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch)
        {
            ml = ZSTD_dedicatedDictSearch_lazy_search(
                offsetPtr,
                ml,
                nbAttempts + ddsExtraAttempts,
                dms,
                ip,
                iLimit,
                prefixStart,
                curr,
                dictLimit,
                ddsIdx
            );
        }
        else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
        {
            /* TODO: Measure and potentially add prefetching to DMS */
            uint dmsLowestIndex = dms->window.dictLimit;
            byte* dmsBase = dms->window.@base;
            byte* dmsEnd = dms->window.nextSrc;
            uint dmsSize = (uint)(dmsEnd - dmsBase);
            uint dmsIndexDelta = dictLimit - dmsSize;
            {
                uint headGrouped = (*dmsTagRow & rowMask) * groupWidth;
                uint* matchBuffer = stackalloc uint[64];
                nuint numMatches = 0;
                nuint currMatch = 0;
                ulong matches = ZSTD_row_getMatchMask(
                    dmsTagRow,
                    (byte)dmsTag,
                    headGrouped,
                    rowEntries
                );
                for (; matches > 0 && nbAttempts > 0; matches &= matches - 1)
                {
                    uint matchPos =
                        (headGrouped + ZSTD_VecMask_next(matches)) / groupWidth & rowMask;
                    uint matchIndex = dmsRow[matchPos];
                    if (matchPos == 0)
                        continue;
                    if (matchIndex < dmsLowestIndex)
                        break;
#if NETCOREAPP3_0_OR_GREATER
                    if (Sse.IsSupported)
                    {
                        Sse.Prefetch0(dmsBase + matchIndex);
                    }
#endif

                    matchBuffer[numMatches++] = matchIndex;
                    --nbAttempts;
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
                        if (MEM_read32(match) == MEM_read32(ip))
                            currentMl =
                                ZSTD_count_2segments(ip + 4, match + 4, iLimit, dmsEnd, prefixStart)
                                + 4;
                    }

                    if (currentMl > ml)
                    {
                        ml = currentMl;
                        assert(curr > matchIndex + dmsIndexDelta);
                        assert(curr - (matchIndex + dmsIndexDelta) > 0);
                        *offsetPtr = curr - (matchIndex + dmsIndexDelta) + 3;
                        if (ip + currentMl == iLimit)
                            break;
                    }
                }
            }
        }

        return ml;
    }

    /* Generate row search fns for each combination of (dictMode, mls, rowLog) */
    private static nuint ZSTD_RowFindBestMatch_noDict_4_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_noDict, 4);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_4_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_noDict, 5);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_4_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_noDict, 6);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_5_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_noDict, 4);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_5_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_noDict, 5);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_5_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_noDict, 6);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_6_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_noDict, 4);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_6_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_noDict, 5);
    }

    private static nuint ZSTD_RowFindBestMatch_noDict_6_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_noDict, 6);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_4_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_extDict, 4);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_4_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_extDict, 5);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_4_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_extDict, 6);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_5_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_extDict, 4);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_5_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_extDict, 5);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_5_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_extDict, 6);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_6_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_extDict, 4);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_6_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_extDict, 5);
    }

    private static nuint ZSTD_RowFindBestMatch_extDict_6_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_extDict, 6);
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_4_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            4
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_4_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            5
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_4_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            6
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_5_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            4
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_5_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            5
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_5_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            6
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_6_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            4
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_6_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            5
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dictMatchState_6_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dictMatchState,
            6
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_4_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            4
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_4_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            5
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_4_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            6
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_5_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            4
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_5_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            5
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_5_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            6
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_6_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 4
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            4
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_6_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 5
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            5
        );
    }

    private static nuint ZSTD_RowFindBestMatch_dedicatedDictSearch_6_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        assert(
            (
                4 > (6 < ms->cParams.searchLog ? 6 : ms->cParams.searchLog) ? 4
                : 6 < ms->cParams.searchLog ? 6
                : ms->cParams.searchLog
            ) == 6
        );
        return ZSTD_RowFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch,
            6
        );
    }

    /* Generate binary Tree search fns for each combination of (dictMode, mls) */
    private static nuint ZSTD_BtFindBestMatch_noDict_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_BtFindBestMatch(ms, ip, iLimit, offBasePtr, 4, ZSTD_dictMode_e.ZSTD_noDict);
    }

    private static nuint ZSTD_BtFindBestMatch_noDict_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_BtFindBestMatch(ms, ip, iLimit, offBasePtr, 5, ZSTD_dictMode_e.ZSTD_noDict);
    }

    private static nuint ZSTD_BtFindBestMatch_noDict_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_BtFindBestMatch(ms, ip, iLimit, offBasePtr, 6, ZSTD_dictMode_e.ZSTD_noDict);
    }

    private static nuint ZSTD_BtFindBestMatch_extDict_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_BtFindBestMatch(ms, ip, iLimit, offBasePtr, 4, ZSTD_dictMode_e.ZSTD_extDict);
    }

    private static nuint ZSTD_BtFindBestMatch_extDict_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_BtFindBestMatch(ms, ip, iLimit, offBasePtr, 5, ZSTD_dictMode_e.ZSTD_extDict);
    }

    private static nuint ZSTD_BtFindBestMatch_extDict_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_BtFindBestMatch(ms, ip, iLimit, offBasePtr, 6, ZSTD_dictMode_e.ZSTD_extDict);
    }

    private static nuint ZSTD_BtFindBestMatch_dictMatchState_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_BtFindBestMatch(
            ms,
            ip,
            iLimit,
            offBasePtr,
            4,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_BtFindBestMatch_dictMatchState_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_BtFindBestMatch(
            ms,
            ip,
            iLimit,
            offBasePtr,
            5,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_BtFindBestMatch_dictMatchState_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_BtFindBestMatch(
            ms,
            ip,
            iLimit,
            offBasePtr,
            6,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_BtFindBestMatch_dedicatedDictSearch_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_BtFindBestMatch(
            ms,
            ip,
            iLimit,
            offBasePtr,
            4,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_BtFindBestMatch_dedicatedDictSearch_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_BtFindBestMatch(
            ms,
            ip,
            iLimit,
            offBasePtr,
            5,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_BtFindBestMatch_dedicatedDictSearch_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offBasePtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_BtFindBestMatch(
            ms,
            ip,
            iLimit,
            offBasePtr,
            6,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    /* Generate hash chain search fns for each combination of (dictMode, mls) */
    private static nuint ZSTD_HcFindBestMatch_noDict_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_HcFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_noDict);
    }

    private static nuint ZSTD_HcFindBestMatch_noDict_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_HcFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_noDict);
    }

    private static nuint ZSTD_HcFindBestMatch_noDict_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_HcFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_noDict);
    }

    private static nuint ZSTD_HcFindBestMatch_extDict_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_HcFindBestMatch(ms, ip, iLimit, offsetPtr, 4, ZSTD_dictMode_e.ZSTD_extDict);
    }

    private static nuint ZSTD_HcFindBestMatch_extDict_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_HcFindBestMatch(ms, ip, iLimit, offsetPtr, 5, ZSTD_dictMode_e.ZSTD_extDict);
    }

    private static nuint ZSTD_HcFindBestMatch_extDict_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_HcFindBestMatch(ms, ip, iLimit, offsetPtr, 6, ZSTD_dictMode_e.ZSTD_extDict);
    }

    private static nuint ZSTD_HcFindBestMatch_dictMatchState_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_HcFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_HcFindBestMatch_dictMatchState_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_HcFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_HcFindBestMatch_dictMatchState_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_HcFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_HcFindBestMatch_dedicatedDictSearch_4(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 4
        );
        return ZSTD_HcFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            4,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_HcFindBestMatch_dedicatedDictSearch_5(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 5
        );
        return ZSTD_HcFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            5,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_HcFindBestMatch_dedicatedDictSearch_6(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iLimit,
        nuint* offsetPtr
    )
    {
        assert(
            (
                4 > (6 < ms->cParams.minMatch ? 6 : ms->cParams.minMatch) ? 4
                : 6 < ms->cParams.minMatch ? 6
                : ms->cParams.minMatch
            ) == 6
        );
        return ZSTD_HcFindBestMatch(
            ms,
            ip,
            iLimit,
            offsetPtr,
            6,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    /**
     * Searches for the longest match at @p ip.
     * Dispatches to the correct implementation function based on the
     * (searchMethod, dictMode, mls, rowLog). We use switch statements
     * here instead of using an indirect function call through a function
     * pointer because after Spectre and Meltdown mitigations, indirect
     * function calls can be very costly, especially in the kernel.
     *
     * NOTE: dictMode and searchMethod should be templated, so those switch
     * statements should be optimized out. Only the mls & rowLog switches
     * should be left.
     *
     * @param ms The match state.
     * @param ip The position to search at.
     * @param iend The end of the input data.
     * @param[out] offsetPtr Stores the match offset into this pointer.
     * @param mls The minimum search length, in the range [4, 6].
     * @param rowLog The row log (if applicable), in the range [4, 6].
     * @param searchMethod The search method to use (templated).
     * @param dictMode The dictMode (templated).
     *
     * @returns The length of the longest match found, or < mls if no match is found.
     * If a match is found its offset is stored in @p offsetPtr.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_searchMax(
        ZSTD_MatchState_t* ms,
        byte* ip,
        byte* iend,
        nuint* offsetPtr,
        uint mls,
        uint rowLog,
        searchMethod_e searchMethod,
        ZSTD_dictMode_e dictMode
    )
    {
        if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
        {
            if (searchMethod == searchMethod_e.search_rowHash)
            {
                if (mls == 4)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_noDict_4_4(ms, ip, iend, offsetPtr);
                    return rowLog == 5
                        ? ZSTD_RowFindBestMatch_noDict_4_5(ms, ip, iend, offsetPtr)
                        : ZSTD_RowFindBestMatch_noDict_4_6(ms, ip, iend, offsetPtr);
                }

                if (mls == 5)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_noDict_5_4(ms, ip, iend, offsetPtr);
                    return rowLog == 5
                        ? ZSTD_RowFindBestMatch_noDict_5_5(ms, ip, iend, offsetPtr)
                        : ZSTD_RowFindBestMatch_noDict_5_6(ms, ip, iend, offsetPtr);
                }

                if (rowLog == 4)
                    return ZSTD_RowFindBestMatch_noDict_6_4(ms, ip, iend, offsetPtr);
                return rowLog == 5
                    ? ZSTD_RowFindBestMatch_noDict_6_5(ms, ip, iend, offsetPtr)
                    : ZSTD_RowFindBestMatch_noDict_6_6(ms, ip, iend, offsetPtr);
            }

            if (searchMethod == searchMethod_e.search_hashChain)
            {
                if (mls == 4)
                    return ZSTD_HcFindBestMatch_noDict_4(ms, ip, iend, offsetPtr);
                return mls == 5
                    ? ZSTD_HcFindBestMatch_noDict_5(ms, ip, iend, offsetPtr)
                    : ZSTD_HcFindBestMatch_noDict_6(ms, ip, iend, offsetPtr);
            }

            // searchMethod_e.search_binaryTree
            if (mls == 4)
                return ZSTD_BtFindBestMatch_noDict_4(ms, ip, iend, offsetPtr);
            return mls == 5
                ? ZSTD_BtFindBestMatch_noDict_5(ms, ip, iend, offsetPtr)
                : ZSTD_BtFindBestMatch_noDict_6(ms, ip, iend, offsetPtr);
        }

        if (dictMode == ZSTD_dictMode_e.ZSTD_extDict)
        {
            if (searchMethod == searchMethod_e.search_rowHash)
            {
                if (mls == 4)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_extDict_4_4(ms, ip, iend, offsetPtr);
                    if (rowLog == 5)
                        return ZSTD_RowFindBestMatch_extDict_4_5(ms, ip, iend, offsetPtr);
                    return ZSTD_RowFindBestMatch_extDict_4_6(ms, ip, iend, offsetPtr);
                }

                if (mls == 5)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_extDict_5_4(ms, ip, iend, offsetPtr);
                    if (rowLog == 5)
                        return ZSTD_RowFindBestMatch_extDict_5_5(ms, ip, iend, offsetPtr);
                    return ZSTD_RowFindBestMatch_extDict_5_6(ms, ip, iend, offsetPtr);
                }

                if (mls == 6)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_extDict_6_4(ms, ip, iend, offsetPtr);
                    if (rowLog == 5)
                        return ZSTD_RowFindBestMatch_extDict_6_5(ms, ip, iend, offsetPtr);
                    return ZSTD_RowFindBestMatch_extDict_6_6(ms, ip, iend, offsetPtr);
                }
            }

            if (searchMethod == searchMethod_e.search_hashChain)
            {
                if (mls == 4)
                    return ZSTD_HcFindBestMatch_extDict_4(ms, ip, iend, offsetPtr);
                if (mls == 5)
                    return ZSTD_HcFindBestMatch_extDict_5(ms, ip, iend, offsetPtr);
                return ZSTD_HcFindBestMatch_extDict_6(ms, ip, iend, offsetPtr);
            }

            // searchMethod_e.search_binaryTree
            if (mls == 4)
                return ZSTD_BtFindBestMatch_extDict_4(ms, ip, iend, offsetPtr);
            if (mls == 5)
                return ZSTD_BtFindBestMatch_extDict_5(ms, ip, iend, offsetPtr);
            return ZSTD_BtFindBestMatch_extDict_6(ms, ip, iend, offsetPtr);
        }

        if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
        {
            if (searchMethod == searchMethod_e.search_rowHash)
            {
                if (mls == 4)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_dictMatchState_4_4(ms, ip, iend, offsetPtr);
                    if (rowLog == 5)
                        return ZSTD_RowFindBestMatch_dictMatchState_4_5(ms, ip, iend, offsetPtr);
                    return ZSTD_RowFindBestMatch_dictMatchState_4_6(ms, ip, iend, offsetPtr);
                }

                if (mls == 5)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_dictMatchState_5_4(ms, ip, iend, offsetPtr);
                    if (rowLog == 5)
                        return ZSTD_RowFindBestMatch_dictMatchState_5_5(ms, ip, iend, offsetPtr);
                    return ZSTD_RowFindBestMatch_dictMatchState_5_6(ms, ip, iend, offsetPtr);
                }

                if (mls == 6)
                {
                    if (rowLog == 4)
                        return ZSTD_RowFindBestMatch_dictMatchState_6_4(ms, ip, iend, offsetPtr);
                    if (rowLog == 5)
                        return ZSTD_RowFindBestMatch_dictMatchState_6_5(ms, ip, iend, offsetPtr);
                    return ZSTD_RowFindBestMatch_dictMatchState_6_6(ms, ip, iend, offsetPtr);
                }
            }

            if (searchMethod == searchMethod_e.search_hashChain)
            {
                if (mls == 4)
                    return ZSTD_HcFindBestMatch_dictMatchState_4(ms, ip, iend, offsetPtr);
                if (mls == 5)
                    return ZSTD_HcFindBestMatch_dictMatchState_5(ms, ip, iend, offsetPtr);
                return ZSTD_HcFindBestMatch_dictMatchState_6(ms, ip, iend, offsetPtr);
            }

            // search_binaryTree
            if (mls == 4)
                return ZSTD_BtFindBestMatch_dictMatchState_4(ms, ip, iend, offsetPtr);
            if (mls == 5)
                return ZSTD_BtFindBestMatch_dictMatchState_5(ms, ip, iend, offsetPtr);
            return ZSTD_BtFindBestMatch_dictMatchState_6(ms, ip, iend, offsetPtr);
        }

        if (searchMethod == searchMethod_e.search_rowHash)
        {
            if (mls == 4)
            {
                if (rowLog == 4)
                    return ZSTD_RowFindBestMatch_dedicatedDictSearch_4_4(ms, ip, iend, offsetPtr);
                if (rowLog == 5)
                    return ZSTD_RowFindBestMatch_dedicatedDictSearch_4_5(ms, ip, iend, offsetPtr);
                return ZSTD_RowFindBestMatch_dedicatedDictSearch_4_6(ms, ip, iend, offsetPtr);
            }

            if (mls == 5)
            {
                if (rowLog == 4)
                    return ZSTD_RowFindBestMatch_dedicatedDictSearch_5_4(ms, ip, iend, offsetPtr);
                if (rowLog == 5)
                    return ZSTD_RowFindBestMatch_dedicatedDictSearch_5_5(ms, ip, iend, offsetPtr);
                return ZSTD_RowFindBestMatch_dedicatedDictSearch_5_6(ms, ip, iend, offsetPtr);
            }

            if (mls == 6)
            {
                if (rowLog == 4)
                    return ZSTD_RowFindBestMatch_dedicatedDictSearch_6_4(ms, ip, iend, offsetPtr);
                if (rowLog == 5)
                    return ZSTD_RowFindBestMatch_dedicatedDictSearch_6_5(ms, ip, iend, offsetPtr);
                return ZSTD_RowFindBestMatch_dedicatedDictSearch_6_6(ms, ip, iend, offsetPtr);
            }
        }

        if (searchMethod == searchMethod_e.search_hashChain)
        {
            if (mls == 4)
                return ZSTD_HcFindBestMatch_dedicatedDictSearch_4(ms, ip, iend, offsetPtr);
            if (mls == 5)
                return ZSTD_HcFindBestMatch_dedicatedDictSearch_5(ms, ip, iend, offsetPtr);
            return ZSTD_HcFindBestMatch_dedicatedDictSearch_6(ms, ip, iend, offsetPtr);
        }

        // searchMethod_e.search_binaryTree
        if (mls == 4)
            return ZSTD_BtFindBestMatch_dedicatedDictSearch_4(ms, ip, iend, offsetPtr);
        if (mls == 5)
            return ZSTD_BtFindBestMatch_dedicatedDictSearch_5(ms, ip, iend, offsetPtr);
        return ZSTD_BtFindBestMatch_dedicatedDictSearch_6(ms, ip, iend, offsetPtr);
    }

    /* *******************************
     *  Common parser - lazy strategy
     *********************************/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_lazy_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        searchMethod_e searchMethod,
        uint depth,
        ZSTD_dictMode_e dictMode
    )
    {
        byte* istart = (byte*)src;
        byte* ip = istart;
        byte* anchor = istart;
        byte* iend = istart + srcSize;
        byte* ilimit = searchMethod == searchMethod_e.search_rowHash ? iend - 8 - 8 : iend - 8;
        byte* @base = ms->window.@base;
        uint prefixLowestIndex = ms->window.dictLimit;
        byte* prefixLowest = @base + prefixLowestIndex;
        uint mls =
            ms->cParams.minMatch <= 4 ? 4
            : ms->cParams.minMatch <= 6 ? ms->cParams.minMatch
            : 6;
        uint rowLog =
            ms->cParams.searchLog <= 4 ? 4
            : ms->cParams.searchLog <= 6 ? ms->cParams.searchLog
            : 6;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        uint offsetSaved1 = 0,
            offsetSaved2 = 0;
        int isDMS = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? 1 : 0;
        int isDDS = dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch ? 1 : 0;
        int isDxS = isDMS != 0 || isDDS != 0 ? 1 : 0;
        ZSTD_MatchState_t* dms = ms->dictMatchState;
        uint dictLowestIndex = isDxS != 0 ? dms->window.dictLimit : 0;
        byte* dictBase = isDxS != 0 ? dms->window.@base : null;
        byte* dictLowest = isDxS != 0 ? dictBase + dictLowestIndex : null;
        byte* dictEnd = isDxS != 0 ? dms->window.nextSrc : null;
        uint dictIndexDelta = isDxS != 0 ? prefixLowestIndex - (uint)(dictEnd - dictBase) : 0;
        uint dictAndPrefixLength = (uint)(ip - prefixLowest + (dictEnd - dictLowest));
        ip += dictAndPrefixLength == 0 ? 1 : 0;
        if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
        {
            uint curr = (uint)(ip - @base);
            uint windowLow = ZSTD_getLowestPrefixIndex(ms, curr, ms->cParams.windowLog);
            uint maxRep = curr - windowLow;
            if (offset_2 > maxRep)
            {
                offsetSaved2 = offset_2;
                offset_2 = 0;
            }

            if (offset_1 > maxRep)
            {
                offsetSaved1 = offset_1;
                offset_1 = 0;
            }
        }

#if DEBUG
        if (isDxS != 0)
        {
            assert(offset_1 <= dictAndPrefixLength);
            assert(offset_2 <= dictAndPrefixLength);
        }
#endif

        ms->lazySkipping = 0;
        if (searchMethod == searchMethod_e.search_rowHash)
        {
            ZSTD_row_fillHashCache(ms, @base, rowLog, mls, ms->nextToUpdate, ilimit);
        }

        while (ip < ilimit)
        {
            nuint matchLength = 0;
            assert(1 >= 1);
            assert(1 <= 3);
            nuint offBase = 1;
            byte* start = ip + 1;
            if (isDxS != 0)
            {
                uint repIndex = (uint)(ip - @base) + 1 - offset_1;
                byte* repMatch =
                    (
                        dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState
                        || dictMode == ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
                    )
                    && repIndex < prefixLowestIndex
                        ? dictBase + (repIndex - dictIndexDelta)
                        : @base + repIndex;
                if (
                    ZSTD_index_overlap_check(prefixLowestIndex, repIndex) != 0
                    && MEM_read32(repMatch) == MEM_read32(ip + 1)
                )
                {
                    byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;
                    matchLength =
                        ZSTD_count_2segments(
                            ip + 1 + 4,
                            repMatch + 4,
                            iend,
                            repMatchEnd,
                            prefixLowest
                        ) + 4;
                    if (depth == 0)
                        goto _storeSequence;
                }
            }

            if (
                dictMode == ZSTD_dictMode_e.ZSTD_noDict
                && offset_1 > 0
                && MEM_read32(ip + 1 - offset_1) == MEM_read32(ip + 1)
            )
            {
                matchLength = ZSTD_count(ip + 1 + 4, ip + 1 + 4 - offset_1, iend) + 4;
                if (depth == 0)
                    goto _storeSequence;
            }

            {
                nuint offbaseFound = 999999999;
                nuint ml2 = ZSTD_searchMax(
                    ms,
                    ip,
                    iend,
                    &offbaseFound,
                    mls,
                    rowLog,
                    searchMethod,
                    dictMode
                );
                if (ml2 > matchLength)
                {
                    matchLength = ml2;
                    start = ip;
                    offBase = offbaseFound;
                }
            }

            if (matchLength < 4)
            {
                /* jump faster over incompressible sections */
                nuint step = ((nuint)(ip - anchor) >> 8) + 1;
                ip += step;
                ms->lazySkipping = step > 8 ? 1 : 0;
                continue;
            }

            if (depth >= 1)
                while (ip < ilimit)
                {
                    ip++;
                    if (
                        dictMode == ZSTD_dictMode_e.ZSTD_noDict
                        && offBase != 0
                        && offset_1 > 0
                        && MEM_read32(ip) == MEM_read32(ip - offset_1)
                    )
                    {
                        nuint mlRep = ZSTD_count(ip + 4, ip + 4 - offset_1, iend) + 4;
                        int gain2 = (int)(mlRep * 3);
                        int gain1 = (int)(matchLength * 3 - ZSTD_highbit32((uint)offBase) + 1);
                        if (mlRep >= 4 && gain2 > gain1)
                        {
                            matchLength = mlRep;
                            assert(1 >= 1);
                            assert(1 <= 3);
                            offBase = 1;
                            start = ip;
                        }
                    }

                    if (isDxS != 0)
                    {
                        uint repIndex = (uint)(ip - @base) - offset_1;
                        byte* repMatch =
                            repIndex < prefixLowestIndex
                                ? dictBase + (repIndex - dictIndexDelta)
                                : @base + repIndex;
                        if (
                            ZSTD_index_overlap_check(prefixLowestIndex, repIndex) != 0
                            && MEM_read32(repMatch) == MEM_read32(ip)
                        )
                        {
                            byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;
                            nuint mlRep =
                                ZSTD_count_2segments(
                                    ip + 4,
                                    repMatch + 4,
                                    iend,
                                    repMatchEnd,
                                    prefixLowest
                                ) + 4;
                            int gain2 = (int)(mlRep * 3);
                            int gain1 = (int)(matchLength * 3 - ZSTD_highbit32((uint)offBase) + 1);
                            if (mlRep >= 4 && gain2 > gain1)
                            {
                                matchLength = mlRep;
                                assert(1 >= 1);
                                assert(1 <= 3);
                                offBase = 1;
                                start = ip;
                            }
                        }
                    }

                    {
                        nuint ofbCandidate = 999999999;
                        nuint ml2 = ZSTD_searchMax(
                            ms,
                            ip,
                            iend,
                            &ofbCandidate,
                            mls,
                            rowLog,
                            searchMethod,
                            dictMode
                        );
                        /* raw approx */
                        int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)ofbCandidate));
                        int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)offBase) + 4);
                        if (ml2 >= 4 && gain2 > gain1)
                        {
                            matchLength = ml2;
                            offBase = ofbCandidate;
                            start = ip;
                            continue;
                        }
                    }

                    if (depth == 2 && ip < ilimit)
                    {
                        ip++;
                        if (
                            dictMode == ZSTD_dictMode_e.ZSTD_noDict
                            && offBase != 0
                            && offset_1 > 0
                            && MEM_read32(ip) == MEM_read32(ip - offset_1)
                        )
                        {
                            nuint mlRep = ZSTD_count(ip + 4, ip + 4 - offset_1, iend) + 4;
                            int gain2 = (int)(mlRep * 4);
                            int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)offBase) + 1);
                            if (mlRep >= 4 && gain2 > gain1)
                            {
                                matchLength = mlRep;
                                assert(1 >= 1);
                                assert(1 <= 3);
                                offBase = 1;
                                start = ip;
                            }
                        }

                        if (isDxS != 0)
                        {
                            uint repIndex = (uint)(ip - @base) - offset_1;
                            byte* repMatch =
                                repIndex < prefixLowestIndex
                                    ? dictBase + (repIndex - dictIndexDelta)
                                    : @base + repIndex;
                            if (
                                ZSTD_index_overlap_check(prefixLowestIndex, repIndex) != 0
                                && MEM_read32(repMatch) == MEM_read32(ip)
                            )
                            {
                                byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;
                                nuint mlRep =
                                    ZSTD_count_2segments(
                                        ip + 4,
                                        repMatch + 4,
                                        iend,
                                        repMatchEnd,
                                        prefixLowest
                                    ) + 4;
                                int gain2 = (int)(mlRep * 4);
                                int gain1 = (int)(
                                    matchLength * 4 - ZSTD_highbit32((uint)offBase) + 1
                                );
                                if (mlRep >= 4 && gain2 > gain1)
                                {
                                    matchLength = mlRep;
                                    assert(1 >= 1);
                                    assert(1 <= 3);
                                    offBase = 1;
                                    start = ip;
                                }
                            }
                        }

                        {
                            nuint ofbCandidate = 999999999;
                            nuint ml2 = ZSTD_searchMax(
                                ms,
                                ip,
                                iend,
                                &ofbCandidate,
                                mls,
                                rowLog,
                                searchMethod,
                                dictMode
                            );
                            /* raw approx */
                            int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)ofbCandidate));
                            int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)offBase) + 7);
                            if (ml2 >= 4 && gain2 > gain1)
                            {
                                matchLength = ml2;
                                offBase = ofbCandidate;
                                start = ip;
                                continue;
                            }
                        }
                    }

                    break;
                }

            if (offBase > 3)
            {
                if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
                {
                    assert(offBase > 3);
                    assert(offBase > 3);
                    while (
                        start > anchor
                        && start - (offBase - 3) > prefixLowest
                        && start[-1] == (start - (offBase - 3))[-1]
                    )
                    {
                        start--;
                        matchLength++;
                    }
                }

                if (isDxS != 0)
                {
                    assert(offBase > 3);
                    uint matchIndex = (uint)((nuint)(start - @base) - (offBase - 3));
                    byte* match =
                        matchIndex < prefixLowestIndex
                            ? dictBase + matchIndex - dictIndexDelta
                            : @base + matchIndex;
                    byte* mStart = matchIndex < prefixLowestIndex ? dictLowest : prefixLowest;
                    while (start > anchor && match > mStart && start[-1] == match[-1])
                    {
                        start--;
                        match--;
                        matchLength++;
                    }
                }

                offset_2 = offset_1;
                assert(offBase > 3);
                offset_1 = (uint)(offBase - 3);
            }

            _storeSequence:
            {
                nuint litLength = (nuint)(start - anchor);
                ZSTD_storeSeq(seqStore, litLength, anchor, iend, (uint)offBase, matchLength);
                anchor = ip = start + matchLength;
            }

            if (ms->lazySkipping != 0)
            {
                if (searchMethod == searchMethod_e.search_rowHash)
                {
                    ZSTD_row_fillHashCache(ms, @base, rowLog, mls, ms->nextToUpdate, ilimit);
                }

                ms->lazySkipping = 0;
            }

            if (isDxS != 0)
            {
                while (ip <= ilimit)
                {
                    uint current2 = (uint)(ip - @base);
                    uint repIndex = current2 - offset_2;
                    byte* repMatch =
                        repIndex < prefixLowestIndex
                            ? dictBase - dictIndexDelta + repIndex
                            : @base + repIndex;
                    if (
                        ZSTD_index_overlap_check(prefixLowestIndex, repIndex) != 0
                        && MEM_read32(repMatch) == MEM_read32(ip)
                    )
                    {
                        byte* repEnd2 = repIndex < prefixLowestIndex ? dictEnd : iend;
                        matchLength =
                            ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repEnd2, prefixLowest)
                            + 4;
                        offBase = offset_2;
                        offset_2 = offset_1;
                        offset_1 = (uint)offBase;
                        assert(1 >= 1);
                        assert(1 <= 3);
                        ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, matchLength);
                        ip += matchLength;
                        anchor = ip;
                        continue;
                    }

                    break;
                }
            }

            if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
            {
                while (ip <= ilimit && offset_2 > 0 && MEM_read32(ip) == MEM_read32(ip - offset_2))
                {
                    matchLength = ZSTD_count(ip + 4, ip + 4 - offset_2, iend) + 4;
                    offBase = offset_2;
                    offset_2 = offset_1;
                    offset_1 = (uint)offBase;
                    assert(1 >= 1);
                    assert(1 <= 3);
                    ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, matchLength);
                    ip += matchLength;
                    anchor = ip;
                    continue;
                }
            }
        }

        offsetSaved2 = offsetSaved1 != 0 && offset_1 != 0 ? offsetSaved1 : offsetSaved2;
        rep[0] = offset_1 != 0 ? offset_1 : offsetSaved1;
        rep[1] = offset_2 != 0 ? offset_2 : offsetSaved2;
        return (nuint)(iend - anchor);
    }

    private static nuint ZSTD_compressBlock_greedy(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            0,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_greedy_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            0,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_greedy_dedicatedDictSearch(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            0,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_compressBlock_greedy_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            0,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_greedy_dictMatchState_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            0,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_greedy_dedicatedDictSearch_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            0,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_compressBlock_lazy(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            1,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_lazy_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            1,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_lazy_dedicatedDictSearch(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            1,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_compressBlock_lazy_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            1,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_lazy_dictMatchState_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            1,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_lazy_dedicatedDictSearch_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            1,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_compressBlock_lazy2(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            2,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            2,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_dedicatedDictSearch(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            2,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            2,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_dictMatchState_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            2,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_dedicatedDictSearch_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            2,
            ZSTD_dictMode_e.ZSTD_dedicatedDictSearch
        );
    }

    private static nuint ZSTD_compressBlock_btlazy2(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_binaryTree,
            2,
            ZSTD_dictMode_e.ZSTD_noDict
        );
    }

    private static nuint ZSTD_compressBlock_btlazy2_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_binaryTree,
            2,
            ZSTD_dictMode_e.ZSTD_dictMatchState
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_lazy_extDict_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        searchMethod_e searchMethod,
        uint depth
    )
    {
        byte* istart = (byte*)src;
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
        uint mls =
            ms->cParams.minMatch <= 4 ? 4
            : ms->cParams.minMatch <= 6 ? ms->cParams.minMatch
            : 6;
        uint rowLog =
            ms->cParams.searchLog <= 4 ? 4
            : ms->cParams.searchLog <= 6 ? ms->cParams.searchLog
            : 6;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        ms->lazySkipping = 0;
        ip += ip == prefixStart ? 1 : 0;
        if (searchMethod == searchMethod_e.search_rowHash)
        {
            ZSTD_row_fillHashCache(ms, @base, rowLog, mls, ms->nextToUpdate, ilimit);
        }

        while (ip < ilimit)
        {
            nuint matchLength = 0;
            assert(1 >= 1);
            assert(1 <= 3);
            nuint offBase = 1;
            byte* start = ip + 1;
            uint curr = (uint)(ip - @base);
            {
                uint windowLow = ZSTD_getLowestMatchIndex(ms, curr + 1, windowLog);
                uint repIndex = curr + 1 - offset_1;
                byte* repBase = repIndex < dictLimit ? dictBase : @base;
                byte* repMatch = repBase + repIndex;
                if (
                    (
                        ZSTD_index_overlap_check(dictLimit, repIndex)
                        & (offset_1 <= curr + 1 - windowLow ? 1 : 0)
                    ) != 0
                )
                    if (MEM_read32(ip + 1) == MEM_read32(repMatch))
                    {
                        /* repcode detected we should take it */
                        byte* repEnd = repIndex < dictLimit ? dictEnd : iend;
                        matchLength =
                            ZSTD_count_2segments(
                                ip + 1 + 4,
                                repMatch + 4,
                                iend,
                                repEnd,
                                prefixStart
                            ) + 4;
                        if (depth == 0)
                            goto _storeSequence;
                    }
            }

            {
                nuint ofbCandidate = 999999999;
                nuint ml2 = ZSTD_searchMax(
                    ms,
                    ip,
                    iend,
                    &ofbCandidate,
                    mls,
                    rowLog,
                    searchMethod,
                    ZSTD_dictMode_e.ZSTD_extDict
                );
                if (ml2 > matchLength)
                {
                    matchLength = ml2;
                    start = ip;
                    offBase = ofbCandidate;
                }
            }

            if (matchLength < 4)
            {
                nuint step = (nuint)(ip - anchor) >> 8;
                ip += step + 1;
                ms->lazySkipping = step > 8 ? 1 : 0;
                continue;
            }

            if (depth >= 1)
                while (ip < ilimit)
                {
                    ip++;
                    curr++;
                    if (offBase != 0)
                    {
                        uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, windowLog);
                        uint repIndex = curr - offset_1;
                        byte* repBase = repIndex < dictLimit ? dictBase : @base;
                        byte* repMatch = repBase + repIndex;
                        if (
                            (
                                ZSTD_index_overlap_check(dictLimit, repIndex)
                                & (offset_1 <= curr - windowLow ? 1 : 0)
                            ) != 0
                        )
                            if (MEM_read32(ip) == MEM_read32(repMatch))
                            {
                                /* repcode detected */
                                byte* repEnd = repIndex < dictLimit ? dictEnd : iend;
                                nuint repLength =
                                    ZSTD_count_2segments(
                                        ip + 4,
                                        repMatch + 4,
                                        iend,
                                        repEnd,
                                        prefixStart
                                    ) + 4;
                                int gain2 = (int)(repLength * 3);
                                int gain1 = (int)(
                                    matchLength * 3 - ZSTD_highbit32((uint)offBase) + 1
                                );
                                if (repLength >= 4 && gain2 > gain1)
                                {
                                    matchLength = repLength;
                                    assert(1 >= 1);
                                    assert(1 <= 3);
                                    offBase = 1;
                                    start = ip;
                                }
                            }
                    }

                    {
                        nuint ofbCandidate = 999999999;
                        nuint ml2 = ZSTD_searchMax(
                            ms,
                            ip,
                            iend,
                            &ofbCandidate,
                            mls,
                            rowLog,
                            searchMethod,
                            ZSTD_dictMode_e.ZSTD_extDict
                        );
                        /* raw approx */
                        int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)ofbCandidate));
                        int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)offBase) + 4);
                        if (ml2 >= 4 && gain2 > gain1)
                        {
                            matchLength = ml2;
                            offBase = ofbCandidate;
                            start = ip;
                            continue;
                        }
                    }

                    if (depth == 2 && ip < ilimit)
                    {
                        ip++;
                        curr++;
                        if (offBase != 0)
                        {
                            uint windowLow = ZSTD_getLowestMatchIndex(ms, curr, windowLog);
                            uint repIndex = curr - offset_1;
                            byte* repBase = repIndex < dictLimit ? dictBase : @base;
                            byte* repMatch = repBase + repIndex;
                            if (
                                (
                                    ZSTD_index_overlap_check(dictLimit, repIndex)
                                    & (offset_1 <= curr - windowLow ? 1 : 0)
                                ) != 0
                            )
                                if (MEM_read32(ip) == MEM_read32(repMatch))
                                {
                                    /* repcode detected */
                                    byte* repEnd = repIndex < dictLimit ? dictEnd : iend;
                                    nuint repLength =
                                        ZSTD_count_2segments(
                                            ip + 4,
                                            repMatch + 4,
                                            iend,
                                            repEnd,
                                            prefixStart
                                        ) + 4;
                                    int gain2 = (int)(repLength * 4);
                                    int gain1 = (int)(
                                        matchLength * 4 - ZSTD_highbit32((uint)offBase) + 1
                                    );
                                    if (repLength >= 4 && gain2 > gain1)
                                    {
                                        matchLength = repLength;
                                        assert(1 >= 1);
                                        assert(1 <= 3);
                                        offBase = 1;
                                        start = ip;
                                    }
                                }
                        }

                        {
                            nuint ofbCandidate = 999999999;
                            nuint ml2 = ZSTD_searchMax(
                                ms,
                                ip,
                                iend,
                                &ofbCandidate,
                                mls,
                                rowLog,
                                searchMethod,
                                ZSTD_dictMode_e.ZSTD_extDict
                            );
                            /* raw approx */
                            int gain2 = (int)(ml2 * 4 - ZSTD_highbit32((uint)ofbCandidate));
                            int gain1 = (int)(matchLength * 4 - ZSTD_highbit32((uint)offBase) + 7);
                            if (ml2 >= 4 && gain2 > gain1)
                            {
                                matchLength = ml2;
                                offBase = ofbCandidate;
                                start = ip;
                                continue;
                            }
                        }
                    }

                    break;
                }

            if (offBase > 3)
            {
                assert(offBase > 3);
                uint matchIndex = (uint)((nuint)(start - @base) - (offBase - 3));
                byte* match = matchIndex < dictLimit ? dictBase + matchIndex : @base + matchIndex;
                byte* mStart = matchIndex < dictLimit ? dictStart : prefixStart;
                while (start > anchor && match > mStart && start[-1] == match[-1])
                {
                    start--;
                    match--;
                    matchLength++;
                }

                offset_2 = offset_1;
                assert(offBase > 3);
                offset_1 = (uint)(offBase - 3);
            }

            _storeSequence:
            {
                nuint litLength = (nuint)(start - anchor);
                ZSTD_storeSeq(seqStore, litLength, anchor, iend, (uint)offBase, matchLength);
                anchor = ip = start + matchLength;
            }

            if (ms->lazySkipping != 0)
            {
                if (searchMethod == searchMethod_e.search_rowHash)
                {
                    ZSTD_row_fillHashCache(ms, @base, rowLog, mls, ms->nextToUpdate, ilimit);
                }

                ms->lazySkipping = 0;
            }

            while (ip <= ilimit)
            {
                uint repCurrent = (uint)(ip - @base);
                uint windowLow = ZSTD_getLowestMatchIndex(ms, repCurrent, windowLog);
                uint repIndex = repCurrent - offset_2;
                byte* repBase = repIndex < dictLimit ? dictBase : @base;
                byte* repMatch = repBase + repIndex;
                if (
                    (
                        ZSTD_index_overlap_check(dictLimit, repIndex)
                        & (offset_2 <= repCurrent - windowLow ? 1 : 0)
                    ) != 0
                )
                    if (MEM_read32(ip) == MEM_read32(repMatch))
                    {
                        /* repcode detected we should take it */
                        byte* repEnd = repIndex < dictLimit ? dictEnd : iend;
                        matchLength =
                            ZSTD_count_2segments(ip + 4, repMatch + 4, iend, repEnd, prefixStart)
                            + 4;
                        offBase = offset_2;
                        offset_2 = offset_1;
                        offset_1 = (uint)offBase;
                        assert(1 >= 1);
                        assert(1 <= 3);
                        ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, matchLength);
                        ip += matchLength;
                        anchor = ip;
                        continue;
                    }

                break;
            }
        }

        rep[0] = offset_1;
        rep[1] = offset_2;
        return (nuint)(iend - anchor);
    }

    private static nuint ZSTD_compressBlock_greedy_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            0
        );
    }

    private static nuint ZSTD_compressBlock_greedy_extDict_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            0
        );
    }

    private static nuint ZSTD_compressBlock_lazy_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            1
        );
    }

    private static nuint ZSTD_compressBlock_lazy_extDict_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            1
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_hashChain,
            2
        );
    }

    private static nuint ZSTD_compressBlock_lazy2_extDict_row(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_rowHash,
            2
        );
    }

    private static nuint ZSTD_compressBlock_btlazy2_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_lazy_extDict_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            searchMethod_e.search_binaryTree,
            2
        );
    }
}
