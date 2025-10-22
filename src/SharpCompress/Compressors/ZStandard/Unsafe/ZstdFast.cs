using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static void ZSTD_fillHashTableForCDict(
        ZSTD_MatchState_t* ms,
        void* end,
        ZSTD_dictTableLoadMethod_e dtlm
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hBits = cParams->hashLog + 8;
        uint mls = cParams->minMatch;
        byte* @base = ms->window.@base;
        byte* ip = @base + ms->nextToUpdate;
        byte* iend = (byte*)end - 8;
        const uint fastHashFillStep = 3;
        assert(dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_full);
        for (; ip + fastHashFillStep < iend + 2; ip += fastHashFillStep)
        {
            uint curr = (uint)(ip - @base);
            {
                nuint hashAndTag = ZSTD_hashPtr(ip, hBits, mls);
                ZSTD_writeTaggedIndex(hashTable, hashAndTag, curr);
            }

            if (dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast)
                continue;
            {
                uint p;
                for (p = 1; p < fastHashFillStep; ++p)
                {
                    nuint hashAndTag = ZSTD_hashPtr(ip + p, hBits, mls);
                    if (hashTable[hashAndTag >> 8] == 0)
                    {
                        ZSTD_writeTaggedIndex(hashTable, hashAndTag, curr + p);
                    }
                }
            }
        }
    }

    private static void ZSTD_fillHashTableForCCtx(
        ZSTD_MatchState_t* ms,
        void* end,
        ZSTD_dictTableLoadMethod_e dtlm
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hBits = cParams->hashLog;
        uint mls = cParams->minMatch;
        byte* @base = ms->window.@base;
        byte* ip = @base + ms->nextToUpdate;
        byte* iend = (byte*)end - 8;
        const uint fastHashFillStep = 3;
        assert(dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast);
        for (; ip + fastHashFillStep < iend + 2; ip += fastHashFillStep)
        {
            uint curr = (uint)(ip - @base);
            nuint hash0 = ZSTD_hashPtr(ip, hBits, mls);
            hashTable[hash0] = curr;
            if (dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast)
                continue;
            {
                uint p;
                for (p = 1; p < fastHashFillStep; ++p)
                {
                    nuint hash = ZSTD_hashPtr(ip + p, hBits, mls);
                    if (hashTable[hash] == 0)
                    {
                        hashTable[hash] = curr + p;
                    }
                }
            }
        }
    }

    private static void ZSTD_fillHashTable(
        ZSTD_MatchState_t* ms,
        void* end,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_tableFillPurpose_e tfp
    )
    {
        if (tfp == ZSTD_tableFillPurpose_e.ZSTD_tfp_forCDict)
        {
            ZSTD_fillHashTableForCDict(ms, end, dtlm);
        }
        else
        {
            ZSTD_fillHashTableForCCtx(ms, end, dtlm);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_match4Found_cmov(
        byte* currentPtr,
        byte* matchAddress,
        uint matchIdx,
        uint idxLowLimit
    )
    {
        /* currentIdx >= lowLimit is a (somewhat) unpredictable branch.
         * However expression below compiles into conditional move.
         */
        byte* mvalAddr = ZSTD_selectAddr(matchIdx, idxLowLimit, matchAddress, dummy);
        if (MEM_read32(currentPtr) != MEM_read32(mvalAddr))
            return 0;
        return matchIdx >= idxLowLimit ? 1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_match4Found_branch(
        byte* currentPtr,
        byte* matchAddress,
        uint matchIdx,
        uint idxLowLimit
    )
    {
        /* using a branch instead of a cmov,
         * because it's faster in scenarios where matchIdx >= idxLowLimit is generally true,
         * aka almost all candidates are within range */
        uint mval;
        if (matchIdx >= idxLowLimit)
        {
            mval = MEM_read32(matchAddress);
        }
        else
        {
            mval = MEM_read32(currentPtr) ^ 1;
        }

        return MEM_read32(currentPtr) == mval ? 1 : 0;
    }

    /**
     * If you squint hard enough (and ignore repcodes), the search operation at any
     * given position is broken into 4 stages:
     *
     * 1. Hash   (map position to hash value via input read)
     * 2. Lookup (map hash val to index via hashtable read)
     * 3. Load   (map index to value at that position via input read)
     * 4. Compare
     *
     * Each of these steps involves a memory read at an address which is computed
     * from the previous step. This means these steps must be sequenced and their
     * latencies are cumulative.
     *
     * Rather than do 1->2->3->4 sequentially for a single position before moving
     * onto the next, this implementation interleaves these operations across the
     * next few positions:
     *
     * R = Repcode Read & Compare
     * H = Hash
     * T = Table Lookup
     * M = Match Read & Compare
     *
     * Pos | Time -->
     * ----+-------------------
     * N   | ... M
     * N+1 | ...   TM
     * N+2 |    R H   T M
     * N+3 |         H    TM
     * N+4 |           R H   T M
     * N+5 |                H   ...
     * N+6 |                  R ...
     *
     * This is very much analogous to the pipelining of execution in a CPU. And just
     * like a CPU, we have to dump the pipeline when we find a match (i.e., take a
     * branch).
     *
     * When this happens, we throw away our current state, and do the following prep
     * to re-enter the loop:
     *
     * Pos | Time -->
     * ----+-------------------
     * N   | H T
     * N+1 |  H
     *
     * This is also the work we do at the beginning to enter the loop initially.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_fast_noDict_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        uint mls,
        int useCmov
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hlog = cParams->hashLog;
        /* min 2 */
        nuint stepSize = cParams->targetLength + (uint)(cParams->targetLength == 0 ? 1 : 0) + 1;
        byte* @base = ms->window.@base;
        byte* istart = (byte*)src;
        uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
        uint prefixStartIndex = ZSTD_getLowestPrefixIndex(ms, endIndex, cParams->windowLog);
        byte* prefixStart = @base + prefixStartIndex;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        byte* anchor = istart;
        byte* ip0 = istart;
        byte* ip1;
        byte* ip2;
        byte* ip3;
        uint current0;
        uint rep_offset1 = rep[0];
        uint rep_offset2 = rep[1];
        uint offsetSaved1 = 0,
            offsetSaved2 = 0;
        /* hash for ip0 */
        nuint hash0;
        /* hash for ip1 */
        nuint hash1;
        /* match idx for ip0 */
        uint matchIdx;
        uint offcode;
        byte* match0;
        nuint mLength;
        /* ip0 and ip1 are always adjacent. The targetLength skipping and
         * uncompressibility acceleration is applied to every other position,
         * matching the behavior of #1562. step therefore represents the gap
         * between pairs of positions, from ip0 to ip2 or ip1 to ip3. */
        nuint step;
        byte* nextStep;
        const nuint kStepIncr = 1 << 8 - 1;
        void* matchFound =
            useCmov != 0
                ? (delegate* managed<byte*, byte*, uint, uint, int>)(&ZSTD_match4Found_cmov)
                : (delegate* managed<byte*, byte*, uint, uint, int>)(&ZSTD_match4Found_branch);
        ip0 += ip0 == prefixStart ? 1 : 0;
        {
            uint curr = (uint)(ip0 - @base);
            uint windowLow = ZSTD_getLowestPrefixIndex(ms, curr, cParams->windowLog);
            uint maxRep = curr - windowLow;
            if (rep_offset2 > maxRep)
            {
                offsetSaved2 = rep_offset2;
                rep_offset2 = 0;
            }

            if (rep_offset1 > maxRep)
            {
                offsetSaved1 = rep_offset1;
                rep_offset1 = 0;
            }
        }

        _start:
        step = stepSize;
        nextStep = ip0 + kStepIncr;
        ip1 = ip0 + 1;
        ip2 = ip0 + step;
        ip3 = ip2 + 1;
        if (ip3 >= ilimit)
        {
            goto _cleanup;
        }

        hash0 = ZSTD_hashPtr(ip0, hlog, mls);
        hash1 = ZSTD_hashPtr(ip1, hlog, mls);
        matchIdx = hashTable[hash0];
        do
        {
            /* load repcode match for ip[2]*/
            uint rval = MEM_read32(ip2 - rep_offset1);
            current0 = (uint)(ip0 - @base);
            hashTable[hash0] = current0;
            if (MEM_read32(ip2) == rval && rep_offset1 > 0)
            {
                ip0 = ip2;
                match0 = ip0 - rep_offset1;
                mLength = ip0[-1] == match0[-1] ? 1U : 0U;
                ip0 -= mLength;
                match0 -= mLength;
                assert(1 >= 1);
                assert(1 <= 3);
                offcode = 1;
                mLength += 4;
                hashTable[hash1] = (uint)(ip1 - @base);
                goto _match;
            }

            if (
                ((delegate* managed<byte*, byte*, uint, uint, int>)matchFound)(
                    ip0,
                    @base + matchIdx,
                    matchIdx,
                    prefixStartIndex
                ) != 0
            )
            {
                hashTable[hash1] = (uint)(ip1 - @base);
                goto _offset;
            }

            matchIdx = hashTable[hash1];
            hash0 = hash1;
            hash1 = ZSTD_hashPtr(ip2, hlog, mls);
            ip0 = ip1;
            ip1 = ip2;
            ip2 = ip3;
            current0 = (uint)(ip0 - @base);
            hashTable[hash0] = current0;
            if (
                ((delegate* managed<byte*, byte*, uint, uint, int>)matchFound)(
                    ip0,
                    @base + matchIdx,
                    matchIdx,
                    prefixStartIndex
                ) != 0
            )
            {
                if (step <= 4)
                {
                    hashTable[hash1] = (uint)(ip1 - @base);
                }

                goto _offset;
            }

            matchIdx = hashTable[hash1];
            hash0 = hash1;
            hash1 = ZSTD_hashPtr(ip2, hlog, mls);
            ip0 = ip1;
            ip1 = ip2;
            ip2 = ip0 + step;
            ip3 = ip1 + step;
            if (ip2 >= nextStep)
            {
                step++;
#if NETCOREAPP3_0_OR_GREATER
                if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                {
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(ip1 + 64);
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(ip1 + 128);
                }
#endif

                nextStep += kStepIncr;
            }
        } while (ip3 < ilimit);
        _cleanup:
        offsetSaved2 = offsetSaved1 != 0 && rep_offset1 != 0 ? offsetSaved1 : offsetSaved2;
        rep[0] = rep_offset1 != 0 ? rep_offset1 : offsetSaved1;
        rep[1] = rep_offset2 != 0 ? rep_offset2 : offsetSaved2;
        return (nuint)(iend - anchor);
        _offset:
        match0 = @base + matchIdx;
        rep_offset2 = rep_offset1;
        rep_offset1 = (uint)(ip0 - match0);
        assert(rep_offset1 > 0);
        offcode = rep_offset1 + 3;
        mLength = 4;
        while (ip0 > anchor && match0 > prefixStart && ip0[-1] == match0[-1])
        {
            ip0--;
            match0--;
            mLength++;
        }

        _match:
        mLength += ZSTD_count(ip0 + mLength, match0 + mLength, iend);
        ZSTD_storeSeq(seqStore, (nuint)(ip0 - anchor), anchor, iend, offcode, mLength);
        ip0 += mLength;
        anchor = ip0;
        if (ip0 <= ilimit)
        {
            assert(@base + current0 + 2 > istart);
            hashTable[ZSTD_hashPtr(@base + current0 + 2, hlog, mls)] = current0 + 2;
            hashTable[ZSTD_hashPtr(ip0 - 2, hlog, mls)] = (uint)(ip0 - 2 - @base);
            if (rep_offset2 > 0)
            {
                while (ip0 <= ilimit && MEM_read32(ip0) == MEM_read32(ip0 - rep_offset2))
                {
                    /* store sequence */
                    nuint rLength = ZSTD_count(ip0 + 4, ip0 + 4 - rep_offset2, iend) + 4;
                    {
                        /* swap rep_offset2 <=> rep_offset1 */
                        uint tmpOff = rep_offset2;
                        rep_offset2 = rep_offset1;
                        rep_offset1 = tmpOff;
                    }

                    hashTable[ZSTD_hashPtr(ip0, hlog, mls)] = (uint)(ip0 - @base);
                    ip0 += rLength;
                    assert(1 >= 1);
                    assert(1 <= 3);
                    ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, rLength);
                    anchor = ip0;
                    continue;
                }
            }
        }

        goto _start;
    }

    private static nuint ZSTD_compressBlock_fast_noDict_4_1(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 4, 1);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_5_1(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 5, 1);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_6_1(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 6, 1);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_7_1(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 7, 1);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_4_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 4, 0);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_5_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 5, 0);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_6_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 6, 0);
    }

    private static nuint ZSTD_compressBlock_fast_noDict_7_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_noDict_generic(ms, seqStore, rep, src, srcSize, 7, 0);
    }

    private static nuint ZSTD_compressBlock_fast(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint mml = ms->cParams.minMatch;
        /* use cmov when "candidate in range" branch is likely unpredictable */
        int useCmov = ms->cParams.windowLog < 19 ? 1 : 0;
        assert(ms->dictMatchState == null);
        if (useCmov != 0)
        {
            switch (mml)
            {
                default:
                case 4:
                    return ZSTD_compressBlock_fast_noDict_4_1(ms, seqStore, rep, src, srcSize);
                case 5:
                    return ZSTD_compressBlock_fast_noDict_5_1(ms, seqStore, rep, src, srcSize);
                case 6:
                    return ZSTD_compressBlock_fast_noDict_6_1(ms, seqStore, rep, src, srcSize);
                case 7:
                    return ZSTD_compressBlock_fast_noDict_7_1(ms, seqStore, rep, src, srcSize);
            }
        }
        else
        {
            switch (mml)
            {
                default:
                case 4:
                    return ZSTD_compressBlock_fast_noDict_4_0(ms, seqStore, rep, src, srcSize);
                case 5:
                    return ZSTD_compressBlock_fast_noDict_5_0(ms, seqStore, rep, src, srcSize);
                case 6:
                    return ZSTD_compressBlock_fast_noDict_6_0(ms, seqStore, rep, src, srcSize);
                case 7:
                    return ZSTD_compressBlock_fast_noDict_7_0(ms, seqStore, rep, src, srcSize);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_fast_dictMatchState_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        uint mls,
        uint hasStep
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hlog = cParams->hashLog;
        /* support stepSize of 0 */
        uint stepSize = cParams->targetLength + (uint)(cParams->targetLength == 0 ? 1 : 0);
        byte* @base = ms->window.@base;
        byte* istart = (byte*)src;
        byte* ip0 = istart;
        /* we assert below that stepSize >= 1 */
        byte* ip1 = ip0 + stepSize;
        byte* anchor = istart;
        uint prefixStartIndex = ms->window.dictLimit;
        byte* prefixStart = @base + prefixStartIndex;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        ZSTD_MatchState_t* dms = ms->dictMatchState;
        ZSTD_compressionParameters* dictCParams = &dms->cParams;
        uint* dictHashTable = dms->hashTable;
        uint dictStartIndex = dms->window.dictLimit;
        byte* dictBase = dms->window.@base;
        byte* dictStart = dictBase + dictStartIndex;
        byte* dictEnd = dms->window.nextSrc;
        uint dictIndexDelta = prefixStartIndex - (uint)(dictEnd - dictBase);
        uint dictAndPrefixLength = (uint)(istart - prefixStart + dictEnd - dictStart);
        uint dictHBits = dictCParams->hashLog + 8;
        /* if a dictionary is still attached, it necessarily means that
         * it is within window size. So we just check it. */
        uint maxDistance = 1U << (int)cParams->windowLog;
        uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
        assert(endIndex - prefixStartIndex <= maxDistance);
        assert(prefixStartIndex >= (uint)(dictEnd - dictBase));
        if (ms->prefetchCDictTables != 0)
        {
            nuint hashTableBytes = ((nuint)1 << (int)dictCParams->hashLog) * sizeof(uint);
            {
                sbyte* _ptr = (sbyte*)dictHashTable;
                nuint _size = hashTableBytes;
                nuint _pos;
                for (_pos = 0; _pos < _size; _pos += 64)
                {
#if NETCOREAPP3_0_OR_GREATER
                    if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                    {
                        System.Runtime.Intrinsics.X86.Sse.Prefetch1(_ptr + _pos);
                    }
#endif
                }
            }
        }

        ip0 += dictAndPrefixLength == 0 ? 1 : 0;
        assert(offset_1 <= dictAndPrefixLength);
        assert(offset_2 <= dictAndPrefixLength);
        assert(stepSize >= 1);
        while (ip1 <= ilimit)
        {
            nuint mLength;
            nuint hash0 = ZSTD_hashPtr(ip0, hlog, mls);
            nuint dictHashAndTag0 = ZSTD_hashPtr(ip0, dictHBits, mls);
            uint dictMatchIndexAndTag = dictHashTable[dictHashAndTag0 >> 8];
            int dictTagsMatch = ZSTD_comparePackedTags(dictMatchIndexAndTag, dictHashAndTag0);
            uint matchIndex = hashTable[hash0];
            uint curr = (uint)(ip0 - @base);
            nuint step = stepSize;
            const nuint kStepIncr = 1 << 8;
            byte* nextStep = ip0 + kStepIncr;
            while (true)
            {
                byte* match = @base + matchIndex;
                uint repIndex = curr + 1 - offset_1;
                byte* repMatch =
                    repIndex < prefixStartIndex
                        ? dictBase + (repIndex - dictIndexDelta)
                        : @base + repIndex;
                nuint hash1 = ZSTD_hashPtr(ip1, hlog, mls);
                nuint dictHashAndTag1 = ZSTD_hashPtr(ip1, dictHBits, mls);
                hashTable[hash0] = curr;
                if (
                    ZSTD_index_overlap_check(prefixStartIndex, repIndex) != 0
                    && MEM_read32(repMatch) == MEM_read32(ip0 + 1)
                )
                {
                    byte* repMatchEnd = repIndex < prefixStartIndex ? dictEnd : iend;
                    mLength =
                        ZSTD_count_2segments(
                            ip0 + 1 + 4,
                            repMatch + 4,
                            iend,
                            repMatchEnd,
                            prefixStart
                        ) + 4;
                    ip0++;
                    assert(1 >= 1);
                    assert(1 <= 3);
                    ZSTD_storeSeq(seqStore, (nuint)(ip0 - anchor), anchor, iend, 1, mLength);
                    break;
                }

                if (dictTagsMatch != 0)
                {
                    /* Found a possible dict match */
                    uint dictMatchIndex = dictMatchIndexAndTag >> 8;
                    byte* dictMatch = dictBase + dictMatchIndex;
                    if (dictMatchIndex > dictStartIndex && MEM_read32(dictMatch) == MEM_read32(ip0))
                    {
                        if (matchIndex <= prefixStartIndex)
                        {
                            uint offset = curr - dictMatchIndex - dictIndexDelta;
                            mLength =
                                ZSTD_count_2segments(
                                    ip0 + 4,
                                    dictMatch + 4,
                                    iend,
                                    dictEnd,
                                    prefixStart
                                ) + 4;
                            while (
                                ip0 > anchor && dictMatch > dictStart && ip0[-1] == dictMatch[-1]
                            )
                            {
                                ip0--;
                                dictMatch--;
                                mLength++;
                            }

                            offset_2 = offset_1;
                            offset_1 = offset;
                            assert(offset > 0);
                            ZSTD_storeSeq(
                                seqStore,
                                (nuint)(ip0 - anchor),
                                anchor,
                                iend,
                                offset + 3,
                                mLength
                            );
                            break;
                        }
                    }
                }

                if (ZSTD_match4Found_cmov(ip0, match, matchIndex, prefixStartIndex) != 0)
                {
                    /* found a regular match of size >= 4 */
                    uint offset = (uint)(ip0 - match);
                    mLength = ZSTD_count(ip0 + 4, match + 4, iend) + 4;
                    while (ip0 > anchor && match > prefixStart && ip0[-1] == match[-1])
                    {
                        ip0--;
                        match--;
                        mLength++;
                    }

                    offset_2 = offset_1;
                    offset_1 = offset;
                    assert(offset > 0);
                    ZSTD_storeSeq(
                        seqStore,
                        (nuint)(ip0 - anchor),
                        anchor,
                        iend,
                        offset + 3,
                        mLength
                    );
                    break;
                }

                dictMatchIndexAndTag = dictHashTable[dictHashAndTag1 >> 8];
                dictTagsMatch = ZSTD_comparePackedTags(dictMatchIndexAndTag, dictHashAndTag1);
                matchIndex = hashTable[hash1];
                if (ip1 >= nextStep)
                {
                    step++;
                    nextStep += kStepIncr;
                }

                ip0 = ip1;
                ip1 = ip1 + step;
                if (ip1 > ilimit)
                    goto _cleanup;
                curr = (uint)(ip0 - @base);
                hash0 = hash1;
            }

            assert(mLength != 0);
            ip0 += mLength;
            anchor = ip0;
            if (ip0 <= ilimit)
            {
                assert(@base + curr + 2 > istart);
                hashTable[ZSTD_hashPtr(@base + curr + 2, hlog, mls)] = curr + 2;
                hashTable[ZSTD_hashPtr(ip0 - 2, hlog, mls)] = (uint)(ip0 - 2 - @base);
                while (ip0 <= ilimit)
                {
                    uint current2 = (uint)(ip0 - @base);
                    uint repIndex2 = current2 - offset_2;
                    byte* repMatch2 =
                        repIndex2 < prefixStartIndex
                            ? dictBase - dictIndexDelta + repIndex2
                            : @base + repIndex2;
                    if (
                        ZSTD_index_overlap_check(prefixStartIndex, repIndex2) != 0
                        && MEM_read32(repMatch2) == MEM_read32(ip0)
                    )
                    {
                        byte* repEnd2 = repIndex2 < prefixStartIndex ? dictEnd : iend;
                        nuint repLength2 =
                            ZSTD_count_2segments(ip0 + 4, repMatch2 + 4, iend, repEnd2, prefixStart)
                            + 4;
                        /* swap offset_2 <=> offset_1 */
                        uint tmpOffset = offset_2;
                        offset_2 = offset_1;
                        offset_1 = tmpOffset;
                        assert(1 >= 1);
                        assert(1 <= 3);
                        ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, repLength2);
                        hashTable[ZSTD_hashPtr(ip0, hlog, mls)] = current2;
                        ip0 += repLength2;
                        anchor = ip0;
                        continue;
                    }

                    break;
                }
            }

            assert(ip0 == anchor);
            ip1 = ip0 + stepSize;
        }

        _cleanup:
        rep[0] = offset_1;
        rep[1] = offset_2;
        return (nuint)(iend - anchor);
    }

    private static nuint ZSTD_compressBlock_fast_dictMatchState_4_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            4,
            0
        );
    }

    private static nuint ZSTD_compressBlock_fast_dictMatchState_5_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            5,
            0
        );
    }

    private static nuint ZSTD_compressBlock_fast_dictMatchState_6_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            6,
            0
        );
    }

    private static nuint ZSTD_compressBlock_fast_dictMatchState_7_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            7,
            0
        );
    }

    private static nuint ZSTD_compressBlock_fast_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint mls = ms->cParams.minMatch;
        assert(ms->dictMatchState != null);
        switch (mls)
        {
            default:
            case 4:
                return ZSTD_compressBlock_fast_dictMatchState_4_0(ms, seqStore, rep, src, srcSize);
            case 5:
                return ZSTD_compressBlock_fast_dictMatchState_5_0(ms, seqStore, rep, src, srcSize);
            case 6:
                return ZSTD_compressBlock_fast_dictMatchState_6_0(ms, seqStore, rep, src, srcSize);
            case 7:
                return ZSTD_compressBlock_fast_dictMatchState_7_0(ms, seqStore, rep, src, srcSize);
        }
    }

    private static nuint ZSTD_compressBlock_fast_extDict_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        uint mls,
        uint hasStep
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashTable = ms->hashTable;
        uint hlog = cParams->hashLog;
        /* support stepSize of 0 */
        nuint stepSize = cParams->targetLength + (uint)(cParams->targetLength == 0 ? 1 : 0) + 1;
        byte* @base = ms->window.@base;
        byte* dictBase = ms->window.dictBase;
        byte* istart = (byte*)src;
        byte* anchor = istart;
        uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
        uint lowLimit = ZSTD_getLowestMatchIndex(ms, endIndex, cParams->windowLog);
        uint dictStartIndex = lowLimit;
        byte* dictStart = dictBase + dictStartIndex;
        uint dictLimit = ms->window.dictLimit;
        uint prefixStartIndex = dictLimit < lowLimit ? lowLimit : dictLimit;
        byte* prefixStart = @base + prefixStartIndex;
        byte* dictEnd = dictBase + prefixStartIndex;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        uint offsetSaved1 = 0,
            offsetSaved2 = 0;
        byte* ip0 = istart;
        byte* ip1;
        byte* ip2;
        byte* ip3;
        uint current0;
        /* hash for ip0 */
        nuint hash0;
        /* hash for ip1 */
        nuint hash1;
        /* match idx for ip0 */
        uint idx;
        /* base pointer for idx */
        byte* idxBase;
        uint offcode;
        byte* match0;
        nuint mLength;
        /* initialize to avoid warning, assert != 0 later */
        byte* matchEnd = null;
        nuint step;
        byte* nextStep;
        const nuint kStepIncr = 1 << 8 - 1;
        if (prefixStartIndex == dictStartIndex)
            return ZSTD_compressBlock_fast(ms, seqStore, rep, src, srcSize);
        {
            uint curr = (uint)(ip0 - @base);
            uint maxRep = curr - dictStartIndex;
            if (offset_2 >= maxRep)
            {
                offsetSaved2 = offset_2;
                offset_2 = 0;
            }

            if (offset_1 >= maxRep)
            {
                offsetSaved1 = offset_1;
                offset_1 = 0;
            }
        }

        _start:
        step = stepSize;
        nextStep = ip0 + kStepIncr;
        ip1 = ip0 + 1;
        ip2 = ip0 + step;
        ip3 = ip2 + 1;
        if (ip3 >= ilimit)
        {
            goto _cleanup;
        }

        hash0 = ZSTD_hashPtr(ip0, hlog, mls);
        hash1 = ZSTD_hashPtr(ip1, hlog, mls);
        idx = hashTable[hash0];
        idxBase = idx < prefixStartIndex ? dictBase : @base;
        do
        {
            {
                uint current2 = (uint)(ip2 - @base);
                uint repIndex = current2 - offset_1;
                byte* repBase = repIndex < prefixStartIndex ? dictBase : @base;
                uint rval;
                if (prefixStartIndex - repIndex >= 4 && offset_1 > 0)
                {
                    rval = MEM_read32(repBase + repIndex);
                }
                else
                {
                    rval = MEM_read32(ip2) ^ 1;
                }

                current0 = (uint)(ip0 - @base);
                hashTable[hash0] = current0;
                if (MEM_read32(ip2) == rval)
                {
                    ip0 = ip2;
                    match0 = repBase + repIndex;
                    matchEnd = repIndex < prefixStartIndex ? dictEnd : iend;
                    assert(match0 != prefixStart && match0 != dictStart);
                    mLength = ip0[-1] == match0[-1] ? 1U : 0U;
                    ip0 -= mLength;
                    match0 -= mLength;
                    assert(1 >= 1);
                    assert(1 <= 3);
                    offcode = 1;
                    mLength += 4;
                    goto _match;
                }
            }

            {
                uint mval = idx >= dictStartIndex ? MEM_read32(idxBase + idx) : MEM_read32(ip0) ^ 1;
                if (MEM_read32(ip0) == mval)
                {
                    goto _offset;
                }
            }

            idx = hashTable[hash1];
            idxBase = idx < prefixStartIndex ? dictBase : @base;
            hash0 = hash1;
            hash1 = ZSTD_hashPtr(ip2, hlog, mls);
            ip0 = ip1;
            ip1 = ip2;
            ip2 = ip3;
            current0 = (uint)(ip0 - @base);
            hashTable[hash0] = current0;
            {
                uint mval = idx >= dictStartIndex ? MEM_read32(idxBase + idx) : MEM_read32(ip0) ^ 1;
                if (MEM_read32(ip0) == mval)
                {
                    goto _offset;
                }
            }

            idx = hashTable[hash1];
            idxBase = idx < prefixStartIndex ? dictBase : @base;
            hash0 = hash1;
            hash1 = ZSTD_hashPtr(ip2, hlog, mls);
            ip0 = ip1;
            ip1 = ip2;
            ip2 = ip0 + step;
            ip3 = ip1 + step;
            if (ip2 >= nextStep)
            {
                step++;
#if NETCOREAPP3_0_OR_GREATER
                if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                {
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(ip1 + 64);
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(ip1 + 128);
                }
#endif

                nextStep += kStepIncr;
            }
        } while (ip3 < ilimit);
        _cleanup:
        offsetSaved2 = offsetSaved1 != 0 && offset_1 != 0 ? offsetSaved1 : offsetSaved2;
        rep[0] = offset_1 != 0 ? offset_1 : offsetSaved1;
        rep[1] = offset_2 != 0 ? offset_2 : offsetSaved2;
        return (nuint)(iend - anchor);
        _offset:
        {
            uint offset = current0 - idx;
            byte* lowMatchPtr = idx < prefixStartIndex ? dictStart : prefixStart;
            matchEnd = idx < prefixStartIndex ? dictEnd : iend;
            match0 = idxBase + idx;
            offset_2 = offset_1;
            offset_1 = offset;
            assert(offset > 0);
            offcode = offset + 3;
            mLength = 4;
            while (ip0 > anchor && match0 > lowMatchPtr && ip0[-1] == match0[-1])
            {
                ip0--;
                match0--;
                mLength++;
            }
        }

        _match:
        assert(matchEnd != null);
        mLength += ZSTD_count_2segments(
            ip0 + mLength,
            match0 + mLength,
            iend,
            matchEnd,
            prefixStart
        );
        ZSTD_storeSeq(seqStore, (nuint)(ip0 - anchor), anchor, iend, offcode, mLength);
        ip0 += mLength;
        anchor = ip0;
        if (ip1 < ip0)
        {
            hashTable[hash1] = (uint)(ip1 - @base);
        }

        if (ip0 <= ilimit)
        {
            assert(@base + current0 + 2 > istart);
            hashTable[ZSTD_hashPtr(@base + current0 + 2, hlog, mls)] = current0 + 2;
            hashTable[ZSTD_hashPtr(ip0 - 2, hlog, mls)] = (uint)(ip0 - 2 - @base);
            while (ip0 <= ilimit)
            {
                uint repIndex2 = (uint)(ip0 - @base) - offset_2;
                byte* repMatch2 =
                    repIndex2 < prefixStartIndex ? dictBase + repIndex2 : @base + repIndex2;
                if (
                    (ZSTD_index_overlap_check(prefixStartIndex, repIndex2) & (offset_2 > 0 ? 1 : 0))
                        != 0
                    && MEM_read32(repMatch2) == MEM_read32(ip0)
                )
                {
                    byte* repEnd2 = repIndex2 < prefixStartIndex ? dictEnd : iend;
                    nuint repLength2 =
                        ZSTD_count_2segments(ip0 + 4, repMatch2 + 4, iend, repEnd2, prefixStart)
                        + 4;
                    {
                        /* swap offset_2 <=> offset_1 */
                        uint tmpOffset = offset_2;
                        offset_2 = offset_1;
                        offset_1 = tmpOffset;
                    }

                    assert(1 >= 1);
                    assert(1 <= 3);
                    ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, repLength2);
                    hashTable[ZSTD_hashPtr(ip0, hlog, mls)] = (uint)(ip0 - @base);
                    ip0 += repLength2;
                    anchor = ip0;
                    continue;
                }

                break;
            }
        }

        goto _start;
    }

    private static nuint ZSTD_compressBlock_fast_extDict_4_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 4, 0);
    }

    private static nuint ZSTD_compressBlock_fast_extDict_5_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 5, 0);
    }

    private static nuint ZSTD_compressBlock_fast_extDict_6_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 6, 0);
    }

    private static nuint ZSTD_compressBlock_fast_extDict_7_0(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 7, 0);
    }

    private static nuint ZSTD_compressBlock_fast_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint mls = ms->cParams.minMatch;
        assert(ms->dictMatchState == null);
        switch (mls)
        {
            default:
            case 4:
                return ZSTD_compressBlock_fast_extDict_4_0(ms, seqStore, rep, src, srcSize);
            case 5:
                return ZSTD_compressBlock_fast_extDict_5_0(ms, seqStore, rep, src, srcSize);
            case 6:
                return ZSTD_compressBlock_fast_extDict_6_0(ms, seqStore, rep, src, srcSize);
            case 7:
                return ZSTD_compressBlock_fast_extDict_7_0(ms, seqStore, rep, src, srcSize);
        }
    }
}
