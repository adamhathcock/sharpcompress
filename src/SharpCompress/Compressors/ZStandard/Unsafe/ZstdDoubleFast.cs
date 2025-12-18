using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static void ZSTD_fillDoubleHashTableForCDict(
        ZSTD_MatchState_t* ms,
        void* end,
        ZSTD_dictTableLoadMethod_e dtlm
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashLarge = ms->hashTable;
        uint hBitsL = cParams->hashLog + 8;
        uint mls = cParams->minMatch;
        uint* hashSmall = ms->chainTable;
        uint hBitsS = cParams->chainLog + 8;
        byte* @base = ms->window.@base;
        byte* ip = @base + ms->nextToUpdate;
        byte* iend = (byte*)end - 8;
        const uint fastHashFillStep = 3;
        for (; ip + fastHashFillStep - 1 <= iend; ip += fastHashFillStep)
        {
            uint curr = (uint)(ip - @base);
            uint i;
            for (i = 0; i < fastHashFillStep; ++i)
            {
                nuint smHashAndTag = ZSTD_hashPtr(ip + i, hBitsS, mls);
                nuint lgHashAndTag = ZSTD_hashPtr(ip + i, hBitsL, 8);
                if (i == 0)
                {
                    ZSTD_writeTaggedIndex(hashSmall, smHashAndTag, curr + i);
                }

                if (i == 0 || hashLarge[lgHashAndTag >> 8] == 0)
                {
                    ZSTD_writeTaggedIndex(hashLarge, lgHashAndTag, curr + i);
                }

                if (dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast)
                    break;
            }
        }
    }

    private static void ZSTD_fillDoubleHashTableForCCtx(
        ZSTD_MatchState_t* ms,
        void* end,
        ZSTD_dictTableLoadMethod_e dtlm
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashLarge = ms->hashTable;
        uint hBitsL = cParams->hashLog;
        uint mls = cParams->minMatch;
        uint* hashSmall = ms->chainTable;
        uint hBitsS = cParams->chainLog;
        byte* @base = ms->window.@base;
        byte* ip = @base + ms->nextToUpdate;
        byte* iend = (byte*)end - 8;
        const uint fastHashFillStep = 3;
        for (; ip + fastHashFillStep - 1 <= iend; ip += fastHashFillStep)
        {
            uint curr = (uint)(ip - @base);
            uint i;
            for (i = 0; i < fastHashFillStep; ++i)
            {
                nuint smHash = ZSTD_hashPtr(ip + i, hBitsS, mls);
                nuint lgHash = ZSTD_hashPtr(ip + i, hBitsL, 8);
                if (i == 0)
                    hashSmall[smHash] = curr + i;
                if (i == 0 || hashLarge[lgHash] == 0)
                    hashLarge[lgHash] = curr + i;
                if (dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast)
                    break;
            }
        }
    }

    private static void ZSTD_fillDoubleHashTable(
        ZSTD_MatchState_t* ms,
        void* end,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_tableFillPurpose_e tfp
    )
    {
        if (tfp == ZSTD_tableFillPurpose_e.ZSTD_tfp_forCDict)
        {
            ZSTD_fillDoubleHashTableForCDict(ms, end, dtlm);
        }
        else
        {
            ZSTD_fillDoubleHashTableForCCtx(ms, end, dtlm);
        }
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<byte> Span_dummy =>
        new byte[10] { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0xe2, 0xb4 };
    private static byte* dummy =>
        (byte*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_dummy)
            );
