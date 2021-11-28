using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static void ZSTD_fillDoubleHashTable(ZSTD_matchState_t* ms, void* end, ZSTD_dictTableLoadMethod_e dtlm)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashLarge = ms->hashTable;
            uint hBitsL = cParams->hashLog;
            uint mls = cParams->minMatch;
            uint* hashSmall = ms->chainTable;
            uint hBitsS = cParams->chainLog;
            byte* @base = ms->window.@base;
            byte* ip = @base + ms->nextToUpdate;
            byte* iend = ((byte*)(end)) - 8;
            uint fastHashFillStep = 3;

            for (; ip + fastHashFillStep - 1 <= iend; ip += fastHashFillStep)
            {
                uint curr = (uint)(ip - @base);
                uint i;

                for (i = 0; i < fastHashFillStep; ++i)
                {
                    nuint smHash = ZSTD_hashPtr((void*)(ip + i), hBitsS, mls);
                    nuint lgHash = ZSTD_hashPtr((void*)(ip + i), hBitsL, 8);

                    if (i == 0)
                    {
                        hashSmall[smHash] = curr + i;
                    }

                    if (i == 0 || hashLarge[lgHash] == 0)
                    {
                        hashLarge[lgHash] = curr + i;
                    }

                    if (dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast)
                    {
                        break;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_compressBlock_doubleFast_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, uint mls, ZSTD_dictMode_e dictMode)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashLong = ms->hashTable;
            uint hBitsL = cParams->hashLog;
            uint* hashSmall = ms->chainTable;
            uint hBitsS = cParams->chainLog;
            byte* @base = ms->window.@base;
            byte* istart = (byte*)(src);
            byte* ip = istart;
            byte* anchor = istart;
            uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
            uint prefixLowestIndex = ZSTD_getLowestPrefixIndex(ms, endIndex, cParams->windowLog);
            byte* prefixLowest = @base + prefixLowestIndex;
            byte* iend = istart + srcSize;
            byte* ilimit = iend - 8;
            uint offset_1 = rep[0], offset_2 = rep[1];
            uint offsetSaved = 0;
            ZSTD_matchState_t* dms = ms->dictMatchState;
            ZSTD_compressionParameters* dictCParams = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? &dms->cParams : null;
            uint* dictHashLong = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->hashTable : null;
            uint* dictHashSmall = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->chainTable : null;
            uint dictStartIndex = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.dictLimit : 0;
            byte* dictBase = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.@base : null;
            byte* dictStart = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dictBase + dictStartIndex : null;
            byte* dictEnd = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dms->window.nextSrc : null;
            uint dictIndexDelta = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? prefixLowestIndex - (uint)(dictEnd - dictBase) : 0;
            uint dictHBitsL = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dictCParams->hashLog : hBitsL;
            uint dictHBitsS = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState ? dictCParams->chainLog : hBitsS;
            uint dictAndPrefixLength = (uint)((ip - prefixLowest) + (dictEnd - dictStart));

            assert(dictMode == ZSTD_dictMode_e.ZSTD_noDict || dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState);
            if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
            {
                assert(ms->window.dictLimit + (1U << (int)cParams->windowLog) >= endIndex);
            }

            ip += ((dictAndPrefixLength == 0) ? 1 : 0);
            if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
            {
                uint curr = (uint)(ip - @base);
                uint windowLow = ZSTD_getLowestPrefixIndex(ms, curr, cParams->windowLog);
                uint maxRep = curr - windowLow;

                if (offset_2 > maxRep)
                {
                    offsetSaved = offset_2; offset_2 = 0;
                }

                if (offset_1 > maxRep)
                {
                    offsetSaved = offset_1; offset_1 = 0;
                }
            }

            if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
            {
                assert(offset_1 <= dictAndPrefixLength);
                assert(offset_2 <= dictAndPrefixLength);
            }

            while (ip < ilimit)
            {
                nuint mLength;
                uint offset;
                nuint h2 = ZSTD_hashPtr((void*)ip, hBitsL, 8);
                nuint h = ZSTD_hashPtr((void*)ip, hBitsS, mls);
                nuint dictHL = ZSTD_hashPtr((void*)ip, dictHBitsL, 8);
                nuint dictHS = ZSTD_hashPtr((void*)ip, dictHBitsS, mls);
                uint curr = (uint)(ip - @base);
                uint matchIndexL = hashLong[h2];
                uint matchIndexS = hashSmall[h];
                byte* matchLong = @base + matchIndexL;
                byte* match = @base + matchIndexS;
                uint repIndex = curr + 1 - offset_1;
                byte* repMatch = (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && repIndex < prefixLowestIndex) ? dictBase + (repIndex - dictIndexDelta) : @base + repIndex;

                hashLong[h2] = hashSmall[h] = curr;
                if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && ((uint)((prefixLowestIndex - 1) - repIndex) >= 3) && (MEM_read32((void*)repMatch) == MEM_read32((void*)(ip + 1))))
                {
                    byte* repMatchEnd = repIndex < prefixLowestIndex ? dictEnd : iend;

                    mLength = ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixLowest) + 4;
                    ip++;
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 0, mLength - 3);
                    goto _match_stored;
                }

                if (dictMode == ZSTD_dictMode_e.ZSTD_noDict && (((offset_1 > 0) && (MEM_read32((void*)(ip + 1 - offset_1)) == MEM_read32((void*)(ip + 1))))))
                {
                    mLength = ZSTD_count(ip + 1 + 4, ip + 1 + 4 - offset_1, iend) + 4;
                    ip++;
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 0, mLength - 3);
                    goto _match_stored;
                }

                if (matchIndexL > prefixLowestIndex)
                {
                    if (MEM_read64((void*)matchLong) == MEM_read64((void*)ip))
                    {
                        mLength = ZSTD_count(ip + 8, matchLong + 8, iend) + 8;
                        offset = (uint)(ip - matchLong);
                        while ((((ip > anchor) && (matchLong > prefixLowest))) && (ip[-1] == matchLong[-1]))
                        {
                            ip--;
                            matchLong--;
                            mLength++;
                        }

                        goto _match_found;
                    }
                }
                else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
                {
                    uint dictMatchIndexL = dictHashLong[dictHL];
                    byte* dictMatchL = dictBase + dictMatchIndexL;

                    assert(dictMatchL < dictEnd);
                    if (dictMatchL > dictStart && MEM_read64((void*)dictMatchL) == MEM_read64((void*)ip))
                    {
                        mLength = ZSTD_count_2segments(ip + 8, dictMatchL + 8, iend, dictEnd, prefixLowest) + 8;
                        offset = (uint)(curr - dictMatchIndexL - dictIndexDelta);
                        while ((((ip > anchor) && (dictMatchL > dictStart))) && (ip[-1] == dictMatchL[-1]))
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
                    if (MEM_read32((void*)match) == MEM_read32((void*)ip))
                    {
                        goto _search_next_long;
                    }
                }
                else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
                {
                    uint dictMatchIndexS = dictHashSmall[dictHS];

                    match = dictBase + dictMatchIndexS;
                    matchIndexS = dictMatchIndexS + dictIndexDelta;
                    if (match > dictStart && MEM_read32((void*)match) == MEM_read32((void*)ip))
                    {
                        goto _search_next_long;
                    }
                }

                ip += ((ip - anchor) >> 8) + 1;
                continue;
                _search_next_long:
                        {
                    nuint hl3 = ZSTD_hashPtr((void*)(ip + 1), hBitsL, 8);
                    nuint dictHLNext = ZSTD_hashPtr((void*)(ip + 1), dictHBitsL, 8);
                    uint matchIndexL3 = hashLong[hl3];
                    byte* matchL3 = @base + matchIndexL3;

                    hashLong[hl3] = curr + 1;
                    if (matchIndexL3 > prefixLowestIndex)
                    {
                        if (MEM_read64((void*)matchL3) == MEM_read64((void*)(ip + 1)))
                        {
                            mLength = ZSTD_count(ip + 9, matchL3 + 8, iend) + 8;
                            ip++;
                            offset = (uint)(ip - matchL3);
                            while ((((ip > anchor) && (matchL3 > prefixLowest))) && (ip[-1] == matchL3[-1]))
                            {
                                ip--;
                                matchL3--;
                                mLength++;
                            }

                            goto _match_found;
                        }
                    }
                    else if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
                    {
                        uint dictMatchIndexL3 = dictHashLong[dictHLNext];
                        byte* dictMatchL3 = dictBase + dictMatchIndexL3;

                        assert(dictMatchL3 < dictEnd);
                        if (dictMatchL3 > dictStart && MEM_read64((void*)dictMatchL3) == MEM_read64((void*)(ip + 1)))
                        {
                            mLength = ZSTD_count_2segments(ip + 1 + 8, dictMatchL3 + 8, iend, dictEnd, prefixLowest) + 8;
                            ip++;
                            offset = (uint)(curr + 1 - dictMatchIndexL3 - dictIndexDelta);
                            while ((((ip > anchor) && (dictMatchL3 > dictStart))) && (ip[-1] == dictMatchL3[-1]))
                            {
                                ip--;
                                dictMatchL3--;
                                mLength++;
                            }

                            goto _match_found;
                        }
                    }
                }

                if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && matchIndexS < prefixLowestIndex)
                {
                    mLength = ZSTD_count_2segments(ip + 4, match + 4, iend, dictEnd, prefixLowest) + 4;
                    offset = (uint)(curr - matchIndexS);
                    while ((((ip > anchor) && (match > dictStart))) && (ip[-1] == match[-1]))
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
                    while ((((ip > anchor) && (match > prefixLowest))) && (ip[-1] == match[-1]))
                    {
                        ip--;
                        match--;
                        mLength++;
                    }
                }

                _match_found:
                offset_2 = offset_1;
                offset_1 = offset;
                ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + (uint)((3 - 1)), mLength - 3);
                _match_stored:
                ip += mLength;
                anchor = ip;
                if (ip <= ilimit)
                {

                    {
                        uint indexToInsert = curr + 2;

                        hashLong[ZSTD_hashPtr((void*)(@base + indexToInsert), hBitsL, 8)] = indexToInsert;
                        hashLong[ZSTD_hashPtr((void*)(ip - 2), hBitsL, 8)] = (uint)(ip - 2 - @base);
                        hashSmall[ZSTD_hashPtr((void*)(@base + indexToInsert), hBitsS, mls)] = indexToInsert;
                        hashSmall[ZSTD_hashPtr((void*)(ip - 1), hBitsS, mls)] = (uint)(ip - 1 - @base);
                    }

                    if (dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState)
                    {
                        while (ip <= ilimit)
                        {
                            uint current2 = (uint)(ip - @base);
                            uint repIndex2 = current2 - offset_2;
                            byte* repMatch2 = dictMode == ZSTD_dictMode_e.ZSTD_dictMatchState && repIndex2 < prefixLowestIndex ? dictBase + repIndex2 - dictIndexDelta : @base + repIndex2;

                            if (((uint)((prefixLowestIndex - 1) - (uint)(repIndex2)) >= 3) && (MEM_read32((void*)repMatch2) == MEM_read32((void*)ip)))
                            {
                                byte* repEnd2 = repIndex2 < prefixLowestIndex ? dictEnd : iend;
                                nuint repLength2 = ZSTD_count_2segments(ip + 4, repMatch2 + 4, iend, repEnd2, prefixLowest) + 4;
                                uint tmpOffset = offset_2;

                                offset_2 = offset_1;
                                offset_1 = tmpOffset;
                                ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, repLength2 - 3);
                                hashSmall[ZSTD_hashPtr((void*)ip, hBitsS, mls)] = current2;
                                hashLong[ZSTD_hashPtr((void*)ip, hBitsL, 8)] = current2;
                                ip += repLength2;
                                anchor = ip;
                                continue;
                            }

                            break;
                        }
                    }

                    if (dictMode == ZSTD_dictMode_e.ZSTD_noDict)
                    {
                        while ((ip <= ilimit) && (((offset_2 > 0) && (MEM_read32((void*)ip) == MEM_read32((void*)(ip - offset_2))))))
                        {
                            nuint rLength = ZSTD_count(ip + 4, ip + 4 - offset_2, iend) + 4;
                            uint tmpOff = offset_2;

                            offset_2 = offset_1;
                            offset_1 = tmpOff;
                            hashSmall[ZSTD_hashPtr((void*)ip, hBitsS, mls)] = (uint)(ip - @base);
                            hashLong[ZSTD_hashPtr((void*)ip, hBitsL, 8)] = (uint)(ip - @base);
                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, rLength - 3);
                            ip += rLength;
                            anchor = ip;
                            continue;
                        }
                    }
                }
            }

            rep[0] = offset_1 != 0 ? offset_1 : offsetSaved;
            rep[1] = offset_2 != 0 ? offset_2 : offsetSaved;
            return (nuint)(iend - anchor);
        }

        public static nuint ZSTD_compressBlock_doubleFast(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint mls = ms->cParams.minMatch;

            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 4, ZSTD_dictMode_e.ZSTD_noDict);
                }

                case 5:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 5, ZSTD_dictMode_e.ZSTD_noDict);
                }

                case 6:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 6, ZSTD_dictMode_e.ZSTD_noDict);
                }

                case 7:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 7, ZSTD_dictMode_e.ZSTD_noDict);
                }
            }
        }

        public static nuint ZSTD_compressBlock_doubleFast_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint mls = ms->cParams.minMatch;

            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 4, ZSTD_dictMode_e.ZSTD_dictMatchState);
                }

                case 5:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 5, ZSTD_dictMode_e.ZSTD_dictMatchState);
                }

                case 6:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 6, ZSTD_dictMode_e.ZSTD_dictMatchState);
                }

                case 7:
                {
                    return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, 7, ZSTD_dictMode_e.ZSTD_dictMatchState);
                }
            }
        }

        private static nuint ZSTD_compressBlock_doubleFast_extDict_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, uint mls)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashLong = ms->hashTable;
            uint hBitsL = cParams->hashLog;
            uint* hashSmall = ms->chainTable;
            uint hBitsS = cParams->chainLog;
            byte* istart = (byte*)(src);
            byte* ip = istart;
            byte* anchor = istart;
            byte* iend = istart + srcSize;
            byte* ilimit = iend - 8;
            byte* @base = ms->window.@base;
            uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
            uint lowLimit = ZSTD_getLowestMatchIndex(ms, endIndex, cParams->windowLog);
            uint dictStartIndex = lowLimit;
            uint dictLimit = ms->window.dictLimit;
            uint prefixStartIndex = (dictLimit > lowLimit) ? dictLimit : lowLimit;
            byte* prefixStart = @base + prefixStartIndex;
            byte* dictBase = ms->window.dictBase;
            byte* dictStart = dictBase + dictStartIndex;
            byte* dictEnd = dictBase + prefixStartIndex;
            uint offset_1 = rep[0], offset_2 = rep[1];

            if (prefixStartIndex == dictStartIndex)
            {
                return ZSTD_compressBlock_doubleFast_generic(ms, seqStore, rep, src, srcSize, mls, ZSTD_dictMode_e.ZSTD_noDict);
            }

            while (ip < ilimit)
            {
                nuint hSmall = ZSTD_hashPtr((void*)ip, hBitsS, mls);
                uint matchIndex = hashSmall[hSmall];
                byte* matchBase = matchIndex < prefixStartIndex ? dictBase : @base;
                byte* match = matchBase + matchIndex;
                nuint hLong = ZSTD_hashPtr((void*)ip, hBitsL, 8);
                uint matchLongIndex = hashLong[hLong];
                byte* matchLongBase = matchLongIndex < prefixStartIndex ? dictBase : @base;
                byte* matchLong = matchLongBase + matchLongIndex;
                uint curr = (uint)(ip - @base);
                uint repIndex = curr + 1 - offset_1;
                byte* repBase = repIndex < prefixStartIndex ? dictBase : @base;
                byte* repMatch = repBase + repIndex;
                nuint mLength;

                hashSmall[hSmall] = hashLong[hLong] = curr;
                if (((((uint)((prefixStartIndex - 1) - repIndex) >= 3) && (offset_1 < curr + 1 - dictStartIndex))) && (MEM_read32((void*)repMatch) == MEM_read32((void*)(ip + 1))))
                {
                    byte* repMatchEnd = repIndex < prefixStartIndex ? dictEnd : iend;

                    mLength = ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixStart) + 4;
                    ip++;
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 0, mLength - 3);
                }
                else
                {
                    if ((matchLongIndex > dictStartIndex) && (MEM_read64((void*)matchLong) == MEM_read64((void*)ip)))
                    {
                        byte* matchEnd = matchLongIndex < prefixStartIndex ? dictEnd : iend;
                        byte* lowMatchPtr = matchLongIndex < prefixStartIndex ? dictStart : prefixStart;
                        uint offset;

                        mLength = ZSTD_count_2segments(ip + 8, matchLong + 8, iend, matchEnd, prefixStart) + 8;
                        offset = curr - matchLongIndex;
                        while ((((ip > anchor) && (matchLong > lowMatchPtr))) && (ip[-1] == matchLong[-1]))
                        {
                            ip--;
                            matchLong--;
                            mLength++;
                        }

                        offset_2 = offset_1;
                        offset_1 = offset;
                        ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + (uint)((3 - 1)), mLength - 3);
                    }
                    else if ((matchIndex > dictStartIndex) && (MEM_read32((void*)match) == MEM_read32((void*)ip)))
                    {
                        nuint h3 = ZSTD_hashPtr((void*)(ip + 1), hBitsL, 8);
                        uint matchIndex3 = hashLong[h3];
                        byte* match3Base = matchIndex3 < prefixStartIndex ? dictBase : @base;
                        byte* match3 = match3Base + matchIndex3;
                        uint offset;

                        hashLong[h3] = curr + 1;
                        if ((matchIndex3 > dictStartIndex) && (MEM_read64((void*)match3) == MEM_read64((void*)(ip + 1))))
                        {
                            byte* matchEnd = matchIndex3 < prefixStartIndex ? dictEnd : iend;
                            byte* lowMatchPtr = matchIndex3 < prefixStartIndex ? dictStart : prefixStart;

                            mLength = ZSTD_count_2segments(ip + 9, match3 + 8, iend, matchEnd, prefixStart) + 8;
                            ip++;
                            offset = curr + 1 - matchIndex3;
                            while ((((ip > anchor) && (match3 > lowMatchPtr))) && (ip[-1] == match3[-1]))
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

                            mLength = ZSTD_count_2segments(ip + 4, match + 4, iend, matchEnd, prefixStart) + 4;
                            offset = curr - matchIndex;
                            while ((((ip > anchor) && (match > lowMatchPtr))) && (ip[-1] == match[-1]))
                            {
                                ip--;
                                match--;
                                mLength++;
                            }
                        }

                        offset_2 = offset_1;
                        offset_1 = offset;
                        ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + (uint)((3 - 1)), mLength - 3);
                    }
                    else
                    {
                        ip += ((ip - anchor) >> 8) + 1;
                        continue;
                    }
                }

                ip += mLength;
                anchor = ip;
                if (ip <= ilimit)
                {

                    {
                        uint indexToInsert = curr + 2;

                        hashLong[ZSTD_hashPtr((void*)(@base + indexToInsert), hBitsL, 8)] = indexToInsert;
                        hashLong[ZSTD_hashPtr((void*)(ip - 2), hBitsL, 8)] = (uint)(ip - 2 - @base);
                        hashSmall[ZSTD_hashPtr((void*)(@base + indexToInsert), hBitsS, mls)] = indexToInsert;
                        hashSmall[ZSTD_hashPtr((void*)(ip - 1), hBitsS, mls)] = (uint)(ip - 1 - @base);
                    }

                    while (ip <= ilimit)
                    {
                        uint current2 = (uint)(ip - @base);
                        uint repIndex2 = current2 - offset_2;
                        byte* repMatch2 = repIndex2 < prefixStartIndex ? dictBase + repIndex2 : @base + repIndex2;

                        if (((((uint)((prefixStartIndex - 1) - repIndex2) >= 3) && (offset_2 < current2 - dictStartIndex))) && (MEM_read32((void*)repMatch2) == MEM_read32((void*)ip)))
                        {
                            byte* repEnd2 = repIndex2 < prefixStartIndex ? dictEnd : iend;
                            nuint repLength2 = ZSTD_count_2segments(ip + 4, repMatch2 + 4, iend, repEnd2, prefixStart) + 4;
                            uint tmpOffset = offset_2;

                            offset_2 = offset_1;
                            offset_1 = tmpOffset;
                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, repLength2 - 3);
                            hashSmall[ZSTD_hashPtr((void*)ip, hBitsS, mls)] = current2;
                            hashLong[ZSTD_hashPtr((void*)ip, hBitsL, 8)] = current2;
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

        public static nuint ZSTD_compressBlock_doubleFast_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint mls = ms->cParams.minMatch;

            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 4);
                }

                case 5:
                {
                    return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 5);
                }

                case 6:
                {
                    return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 6);
                }

                case 7:
                {
                    return ZSTD_compressBlock_doubleFast_extDict_generic(ms, seqStore, rep, src, srcSize, 7);
                }
            }
        }
    }
}
