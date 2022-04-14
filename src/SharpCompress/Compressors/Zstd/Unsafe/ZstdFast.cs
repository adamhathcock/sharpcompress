using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static void ZSTD_fillHashTable(ZSTD_matchState_t* ms, void* end, ZSTD_dictTableLoadMethod_e dtlm)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hBits = cParams->hashLog;
            uint mls = cParams->minMatch;
            byte* @base = ms->window.@base;
            byte* ip = @base + ms->nextToUpdate;
            byte* iend = ((byte*)(end)) - 8;
            uint fastHashFillStep = 3;

            for (; ip + fastHashFillStep < iend + 2; ip += fastHashFillStep)
            {
                uint curr = (uint)(ip - @base);
                nuint hash0 = ZSTD_hashPtr((void*)ip, hBits, mls);

                hashTable[hash0] = curr;
                if (dtlm == ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast)
                {
                    continue;
                }


                {
                    uint p;

                    for (p = 1; p < fastHashFillStep; ++p)
                    {
                        nuint hash = ZSTD_hashPtr((void*)(ip + p), hBits, mls);

                        if (hashTable[hash] == 0)
                        {
                            hashTable[hash] = curr + p;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_compressBlock_fast_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, uint mls)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hlog = cParams->hashLog;
            nuint stepSize = cParams->targetLength + (uint)((cParams->targetLength) == 0 ? 1 : 0) + 1;
            byte* @base = ms->window.@base;
            byte* istart = (byte*)(src);
            byte* ip0 = istart;
            byte* ip1;
            byte* anchor = istart;
            uint endIndex = (uint)((nuint)(istart - @base) + srcSize);
            uint prefixStartIndex = ZSTD_getLowestPrefixIndex(ms, endIndex, cParams->windowLog);
            byte* prefixStart = @base + prefixStartIndex;
            byte* iend = istart + srcSize;
            byte* ilimit = iend - 8;
            uint offset_1 = rep[0], offset_2 = rep[1];
            uint offsetSaved = 0;

            ip0 += ((ip0 == prefixStart) ? 1 : 0);
            ip1 = ip0 + 1;

            {
                uint curr = (uint)(ip0 - @base);
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

            while (ip1 < ilimit)
            {
                byte* match0;
                byte* match1;
                uint current0, matchIndex0, matchIndex1;
                {
                    nuint h0 = ZSTD_hashPtr((void*)ip0, hlog, mls);
                    nuint h1 = ZSTD_hashPtr((void*)ip1, hlog, mls);
                    matchIndex0 = hashTable[h0];
                    matchIndex1 = hashTable[h1];
                    match0 = @base + matchIndex0;
                    match1 = @base + matchIndex1;
                    current0 = (uint)(ip0 - @base);
                    hashTable[h0] = current0;
                    hashTable[h1] = (uint)(ip1 - @base);
                }

                uint offcode;
                nuint mLength;

                assert(ip0 + 1 == ip1);
                {
                    byte* ip2 = ip0 + 2;
                    byte* repMatch = ip2 - offset_1;
                    if (((offset_1 > 0) && (MEM_read32((void*) repMatch) == MEM_read32((void*) ip2))))
                    {
                        mLength = (nuint) ((ip2[-1] == repMatch[-1]) ? 1 : 0);
                        ip0 = ip2 - mLength;
                        match0 = repMatch - mLength;
                        mLength += 4;
                        offcode = 0;
                        goto _match;
                    }
                }

                if ((matchIndex0 > prefixStartIndex) && MEM_read32((void*)match0) == MEM_read32((void*)ip0))
                {
                    goto _offset;
                }

                if ((matchIndex1 > prefixStartIndex) && MEM_read32((void*)match1) == MEM_read32((void*)ip1))
                {
                    ip0 = ip1;
                    match0 = match1;
                    goto _offset;
                }


                {
                    nuint step = ((nuint)(ip0 - anchor) >> (8 - 1)) + stepSize;

                    assert(step >= 2);
                    ip0 += step;
                    ip1 += step;
                    continue;
                }

                _offset:
                offset_2 = offset_1;
                offset_1 = (uint)(ip0 - match0);
                offcode = offset_1 + (uint)((3 - 1));
                mLength = 4;
                while ((((ip0 > anchor) && (match0 > prefixStart))) && (ip0[-1] == match0[-1]))
                {
                    ip0--;
                    match0--;
                    mLength++;
                }

                _match:
                mLength += ZSTD_count(ip0 + mLength, match0 + mLength, iend);
                ZSTD_storeSeq(seqStore, (nuint)(ip0 - anchor), anchor, iend, offcode, mLength - 3);
                ip0 += mLength;
                anchor = ip0;
                if (ip0 <= ilimit)
                {
                    assert(@base + current0 + 2 > istart);
                    hashTable[ZSTD_hashPtr((void*)(@base + current0 + 2), hlog, mls)] = current0 + 2;
                    hashTable[ZSTD_hashPtr((void*)(ip0 - 2), hlog, mls)] = (uint)(ip0 - 2 - @base);
                    if (offset_2 > 0)
                    {
                        while ((ip0 <= ilimit) && (MEM_read32((void*)ip0) == MEM_read32((void*)(ip0 - offset_2))))
                        {
                            nuint rLength = ZSTD_count(ip0 + 4, ip0 + 4 - offset_2, iend) + 4;


                            {
                                uint tmpOff = offset_2;

                                offset_2 = offset_1;
                                offset_1 = tmpOff;
                            }

                            hashTable[ZSTD_hashPtr((void*)ip0, hlog, mls)] = (uint)(ip0 - @base);
                            ip0 += rLength;
                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, rLength - 3);
                            anchor = ip0;
                            continue;
                        }
                    }
                }

                ip1 = ip0 + 1;
            }

            rep[0] = offset_1 != 0 ? offset_1 : offsetSaved;
            rep[1] = offset_2 != 0 ? offset_2 : offsetSaved;
            return (nuint)(iend - anchor);
        }

        public static nuint ZSTD_compressBlock_fast(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint mls = ms->cParams.minMatch;

            assert(ms->dictMatchState == null);
            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_compressBlock_fast_generic(ms, seqStore, rep, src, srcSize, 4);
                }

                case 5:
                {
                    return ZSTD_compressBlock_fast_generic(ms, seqStore, rep, src, srcSize, 5);
                }

                case 6:
                {
                    return ZSTD_compressBlock_fast_generic(ms, seqStore, rep, src, srcSize, 6);
                }

                case 7:
                {
                    return ZSTD_compressBlock_fast_generic(ms, seqStore, rep, src, srcSize, 7);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_compressBlock_fast_dictMatchState_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, uint mls)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hlog = cParams->hashLog;
            uint stepSize = cParams->targetLength + (uint)((cParams->targetLength) == 0 ? 1 : 0);
            byte* @base = ms->window.@base;
            byte* istart = (byte*)(src);
            byte* ip = istart;
            byte* anchor = istart;
            uint prefixStartIndex = ms->window.dictLimit;
            byte* prefixStart = @base + prefixStartIndex;
            byte* iend = istart + srcSize;
            byte* ilimit = iend - 8;
            uint offset_1 = rep[0], offset_2 = rep[1];
            uint offsetSaved = 0;
            ZSTD_matchState_t* dms = ms->dictMatchState;
            ZSTD_compressionParameters* dictCParams = &dms->cParams;
            uint* dictHashTable = dms->hashTable;
            uint dictStartIndex = dms->window.dictLimit;
            byte* dictBase = dms->window.@base;
            byte* dictStart = dictBase + dictStartIndex;
            byte* dictEnd = dms->window.nextSrc;
            uint dictIndexDelta = prefixStartIndex - (uint)(dictEnd - dictBase);
            uint dictAndPrefixLength = (uint)(ip - prefixStart + dictEnd - dictStart);
            uint dictHLog = dictCParams->hashLog;
            uint maxDistance = 1U << (int)cParams->windowLog;
            uint endIndex = (uint)((nuint)(ip - @base) + srcSize);

            assert(endIndex - prefixStartIndex <= maxDistance);
            assert(prefixStartIndex >= (uint)(dictEnd - dictBase));
            ip += ((dictAndPrefixLength == 0) ? 1 : 0);
            assert(offset_1 <= dictAndPrefixLength);
            assert(offset_2 <= dictAndPrefixLength);
            while (ip < ilimit)
            {
                nuint mLength;
                nuint h = ZSTD_hashPtr((void*)ip, hlog, mls);
                uint curr = (uint)(ip - @base);
                uint matchIndex = hashTable[h];
                byte* match = @base + matchIndex;
                uint repIndex = curr + 1 - offset_1;
                byte* repMatch = (repIndex < prefixStartIndex) ? dictBase + (repIndex - dictIndexDelta) : @base + repIndex;

                hashTable[h] = curr;
                if (((uint)((prefixStartIndex - 1) - repIndex) >= 3) && (MEM_read32((void*)repMatch) == MEM_read32((void*)(ip + 1))))
                {
                    byte* repMatchEnd = repIndex < prefixStartIndex ? dictEnd : iend;

                    mLength = ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixStart) + 4;
                    ip++;
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 0, mLength - 3);
                }
                else if ((matchIndex <= prefixStartIndex))
                {
                    nuint dictHash = ZSTD_hashPtr((void*)ip, dictHLog, mls);
                    uint dictMatchIndex = dictHashTable[dictHash];
                    byte* dictMatch = dictBase + dictMatchIndex;

                    if (dictMatchIndex <= dictStartIndex || MEM_read32((void*)dictMatch) != MEM_read32((void*)ip))
                    {
                        assert(stepSize >= 1);
                        ip += (ulong)((ip - anchor) >> 8) + stepSize;
                        continue;
                    }
                    else
                    {
                        uint offset = (uint)(curr - dictMatchIndex - dictIndexDelta);

                        mLength = ZSTD_count_2segments(ip + 4, dictMatch + 4, iend, dictEnd, prefixStart) + 4;
                        while ((((ip > anchor) && (dictMatch > dictStart))) && (ip[-1] == dictMatch[-1]))
                        {
                            ip--;
                            dictMatch--;
                            mLength++;
                        }

                        offset_2 = offset_1;
                        offset_1 = offset;
                        ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + (uint)((3 - 1)), mLength - 3);
                    }
                }
                else if (MEM_read32((void*)match) != MEM_read32((void*)ip))
                {
                    assert(stepSize >= 1);
                    ip += (ulong)((ip - anchor) >> 8) + stepSize;
                    continue;
                }
                else
                {
                    uint offset = (uint)(ip - match);

                    mLength = ZSTD_count(ip + 4, match + 4, iend) + 4;
                    while ((((ip > anchor) && (match > prefixStart))) && (ip[-1] == match[-1]))
                    {
                        ip--;
                        match--;
                        mLength++;
                    }

                    offset_2 = offset_1;
                    offset_1 = offset;
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + (uint)((3 - 1)), mLength - 3);
                }

                ip += mLength;
                anchor = ip;
                if (ip <= ilimit)
                {
                    assert(@base + curr + 2 > istart);
                    hashTable[ZSTD_hashPtr((void*)(@base + curr + 2), hlog, mls)] = curr + 2;
                    hashTable[ZSTD_hashPtr((void*)(ip - 2), hlog, mls)] = (uint)(ip - 2 - @base);
                    while (ip <= ilimit)
                    {
                        uint current2 = (uint)(ip - @base);
                        uint repIndex2 = current2 - offset_2;
                        byte* repMatch2 = repIndex2 < prefixStartIndex ? dictBase - dictIndexDelta + repIndex2 : @base + repIndex2;

                        if (((uint)((prefixStartIndex - 1) - (uint)(repIndex2)) >= 3) && (MEM_read32((void*)repMatch2) == MEM_read32((void*)ip)))
                        {
                            byte* repEnd2 = repIndex2 < prefixStartIndex ? dictEnd : iend;
                            nuint repLength2 = ZSTD_count_2segments(ip + 4, repMatch2 + 4, iend, repEnd2, prefixStart) + 4;
                            uint tmpOffset = offset_2;

                            offset_2 = offset_1;
                            offset_1 = tmpOffset;
                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, repLength2 - 3);
                            hashTable[ZSTD_hashPtr((void*)ip, hlog, mls)] = current2;
                            ip += repLength2;
                            anchor = ip;
                            continue;
                        }

                        break;
                    }
                }
            }

            rep[0] = offset_1 != 0 ? offset_1 : offsetSaved;
            rep[1] = offset_2 != 0 ? offset_2 : offsetSaved;
            return (nuint)(iend - anchor);
        }

        public static nuint ZSTD_compressBlock_fast_dictMatchState(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint mls = ms->cParams.minMatch;

            assert(ms->dictMatchState != null);
            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_compressBlock_fast_dictMatchState_generic(ms, seqStore, rep, src, srcSize, 4);
                }

                case 5:
                {
                    return ZSTD_compressBlock_fast_dictMatchState_generic(ms, seqStore, rep, src, srcSize, 5);
                }

                case 6:
                {
                    return ZSTD_compressBlock_fast_dictMatchState_generic(ms, seqStore, rep, src, srcSize, 6);
                }

                case 7:
                {
                    return ZSTD_compressBlock_fast_dictMatchState_generic(ms, seqStore, rep, src, srcSize, 7);
                }
            }
        }

        private static nuint ZSTD_compressBlock_fast_extDict_generic(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize, uint mls)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint* hashTable = ms->hashTable;
            uint hlog = cParams->hashLog;
            uint stepSize = cParams->targetLength + (uint)((cParams->targetLength) == 0 ? 1 : 0);
            byte* @base = ms->window.@base;
            byte* dictBase = ms->window.dictBase;
            byte* istart = (byte*)(src);
            byte* ip = istart;
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
            uint offset_1 = rep[0], offset_2 = rep[1];

            if (prefixStartIndex == dictStartIndex)
            {
                return ZSTD_compressBlock_fast_generic(ms, seqStore, rep, src, srcSize, mls);
            }

            while (ip < ilimit)
            {
                nuint h = ZSTD_hashPtr((void*)ip, hlog, mls);
                uint matchIndex = hashTable[h];
                byte* matchBase = matchIndex < prefixStartIndex ? dictBase : @base;
                byte* match = matchBase + matchIndex;
                uint curr = (uint)(ip - @base);
                uint repIndex = curr + 1 - offset_1;
                byte* repBase = repIndex < prefixStartIndex ? dictBase : @base;
                byte* repMatch = repBase + repIndex;

                hashTable[h] = curr;
                if (((((uint)((prefixStartIndex - 1) - repIndex) >= 3) && (offset_1 < curr + 1 - dictStartIndex))) && (MEM_read32((void*)repMatch) == MEM_read32((void*)(ip + 1))))
                {
                    byte* repMatchEnd = repIndex < prefixStartIndex ? dictEnd : iend;
                    nuint rLength = ZSTD_count_2segments(ip + 1 + 4, repMatch + 4, iend, repMatchEnd, prefixStart) + 4;

                    ip++;
                    ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, 0, rLength - 3);
                    ip += rLength;
                    anchor = ip;
                }
                else
                {
                    if ((matchIndex < dictStartIndex) || (MEM_read32((void*)match) != MEM_read32((void*)ip)))
                    {
                        assert(stepSize >= 1);
                        ip += (ulong)((ip - anchor) >> 8) + stepSize;
                        continue;
                    }


                    {
                        byte* matchEnd = matchIndex < prefixStartIndex ? dictEnd : iend;
                        byte* lowMatchPtr = matchIndex < prefixStartIndex ? dictStart : prefixStart;
                        uint offset = curr - matchIndex;
                        nuint mLength = ZSTD_count_2segments(ip + 4, match + 4, iend, matchEnd, prefixStart) + 4;

                        while ((((ip > anchor) && (match > lowMatchPtr))) && (ip[-1] == match[-1]))
                        {
                            ip--;
                            match--;
                            mLength++;
                        }

                        offset_2 = offset_1;
                        offset_1 = offset;
                        ZSTD_storeSeq(seqStore, (nuint)(ip - anchor), anchor, iend, offset + (uint)((3 - 1)), mLength - 3);
                        ip += mLength;
                        anchor = ip;
                    }
                }

                if (ip <= ilimit)
                {
                    hashTable[ZSTD_hashPtr((void*)(@base + curr + 2), hlog, mls)] = curr + 2;
                    hashTable[ZSTD_hashPtr((void*)(ip - 2), hlog, mls)] = (uint)(ip - 2 - @base);
                    while (ip <= ilimit)
                    {
                        uint current2 = (uint)(ip - @base);
                        uint repIndex2 = current2 - offset_2;
                        byte* repMatch2 = repIndex2 < prefixStartIndex ? dictBase + repIndex2 : @base + repIndex2;

                        if (((((uint)((prefixStartIndex - 1) - repIndex2) >= 3) && (offset_2 < curr - dictStartIndex))) && (MEM_read32((void*)repMatch2) == MEM_read32((void*)ip)))
                        {
                            byte* repEnd2 = repIndex2 < prefixStartIndex ? dictEnd : iend;
                            nuint repLength2 = ZSTD_count_2segments(ip + 4, repMatch2 + 4, iend, repEnd2, prefixStart) + 4;


                            {
                                uint tmpOffset = offset_2;

                                offset_2 = offset_1;
                                offset_1 = tmpOffset;
                            }

                            ZSTD_storeSeq(seqStore, 0, anchor, iend, 0, repLength2 - 3);
                            hashTable[ZSTD_hashPtr((void*)ip, hlog, mls)] = current2;
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

        public static nuint ZSTD_compressBlock_fast_extDict(ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize)
        {
            uint mls = ms->cParams.minMatch;

            switch (mls)
            {
                default:
                case 4:
                {
                    return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 4);
                }

                case 5:
                {
                    return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 5);
                }

                case 6:
                {
                    return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 6);
                }

                case 7:
                {
                    return ZSTD_compressBlock_fast_extDict_generic(ms, seqStore, rep, src, srcSize, 7);
                }
            }
        }
    }
}