#else

    private static readonly byte* dummy = GetArrayPointer(
        new byte[10] { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0xe2, 0xb4 }
    );
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_doubleFast_noDict_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        uint mls
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashLong = ms->hashTable;
        uint hBitsL = cParams->hashLog;
        uint* hashSmall = ms->chainTable;
        uint hBitsS = cParams->chainLog;
        byte* @base = ms->window.@base;
        byte* istart = (byte*)src;
        byte* anchor = istart;
        uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
        /* presumes that, if there is a dictionary, it must be using Attach mode */
        uint prefixLowestIndex = ZSTD_getLowestPrefixIndex(ms, endIndex, cParams->windowLog);
        byte* prefixLowest = @base + prefixLowestIndex;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        uint offsetSaved1 = 0,
            offsetSaved2 = 0;
        nuint mLength;
        uint offset;
        uint curr;
        /* how many positions to search before increasing step size */
        const nuint kStepIncr = 1 << 8;
        /* the position at which to increment the step size if no match is found */
        byte* nextStep;
        /* the current step size */
        nuint step;
        /* the long hash at ip */
        nuint hl0;
        /* the long hash at ip1 */
        nuint hl1;
        /* the long match index for ip */
        uint idxl0;
        /* the long match index for ip1 */
        uint idxl1;
        /* the long match for ip */
        byte* matchl0;
        /* the short match for ip */
        byte* matchs0;
        /* the long match for ip1 */
        byte* matchl1;
        /* matchs0 or safe address */
        byte* matchs0_safe;
        /* the current position */
        byte* ip = istart;
        /* the next position */
        byte* ip1;
        ip += ip - prefixLowest == 0 ? 1 : 0;
        {
            uint current = (uint)(ip - @base);
            uint windowLow = ZSTD_getLowestPrefixIndex(ms, current, cParams->windowLog);
            uint maxRep = current - windowLow;
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

        while (true)
        {
            step = 1;
            nextStep = ip + kStepIncr;
            ip1 = ip + step;
            if (ip1 > ilimit)
            {
                goto _cleanup;
            }

            hl0 = ZSTD_hashPtr(ip, hBitsL, 8);
            idxl0 = hashLong[hl0];
            matchl0 = @base + idxl0;
            do
            {
                nuint hs0 = ZSTD_hashPtr(ip, hBitsS, mls);
                uint idxs0 = hashSmall[hs0];
                curr = (uint)(ip - @base);
                matchs0 = @base + idxs0;
                hashLong[hl0] = hashSmall[hs0] = curr;
                if (offset_1 > 0 && MEM_read32(ip + 1 - offset_1) == MEM_read32(ip + 1))
                {
                    mLength = ZSTD_count(ip + 1 + 4, ip + 1 + 4 - offset_1, iend) + 4;
                    ip++;
                    assert(1 >= 1);
                    assert(1 <= 3);
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 1, mLength);
                    goto _match_stored;
                }

                hl1 = ZSTD_hashPtr(ip1, hBitsL, 8);
                {
                    byte* matchl0_safe = ZSTD_selectAddr(
                        idxl0,
                        prefixLowestIndex,
                        matchl0,
                        &dummy[0]
                    );
                    if (MEM_read64(matchl0_safe) == MEM_read64(ip) && matchl0_safe == matchl0)
                    {
                        mLength = ZSTD_count(ip + 8, matchl0 + 8, iend) + 8;
                        offset = (uint)(ip - matchl0);
                        while (ip > anchor && matchl0 > prefixLowest && ip[-1] == matchl0[-1])
                        {
                            ip--;
                            matchl0--;
                            mLength++;
                        }

                        goto _match_found;
                    }
                }

                idxl1 = hashLong[hl1];
                matchl1 = @base + idxl1;
                matchs0_safe = ZSTD_selectAddr(idxs0, prefixLowestIndex, matchs0, &dummy[0]);
                if (MEM_read32(matchs0_safe) == MEM_read32(ip) && matchs0_safe == matchs0)
                {
                    goto _search_next_long;
                }

                if (ip1 >= nextStep)
                {
#if NETCOREAPP3_0_OR_GREATER
                    if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                    {
                        System.Runtime.Intrinsics.X86.Sse.Prefetch0(ip1 + 64);
                        System.Runtime.Intrinsics.X86.Sse.Prefetch0(ip1 + 128);
                    }
#endif

                    step++;
                    nextStep += kStepIncr;
                }

                ip = ip1;
                ip1 += step;
                hl0 = hl1;
                idxl0 = idxl1;
                matchl0 = matchl1;
            } while (ip1 <= ilimit);
            _cleanup:
            offsetSaved2 = offsetSaved1 != 0 && offset_1 != 0 ? offsetSaved1 : offsetSaved2;
            rep[0] = offset_1 != 0 ? offset_1 : offsetSaved1;
            rep[1] = offset_2 != 0 ? offset_2 : offsetSaved2;
            return (nuint)(iend - anchor);
            _search_next_long:
            mLength = ZSTD_count(ip + 4, matchs0 + 4, iend) + 4;
            offset = (uint)(ip - matchs0);
            if (idxl1 > prefixLowestIndex && MEM_read64(matchl1) == MEM_read64(ip1))
            {
                nuint l1len = ZSTD_count(ip1 + 8, matchl1 + 8, iend) + 8;
                if (l1len > mLength)
                {
                    ip = ip1;
                    mLength = l1len;
                    offset = (uint)(ip - matchl1);
                    matchs0 = matchl1;
                }
            }

            while (ip > anchor && matchs0 > prefixLowest && ip[-1] == matchs0[-1])
            {
                ip--;
                matchs0--;
                mLength++;
            }

            _match_found:
            offset_2 = offset_1;
            offset_1 = offset;
            if (step < 4)
            {
                hashLong[hl1] = (uint)(ip1 - @base);
            }

            assert(offset > 0);
            ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + 3, mLength);
            _match_stored:
            ip += mLength;
            anchor = ip;
            if (ip <= ilimit)
            {
                {
                    uint indexToInsert = curr + 2;
                    hashLong[ZSTD_hashPtr(@base + indexToInsert, hBitsL, 8)] = indexToInsert;
                    hashLong[ZSTD_hashPtr(ip - 2, hBitsL, 8)] = (uint)(ip - 2 - @base);
                    hashSmall[ZSTD_hashPtr(@base + indexToInsert, hBitsS, mls)] = indexToInsert;
                    hashSmall[ZSTD_hashPtr(ip - 1, hBitsS, mls)] = (uint)(ip - 1 - @base);
                }

                while (ip <= ilimit && offset_2 > 0 && MEM_read32(ip) == MEM_read32(ip - offset_2))
                {
                    /* store sequence */
                    nuint rLength = ZSTD_count(ip + 4, ip + 4 - offset_2, iend) + 4;
                    /* swap offset_2 <=> offset_1 */
                    uint tmpOff = offset_2;
                    offset_2 = offset_1;
                    offset_1 = tmpOff;
                    hashSmall[ZSTD_hashPtr(ip, hBitsS, mls)] = (uint)(ip - @base);
                    hashLong[ZSTD_hashPtr(ip, hBitsL, 8)] = (uint)(ip - @base);
                    assert(1 >= 1);
                    assert(1 <= 3);
                    ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, rLength);
                    ip += rLength;
                    anchor = ip;
                    continue;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_compressBlock_doubleFast_dictMatchState_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        uint mls
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashLong = ms->hashTable;
        uint hBitsL = cParams->hashLog;
        uint* hashSmall = ms->chainTable;
        uint hBitsS = cParams->chainLog;
        byte* @base = ms->window.@base;
        byte* istart = (byte*)src;
        byte* ip = istart;
        byte* anchor = istart;
        uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
        /* presumes that, if there is a dictionary, it must be using Attach mode */
        uint prefixLowestIndex = ZSTD_getLowestPrefixIndex(ms, endIndex, cParams->windowLog);
        byte* prefixLowest = @base + prefixLowestIndex;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        ZSTD_MatchState_t* dms = ms->dictMatchState;
        ZSTD_compressionParameters* dictCParams = &dms->cParams;
        uint* dictHashLong = dms->hashTable;
        uint* dictHashSmall = dms->chainTable;
        uint dictStartIndex = dms->window.dictLimit;
        byte* dictBase = dms->window.@base;
        byte* dictStart = dictBase + dictStartIndex;
        byte* dictEnd = dms->window.nextSrc;
        uint dictIndexDelta = prefixLowestIndex - (uint)(dictEnd - dictBase);
        uint dictHBitsL = dictCParams->hashLog + 8;
        uint dictHBitsS = dictCParams->chainLog + 8;
        uint dictAndPrefixLength = (uint)(ip - prefixLowest + (dictEnd - dictStart));
        assert(ms->window.dictLimit + (1U << (int)cParams->windowLog) >= endIndex);
        if (ms->prefetchCDictTables != 0)
        {
            nuint hashTableBytes = ((nuint)1 << (int)dictCParams->hashLog) * sizeof(uint);
            nuint chainTableBytes = ((nuint)1 << (int)dictCParams->chainLog) * sizeof(uint);
            {
                sbyte* _ptr = (sbyte*)dictHashLong;
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

            {
                sbyte* _ptr = (sbyte*)dictHashSmall;
                nuint _size = chainTableBytes;
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

        ip += dictAndPrefixLength == 0 ? 1 : 0;
        assert(offset_1 <= dictAndPrefixLength);
        assert(offset_2 <= dictAndPrefixLength);
        while (ip < ilimit)
        {
            nuint mLength;
            uint offset;
            nuint h2 = ZSTD_hashPtr(ip, hBitsL, 8);
            nuint h = ZSTD_hashPtr(ip, hBitsS, mls);
            nuint dictHashAndTagL = ZSTD_hashPtr(ip, dictHBitsL, 8);
            nuint dictHashAndTagS = ZSTD_hashPtr(ip, dictHBitsS, mls);
            uint dictMatchIndexAndTagL = dictHashLong[dictHashAndTagL >> 8];
            uint dictMatchIndexAndTagS = dictHashSmall[dictHashAndTagS >> 8];
            int dictTagsMatchL = ZSTD_comparePackedTags(dictMatchIndexAndTagL, dictHashAndTagL);
            int dictTagsMatchS = ZSTD_comparePackedTags(dictMatchIndexAndTagS, dictHashAndTagS);
            uint curr = (uint)(ip - @base);
            uint matchIndexL = hashLong[h2];
            uint matchIndexS = hashSmall[h];
            byte* matchLong = @base + matchIndexL;
            byte* match = @base + matchIndexS;
            uint repIndex = curr + 1 - offset_1;
            byte* repMatch =
                repIndex < prefixLowestIndex
                    ? dictBase + (repIndex - dictIndexDelta)
                    : @base + repIndex;
            hashLong[h2] = hashSmall[h] = curr;
            if (
                ZSTD_index_overlap_check(prefixLowestIndex, repIndex) != 0
                && MEM_read32(repMatch) == MEM_read32(ip + 1)
            )
            {
                byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;
                mLength =
                    ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixLowest)
                    + 4;
                ip++;
                assert(1 >= 1);
                assert(1 <= 3);
                ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 1, mLength);
                goto _match_stored;
            }

            if (matchIndexL >= prefixLowestIndex && MEM_read64(matchLong) == MEM_read64(ip))
            {
                mLength = ZSTD_count(ip + 8, matchLong + 8, iend) + 8;
                offset = (uint)(ip - matchLong);
                while (ip > anchor && matchLong > prefixLowest && ip[-1] == matchLong[-1])
                {
                    ip--;
                    matchLong--;
                    mLength++;
                }

                goto _match_found;
            }
            else if (dictTagsMatchL != 0)
            {
                /* check dictMatchState long match */
                uint dictMatchIndexL = dictMatchIndexAndTagL >> 8;
                byte* dictMatchL = dictBase + dictMatchIndexL;
                assert(dictMatchL < dictEnd);
                if (dictMatchL > dictStart && MEM_read64(dictMatchL) == MEM_read64(ip))
                {
                    mLength =
                        ZSTD_count_2segments(ip + 8, dictMatchL + 8, iend, dictEnd, prefixLowest)
                        + 8;
                    offset = curr - dictMatchIndexL - dictIndexDelta;
                    while (ip > anchor && dictMatchL > dictStart && ip[-1] == dictMatchL[-1])
                    {
                        ip--;
                        dictMatchL--;
                        mLength++;
                    }

                    goto _match_found;
                }
            }

            if (matchIndexS > prefixLowestIndex)
            {
                if (MEM_read32(match) == MEM_read32(ip))
                {
                    goto _search_next_long;
                }
            }
            else if (dictTagsMatchS != 0)
            {
                /* check dictMatchState short match */
                uint dictMatchIndexS = dictMatchIndexAndTagS >> 8;
                match = dictBase + dictMatchIndexS;
                matchIndexS = dictMatchIndexS + dictIndexDelta;
                if (match > dictStart && MEM_read32(match) == MEM_read32(ip))
                {
                    goto _search_next_long;
                }
            }

            ip += (ip - anchor >> 8) + 1;
            continue;
            _search_next_long:
            {
                nuint hl3 = ZSTD_hashPtr(ip + 1, hBitsL, 8);
                nuint dictHashAndTagL3 = ZSTD_hashPtr(ip + 1, dictHBitsL, 8);
                uint matchIndexL3 = hashLong[hl3];
                uint dictMatchIndexAndTagL3 = dictHashLong[dictHashAndTagL3 >> 8];
                int dictTagsMatchL3 = ZSTD_comparePackedTags(
                    dictMatchIndexAndTagL3,
                    dictHashAndTagL3
                );
                byte* matchL3 = @base + matchIndexL3;
                hashLong[hl3] = curr + 1;
                if (matchIndexL3 >= prefixLowestIndex && MEM_read64(matchL3) == MEM_read64(ip + 1))
                {
                    mLength = ZSTD_count(ip + 9, matchL3 + 8, iend) + 8;
                    ip++;
                    offset = (uint)(ip - matchL3);
                    while (ip > anchor && matchL3 > prefixLowest && ip[-1] == matchL3[-1])
                    {
                        ip--;
                        matchL3--;
                        mLength++;
                    }

                    goto _match_found;
                }
                else if (dictTagsMatchL3 != 0)
                {
                    /* check dict long +1 match */
                    uint dictMatchIndexL3 = dictMatchIndexAndTagL3 >> 8;
                    byte* dictMatchL3 = dictBase + dictMatchIndexL3;
                    assert(dictMatchL3 < dictEnd);
                    if (dictMatchL3 > dictStart && MEM_read64(dictMatchL3) == MEM_read64(ip + 1))
                    {
                        mLength =
                            ZSTD_count_2segments(
                                ip + 1 + 8,
                                dictMatchL3 + 8,
                                iend,
                                dictEnd,
                                prefixLowest
                            ) + 8;
                        ip++;
                        offset = curr + 1 - dictMatchIndexL3 - dictIndexDelta;
                        while (ip > anchor && dictMatchL3 > dictStart && ip[-1] == dictMatchL3[-1])
                        {
                            ip--;
                            dictMatchL3--;
                            mLength++;
                        }

                        goto _match_found;
                    }
                }
            }

            if (matchIndexS < prefixLowestIndex)
            {
                mLength = ZSTD_count_2segments(ip + 4, match + 4, iend, dictEnd, prefixLowest) + 4;
                offset = curr - matchIndexS;
                while (ip > anchor && match > dictStart && ip[-1] == match[-1])
                {
                    ip--;
                    match--;
                    mLength++;
                }
            }
            else
            {
                mLength = ZSTD_count(ip + 4, match + 4, iend) + 4;
                offset = (uint)(ip - match);
                while (ip > anchor && match > prefixLowest && ip[-1] == match[-1])
                {
                    ip--;
                    match--;
                    mLength++;
                }
            }

            _match_found:
            offset_2 = offset_1;
            offset_1 = offset;
            assert(offset > 0);
            ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + 3, mLength);
            _match_stored:
            ip += mLength;
            anchor = ip;
            if (ip <= ilimit)
            {
                {
                    uint indexToInsert = curr + 2;
                    hashLong[ZSTD_hashPtr(@base + indexToInsert, hBitsL, 8)] = indexToInsert;
                    hashLong[ZSTD_hashPtr(ip - 2, hBitsL, 8)] = (uint)(ip - 2 - @base);
                    hashSmall[ZSTD_hashPtr(@base + indexToInsert, hBitsS, mls)] = indexToInsert;
                    hashSmall[ZSTD_hashPtr(ip - 1, hBitsS, mls)] = (uint)(ip - 1 - @base);
                }

                while (ip <= ilimit)
                {
                    uint current2 = (uint)(ip - @base);
                    uint repIndex2 = current2 - offset_2;
                    byte* repMatch2 =
                        repIndex2 < prefixLowestIndex
                            ? dictBase + repIndex2 - dictIndexDelta
                            : @base + repIndex2;
                    if (
                        ZSTD_index_overlap_check(prefixLowestIndex, repIndex2) != 0
                        && MEM_read32(repMatch2) == MEM_read32(ip)
                    )
                    {
                        byte* repEnd2 = repIndex2 < prefixLowestIndex ? dictEnd : iend;
                        nuint repLength2 =
                            ZSTD_count_2segments(ip + 4, repMatch2 + 4, iend, repEnd2, prefixLowest)
                            + 4;
                        /* swap offset_2 <=> offset_1 */
                        uint tmpOffset = offset_2;
                        offset_2 = offset_1;
                        offset_1 = tmpOffset;
                        assert(1 >= 1);
                        assert(1 <= 3);
                        ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, repLength2);
                        hashSmall[ZSTD_hashPtr(ip, hBitsS, mls)] = current2;
                        hashLong[ZSTD_hashPtr(ip, hBitsL, 8)] = current2;
                        ip += repLength2;
                        anchor = ip;
                        continue;
                    }

                    break;
                }
            }
        }

        rep[0] = offset_1;
        rep[1] = offset_2;
        return (nuint)(iend - anchor);
    }

    private static nuint ZSTD_compressBlock_doubleFast_noDict_4(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_noDict_generic(ms, seqStore, rep, src, srcSize, 4);
    }

    private static nuint ZSTD_compressBlock_doubleFast_noDict_5(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_noDict_generic(ms, seqStore, rep, src, srcSize, 5);
    }

    private static nuint ZSTD_compressBlock_doubleFast_noDict_6(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_noDict_generic(ms, seqStore, rep, src, srcSize, 6);
    }

    private static nuint ZSTD_compressBlock_doubleFast_noDict_7(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_noDict_generic(ms, seqStore, rep, src, srcSize, 7);
    }

    private static nuint ZSTD_compressBlock_doubleFast_dictMatchState_4(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            4
        );
    }

    private static nuint ZSTD_compressBlock_doubleFast_dictMatchState_5(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            5
        );
    }

    private static nuint ZSTD_compressBlock_doubleFast_dictMatchState_6(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            6
        );
    }

    private static nuint ZSTD_compressBlock_doubleFast_dictMatchState_7(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_dictMatchState_generic(
            ms,
            seqStore,
            rep,
            src,
            srcSize,
            7
        );
    }

    private static nuint ZSTD_compressBlock_doubleFast(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint mls = ms->cParams.minMatch;
        switch (mls)
        {
            default:
            case 4:
                return ZSTD_compressBlock_doubleFast_noDict_4(ms, seqStore, rep, src, srcSize);
            case 5:
                return ZSTD_compressBlock_doubleFast_noDict_5(ms, seqStore, rep, src, srcSize);
            case 6:
                return ZSTD_compressBlock_doubleFast_noDict_6(ms, seqStore, rep, src, srcSize);
            case 7:
                return ZSTD_compressBlock_doubleFast_noDict_7(ms, seqStore, rep, src, srcSize);
        }
    }

    private static nuint ZSTD_compressBlock_doubleFast_dictMatchState(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint mls = ms->cParams.minMatch;
        switch (mls)
        {
            default:
            case 4:
                return ZSTD_compressBlock_doubleFast_dictMatchState_4(
                    ms,
                    seqStore,
                    rep,
                    src,
                    srcSize
                );
            case 5:
                return ZSTD_compressBlock_doubleFast_dictMatchState_5(
                    ms,
                    seqStore,
                    rep,
                    src,
                    srcSize
                );
            case 6:
                return ZSTD_compressBlock_doubleFast_dictMatchState_6(
                    ms,
                    seqStore,
                    rep,
                    src,
                    srcSize
                );
            case 7:
                return ZSTD_compressBlock_doubleFast_dictMatchState_7(
                    ms,
                    seqStore,
                    rep,
                    src,
                    srcSize
                );
        }
    }

    private static nuint ZSTD_compressBlock_doubleFast_extDict_generic(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize,
        uint mls
    )
    {
        ZSTD_compressionParameters* cParams = &ms->cParams;
        uint* hashLong = ms->hashTable;
        uint hBitsL = cParams->hashLog;
        uint* hashSmall = ms->chainTable;
        uint hBitsS = cParams->chainLog;
        byte* istart = (byte*)src;
        byte* ip = istart;
        byte* anchor = istart;
        byte* iend = istart + srcSize;
        byte* ilimit = iend - 8;
        byte* @base = ms->window.@base;
        uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
        uint lowLimit = ZSTD_getLowestMatchIndex(ms, endIndex, cParams->windowLog);
        uint dictStartIndex = lowLimit;
        uint dictLimit = ms->window.dictLimit;
        uint prefixStartIndex = dictLimit > lowLimit ? dictLimit : lowLimit;
        byte* prefixStart = @base + prefixStartIndex;
        byte* dictBase = ms->window.dictBase;
        byte* dictStart = dictBase + dictStartIndex;
        byte* dictEnd = dictBase + prefixStartIndex;
        uint offset_1 = rep[0],
            offset_2 = rep[1];
        if (prefixStartIndex == dictStartIndex)
            return ZSTD_compressBlock_doubleFast(ms, seqStore, rep, src, srcSize);
        while (ip < ilimit)
        {
            nuint hSmall = ZSTD_hashPtr(ip, hBitsS, mls);
            uint matchIndex = hashSmall[hSmall];
            byte* matchBase = matchIndex < prefixStartIndex ? dictBase : @base;
            byte* match = matchBase + matchIndex;
            nuint hLong = ZSTD_hashPtr(ip, hBitsL, 8);
            uint matchLongIndex = hashLong[hLong];
            byte* matchLongBase = matchLongIndex < prefixStartIndex ? dictBase : @base;
            byte* matchLong = matchLongBase + matchLongIndex;
            uint curr = (uint)(ip - @base);
            /* offset_1 expected <= curr +1 */
            uint repIndex = curr + 1 - offset_1;
            byte* repBase = repIndex < prefixStartIndex ? dictBase : @base;
            byte* repMatch = repBase + repIndex;
            nuint mLength;
            hashSmall[hSmall] = hashLong[hLong] = curr;
            if (
                (
                    ZSTD_index_overlap_check(prefixStartIndex, repIndex)
                    & (offset_1 <= curr + 1 - dictStartIndex ? 1 : 0)
                ) != 0
                && MEM_read32(repMatch) == MEM_read32(ip + 1)
            )
            {
                byte* repMatchEnd = repIndex < prefixStartIndex ? dictEnd : iend;
                mLength =
                    ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixStart)
                    + 4;
                ip++;
                assert(1 >= 1);
                assert(1 <= 3);
                ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 1, mLength);
            }
            else
            {
                if (matchLongIndex > dictStartIndex && MEM_read64(matchLong) == MEM_read64(ip))
                {
                    byte* matchEnd = matchLongIndex < prefixStartIndex ? dictEnd : iend;
                    byte* lowMatchPtr = matchLongIndex < prefixStartIndex ? dictStart : prefixStart;
                    uint offset;
                    mLength =
                        ZSTD_count_2segments(ip + 8, matchLong + 8, iend, matchEnd, prefixStart)
                        + 8;
                    offset = curr - matchLongIndex;
                    while (ip > anchor && matchLong > lowMatchPtr && ip[-1] == matchLong[-1])
                    {
                        ip--;
                        matchLong--;
                        mLength++;
                    }

                    offset_2 = offset_1;
                    offset_1 = offset;
                    assert(offset > 0);
                    ZSTD_storeSeq(
                        seqStore,
                        (nuint)(ip - anchor),
                        anchor,
                        iend,
                        offset + 3,
                        mLength
                    );
                }
                else if (matchIndex > dictStartIndex && MEM_read32(match) == MEM_read32(ip))
                {
                    nuint h3 = ZSTD_hashPtr(ip + 1, hBitsL, 8);
                    uint matchIndex3 = hashLong[h3];
                    byte* match3Base = matchIndex3 < prefixStartIndex ? dictBase : @base;
                    byte* match3 = match3Base + matchIndex3;
                    uint offset;
                    hashLong[h3] = curr + 1;
                    if (matchIndex3 > dictStartIndex && MEM_read64(match3) == MEM_read64(ip + 1))
                    {
                        byte* matchEnd = matchIndex3 < prefixStartIndex ? dictEnd : iend;
                        byte* lowMatchPtr =
                            matchIndex3 < prefixStartIndex ? dictStart : prefixStart;
                        mLength =
                            ZSTD_count_2segments(ip + 9, match3 + 8, iend, matchEnd, prefixStart)
                            + 8;
                        ip++;
                        offset = curr + 1 - matchIndex3;
                        while (ip > anchor && match3 > lowMatchPtr && ip[-1] == match3[-1])
                        {
                            ip--;
                            match3--;
                            mLength++;
                        }
                    }
                    else
                    {
                        byte* matchEnd = matchIndex < prefixStartIndex ? dictEnd : iend;
                        byte* lowMatchPtr = matchIndex < prefixStartIndex ? dictStart : prefixStart;
                        mLength =
                            ZSTD_count_2segments(ip + 4, match + 4, iend, matchEnd, prefixStart)
                            + 4;
                        offset = curr - matchIndex;
                        while (ip > anchor && match > lowMatchPtr && ip[-1] == match[-1])
                        {
                            ip--;
                            match--;
                            mLength++;
                        }
                    }

                    offset_2 = offset_1;
                    offset_1 = offset;
                    assert(offset > 0);
                    ZSTD_storeSeq(
                        seqStore,
                        (nuint)(ip - anchor),
                        anchor,
                        iend,
                        offset + 3,
                        mLength
                    );
                }
                else
                {
                    ip += (ip - anchor >> 8) + 1;
                    continue;
                }
            }

            ip += mLength;
            anchor = ip;
            if (ip <= ilimit)
            {
                {
                    uint indexToInsert = curr + 2;
                    hashLong[ZSTD_hashPtr(@base + indexToInsert, hBitsL, 8)] = indexToInsert;
                    hashLong[ZSTD_hashPtr(ip - 2, hBitsL, 8)] = (uint)(ip - 2 - @base);
                    hashSmall[ZSTD_hashPtr(@base + indexToInsert, hBitsS, mls)] = indexToInsert;
                    hashSmall[ZSTD_hashPtr(ip - 1, hBitsS, mls)] = (uint)(ip - 1 - @base);
                }

                while (ip <= ilimit)
                {
                    uint current2 = (uint)(ip - @base);
                    uint repIndex2 = current2 - offset_2;
                    byte* repMatch2 =
                        repIndex2 < prefixStartIndex ? dictBase + repIndex2 : @base + repIndex2;
                    if (
                        (
                            ZSTD_index_overlap_check(prefixStartIndex, repIndex2)
                            & (offset_2 <= current2 - dictStartIndex ? 1 : 0)
                        ) != 0
                        && MEM_read32(repMatch2) == MEM_read32(ip)
                    )
                    {
                        byte* repEnd2 = repIndex2 < prefixStartIndex ? dictEnd : iend;
                        nuint repLength2 =
                            ZSTD_count_2segments(ip + 4, repMatch2 + 4, iend, repEnd2, prefixStart)
                            + 4;
                        /* swap offset_2 <=> offset_1 */
                        uint tmpOffset = offset_2;
                        offset_2 = offset_1;
                        offset_1 = tmpOffset;
                        assert(1 >= 1);
                        assert(1 <= 3);
                        ZSTD_storeSeq(seqStore, 0, anchor, iend, 1, repLength2);
                        hashSmall[ZSTD_hashPtr(ip, hBitsS, mls)] = current2;
                        hashLong[ZSTD_hashPtr(ip, hBitsL, 8)] = current2;
                        ip += repLength2;
                        anchor = ip;
                        continue;
                    }

                    break;
                }
            }
        }

        rep[0] = offset_1;
        rep[1] = offset_2;
        return (nuint)(iend - anchor);
    }

    private static nuint ZSTD_compressBlock_doubleFast_extDict_4(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 4);
    }

    private static nuint ZSTD_compressBlock_doubleFast_extDict_5(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 5);
    }

    private static nuint ZSTD_compressBlock_doubleFast_extDict_6(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 6);
    }

    private static nuint ZSTD_compressBlock_doubleFast_extDict_7(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 7);
    }

    private static nuint ZSTD_compressBlock_doubleFast_extDict(
        ZSTD_MatchState_t* ms,
        SeqStore_t* seqStore,
        uint* rep,
        void* src,
        nuint srcSize
    )
    {
        uint mls = ms->cParams.minMatch;
        switch (mls)
        {
            default:
            case 4:
                return ZSTD_compressBlock_doubleFast_extDict_4(ms, seqStore, rep, src, srcSize);
            case 5:
                return ZSTD_compressBlock_doubleFast_extDict_5(ms, seqStore, rep, src, srcSize);
            case 6:
                return ZSTD_compressBlock_doubleFast_extDict_6(ms, seqStore, rep, src, srcSize);
            case 7:
                return ZSTD_compressBlock_doubleFast_extDict_7(ms, seqStore, rep, src, srcSize);
        }
    }
}
