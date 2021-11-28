using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /** ZSTD_ldm_gear_init():
         *
         * Initializes the rolling hash state such that it will honor the
         * settings in params. */
        private static void ZSTD_ldm_gear_init(ldmRollingHashState_t* state, ldmParams_t* @params)
        {
            uint maxBitsInMask = ((@params->minMatchLength) < (64) ? (@params->minMatchLength) : (64));
            uint hashRateLog = @params->hashRateLog;

            state->rolling = unchecked(~(uint)(0));
            if (hashRateLog > 0 && hashRateLog <= maxBitsInMask)
            {
                state->stopMask = (((ulong)(1) << (int)hashRateLog) - 1) << (int)(maxBitsInMask - hashRateLog);
            }
            else
            {
                state->stopMask = ((ulong)(1) << (int)hashRateLog) - 1;
            }
        }

        /** ZSTD_ldm_gear_reset()
         * Feeds [data, data + minMatchLength) into the hash without registering any
         * splits. This effectively resets the hash state. This is used when skipping
         * over data, either at the beginning of a block, or skipping sections.
         */
        private static void ZSTD_ldm_gear_reset(ldmRollingHashState_t* state, byte* data, nuint minMatchLength)
        {
            ulong hash = state->rolling;
            nuint n = 0;

            while (n + 3 < minMatchLength)
            {

                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                }


                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                }


                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                }


                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                }
            }

            while (n < minMatchLength)
            {

                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                }
            }
        }

        /** ZSTD_ldm_gear_feed():
         *
         * Registers in the splits array all the split points found in the first
         * size bytes following the data pointer. This function terminates when
         * either all the data has been processed or LDM_BATCH_SIZE splits are
         * present in the splits array.
         *
         * Precondition: The splits array must not be full.
         * Returns: The number of bytes processed. */
        private static nuint ZSTD_ldm_gear_feed(ldmRollingHashState_t* state, byte* data, nuint size, nuint* splits, uint* numSplits)
        {
            nuint n;
            ulong hash, mask;

            hash = state->rolling;
            mask = state->stopMask;
            n = 0;
            while (n + 3 < size)
            {

                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                    if (((hash & mask) == 0))
                    {
                        splits[*numSplits] = n;
                        *numSplits += 1;
                        if (*numSplits == 64)
                        {
                            goto done;
                        }
                    }
                }


                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                    if (((hash & mask) == 0))
                    {
                        splits[*numSplits] = n;
                        *numSplits += 1;
                        if (*numSplits == 64)
                        {
                            goto done;
                        }
                    }
                }


                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                    if (((hash & mask) == 0))
                    {
                        splits[*numSplits] = n;
                        *numSplits += 1;
                        if (*numSplits == 64)
                        {
                            goto done;
                        }
                    }
                }


                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                    if (((hash & mask) == 0))
                    {
                        splits[*numSplits] = n;
                        *numSplits += 1;
                        if (*numSplits == 64)
                        {
                            goto done;
                        }
                    }
                }
            }

            while (n < size)
            {

                {
                    hash = (hash << 1) + ZSTD_ldm_gearTab[data[n] & 0xff];
                    n += 1;
                    if (((hash & mask) == 0))
                    {
                        splits[*numSplits] = n;
                        *numSplits += 1;
                        if (*numSplits == 64)
                        {
                            goto done;
                        }
                    }
                }
            }

            done:
            state->rolling = hash;
            return n;
        }

        /** ZSTD_ldm_adjustParameters() :
         *  If the params->hashRateLog is not set, set it to its default value based on
         *  windowLog and params->hashLog.
         *
         *  Ensures that params->bucketSizeLog is <= params->hashLog (setting it to
         *  params->hashLog if it is not).
         *
         *  Ensures that the minMatchLength >= targetLength during optimal parsing.
         */
        public static void ZSTD_ldm_adjustParameters(ldmParams_t* @params, ZSTD_compressionParameters* cParams)
        {
            @params->windowLog = cParams->windowLog;
            if (@params->bucketSizeLog == 0)
            {
                @params->bucketSizeLog = 3;
            }

            if (@params->minMatchLength == 0)
            {
                @params->minMatchLength = 64;
            }

            if (@params->hashLog == 0)
            {
                @params->hashLog = (uint)((6) > (@params->windowLog - 7) ? (6) : (@params->windowLog - 7));
                assert(@params->hashLog <= (uint)(((((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) < 30) ? ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) : 30)));
            }

            if (@params->hashRateLog == 0)
            {
                @params->hashRateLog = (uint)(@params->windowLog < @params->hashLog ? 0 : @params->windowLog - @params->hashLog);
            }

            @params->bucketSizeLog = ((@params->bucketSizeLog) < (@params->hashLog) ? (@params->bucketSizeLog) : (@params->hashLog));
        }

        /** ZSTD_ldm_getTableSize() :
         *  Estimate the space needed for long distance matching tables or 0 if LDM is
         *  disabled.
         */
        public static nuint ZSTD_ldm_getTableSize(ldmParams_t @params)
        {
            nuint ldmHSize = ((nuint)(1)) << (int)@params.hashLog;
            nuint ldmBucketSizeLog = ((@params.bucketSizeLog) < (@params.hashLog) ? (@params.bucketSizeLog) : (@params.hashLog));
            nuint ldmBucketSize = ((nuint)(1)) << (int)(@params.hashLog - ldmBucketSizeLog);
            nuint totalSize = ZSTD_cwksp_alloc_size(ldmBucketSize) + ZSTD_cwksp_alloc_size(ldmHSize * (nuint)(8));

            return @params.enableLdm != 0 ? totalSize : 0;
        }

        /** ZSTD_ldm_getSeqSpace() :
         *  Return an upper bound on the number of sequences that can be produced by
         *  the long distance matcher, or 0 if LDM is disabled.
         */
        public static nuint ZSTD_ldm_getMaxNbSeq(ldmParams_t @params, nuint maxChunkSize)
        {
            return @params.enableLdm != 0 ? (maxChunkSize / @params.minMatchLength) : 0;
        }

        /** ZSTD_ldm_getBucket() :
         *  Returns a pointer to the start of the bucket associated with hash. */
        private static ldmEntry_t* ZSTD_ldm_getBucket(ldmState_t* ldmState, nuint hash, ldmParams_t ldmParams)
        {
            return ldmState->hashTable + (hash << (int)ldmParams.bucketSizeLog);
        }

        /** ZSTD_ldm_insertEntry() :
         *  Insert the entry with corresponding hash into the hash table */
        private static void ZSTD_ldm_insertEntry(ldmState_t* ldmState, nuint hash, ldmEntry_t entry, ldmParams_t ldmParams)
        {
            byte* pOffset = ldmState->bucketOffsets + hash;
            uint offset = *pOffset;

            *(ZSTD_ldm_getBucket(ldmState, hash, ldmParams) + offset) = entry;
            *pOffset = (byte)((offset + 1) & ((1U << (int)ldmParams.bucketSizeLog) - 1));
        }

        /** ZSTD_ldm_countBackwardsMatch() :
         *  Returns the number of bytes that match backwards before pIn and pMatch.
         *
         *  We count only bytes where pMatch >= pBase and pIn >= pAnchor. */
        private static nuint ZSTD_ldm_countBackwardsMatch(byte* pIn, byte* pAnchor, byte* pMatch, byte* pMatchBase)
        {
            nuint matchLength = 0;

            while (pIn > pAnchor && pMatch > pMatchBase && pIn[-1] == pMatch[-1])
            {
                pIn--;
                pMatch--;
                matchLength++;
            }

            return matchLength;
        }

        /** ZSTD_ldm_countBackwardsMatch_2segments() :
         *  Returns the number of bytes that match backwards from pMatch,
         *  even with the backwards match spanning 2 different segments.
         *
         *  On reaching `pMatchBase`, start counting from mEnd */
        private static nuint ZSTD_ldm_countBackwardsMatch_2segments(byte* pIn, byte* pAnchor, byte* pMatch, byte* pMatchBase, byte* pExtDictStart, byte* pExtDictEnd)
        {
            nuint matchLength = ZSTD_ldm_countBackwardsMatch(pIn, pAnchor, pMatch, pMatchBase);

            if (pMatch - matchLength != pMatchBase || pMatchBase == pExtDictStart)
            {
                return matchLength;
            }

            matchLength += ZSTD_ldm_countBackwardsMatch(pIn - matchLength, pAnchor, pExtDictEnd, pExtDictStart);
            return matchLength;
        }

        /** ZSTD_ldm_fillFastTables() :
         *
         *  Fills the relevant tables for the ZSTD_fast and ZSTD_dfast strategies.
         *  This is similar to ZSTD_loadDictionaryContent.
         *
         *  The tables for the other strategies are filled within their
         *  block compressors. */
        private static nuint ZSTD_ldm_fillFastTables(ZSTD_matchState_t* ms, void* end)
        {
            byte* iend = (byte*)(end);

            switch (ms->cParams.strategy)
            {
                case ZSTD_strategy.ZSTD_fast:
                {
                    ZSTD_fillHashTable(ms, (void*)iend, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast);
                }

                break;
                case ZSTD_strategy.ZSTD_dfast:
                {
                    ZSTD_fillDoubleHashTable(ms, (void*)iend, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast);
                }

                break;
                case ZSTD_strategy.ZSTD_greedy:
                case ZSTD_strategy.ZSTD_lazy:
                case ZSTD_strategy.ZSTD_lazy2:
                case ZSTD_strategy.ZSTD_btlazy2:
                case ZSTD_strategy.ZSTD_btopt:
                case ZSTD_strategy.ZSTD_btultra:
                case ZSTD_strategy.ZSTD_btultra2:
                {
                    break;
                }

                default:
                {
                    assert(0 != 0);
                }
                break;
            }

            return 0;
        }

        public static void ZSTD_ldm_fillHashTable(ldmState_t* ldmState, byte* ip, byte* iend, ldmParams_t* @params)
        {
            uint minMatchLength = @params->minMatchLength;
            uint hBits = @params->hashLog - @params->bucketSizeLog;
            byte* @base = ldmState->window.@base;
            byte* istart = ip;
            ldmRollingHashState_t hashState;
            nuint* splits = (nuint*)ldmState->splitIndices;
            uint numSplits;

            ZSTD_ldm_gear_init(&hashState, @params);
            while (ip < iend)
            {
                nuint hashed;
                uint n;

                numSplits = 0;
                hashed = ZSTD_ldm_gear_feed(&hashState, ip, (nuint)(iend - ip), splits, &numSplits);
                for (n = 0; n < numSplits; n++)
                {
                    if (ip + splits[n] >= istart + minMatchLength)
                    {
                        byte* split = ip + splits[n] - minMatchLength;
                        ulong xxhash = XXH64((void*)split, minMatchLength, 0);
                        uint hash = (uint)(xxhash & (((uint)(1) << (int)hBits) - 1));
                        ldmEntry_t entry;

                        entry.offset = (uint)(split - @base);
                        entry.checksum = (uint)(xxhash >> 32);
                        ZSTD_ldm_insertEntry(ldmState, hash, entry, *@params);
                    }
                }

                ip += hashed;
            }
        }

        /** ZSTD_ldm_limitTableUpdate() :
         *
         *  Sets cctx->nextToUpdate to a position corresponding closer to anchor
         *  if it is far way
         *  (after a long match, only update tables a limited amount). */
        private static void ZSTD_ldm_limitTableUpdate(ZSTD_matchState_t* ms, byte* anchor)
        {
            uint curr = (uint)(anchor - ms->window.@base);

            if (curr > ms->nextToUpdate + 1024)
            {
                ms->nextToUpdate = curr - (uint)((512) < (curr - ms->nextToUpdate - 1024) ? (512) : (curr - ms->nextToUpdate - 1024));
            }
        }

        private static nuint ZSTD_ldm_generateSequences_internal(ldmState_t* ldmState, rawSeqStore_t* rawSeqStore, ldmParams_t* @params, void* src, nuint srcSize)
        {
            int extDict = (int)(ZSTD_window_hasExtDict(ldmState->window));
            uint minMatchLength = @params->minMatchLength;
            uint entsPerBucket = 1U << (int)@params->bucketSizeLog;
            uint hBits = @params->hashLog - @params->bucketSizeLog;
            uint dictLimit = ldmState->window.dictLimit;
            uint lowestIndex = extDict != 0 ? ldmState->window.lowLimit : dictLimit;
            byte* @base = ldmState->window.@base;
            byte* dictBase = extDict != 0 ? ldmState->window.dictBase : null;
            byte* dictStart = extDict != 0 ? dictBase + lowestIndex : null;
            byte* dictEnd = extDict != 0 ? dictBase + dictLimit : null;
            byte* lowPrefixPtr = @base + dictLimit;
            byte* istart = (byte*)(src);
            byte* iend = istart + srcSize;
            byte* ilimit = iend - 8;
            byte* anchor = istart;
            byte* ip = istart;
            ldmRollingHashState_t hashState;
            nuint* splits = (nuint*)ldmState->splitIndices;
            ldmMatchCandidate_t* candidates = (ldmMatchCandidate_t*)ldmState->matchCandidates;
            uint numSplits;

            if (srcSize < minMatchLength)
            {
                return (nuint)(iend - anchor);
            }

            ZSTD_ldm_gear_init(&hashState, @params);
            ZSTD_ldm_gear_reset(&hashState, ip, minMatchLength);
            ip += minMatchLength;
            while (ip < ilimit)
            {
                nuint hashed;
                uint n;

                numSplits = 0;
                hashed = ZSTD_ldm_gear_feed(&hashState, ip, (nuint)(ilimit - ip), splits, &numSplits);
                for (n = 0; n < numSplits; n++)
                {
                    byte* split = ip + splits[n] - minMatchLength;
                    ulong xxhash = XXH64((void*)split, minMatchLength, 0);
                    uint hash = (uint)(xxhash & (((uint)(1) << (int)hBits) - 1));

                    candidates[n].split = split;
                    candidates[n].hash = hash;
                    candidates[n].checksum = (uint)(xxhash >> 32);
                    candidates[n].bucket = ZSTD_ldm_getBucket(ldmState, hash, *@params);
                    Prefetch0((void*)(candidates[n].bucket));
                }

                for (n = 0; n < numSplits; n++)
                {
                    nuint forwardMatchLength = 0, backwardMatchLength = 0, bestMatchLength = 0, mLength;
                    uint offset;
                    byte* split = candidates[n].split;
                    uint checksum = candidates[n].checksum;
                    uint hash = candidates[n].hash;
                    ldmEntry_t* bucket = candidates[n].bucket;
                    ldmEntry_t* cur;
                    ldmEntry_t* bestEntry = (ldmEntry_t*)null;
                    ldmEntry_t newEntry;

                    newEntry.offset = (uint)(split - @base);
                    newEntry.checksum = checksum;
                    if (split < anchor)
                    {
                        ZSTD_ldm_insertEntry(ldmState, hash, newEntry, *@params);
                        continue;
                    }

                    for (cur = bucket; cur < bucket + entsPerBucket; cur++)
                    {
                        nuint curForwardMatchLength, curBackwardMatchLength, curTotalMatchLength;

                        if (cur->checksum != checksum || cur->offset <= lowestIndex)
                        {
                            continue;
                        }

                        if (extDict != 0)
                        {
                            byte* curMatchBase = cur->offset < dictLimit ? dictBase : @base;
                            byte* pMatch = curMatchBase + cur->offset;
                            byte* matchEnd = cur->offset < dictLimit ? dictEnd : iend;
                            byte* lowMatchPtr = cur->offset < dictLimit ? dictStart : lowPrefixPtr;

                            curForwardMatchLength = ZSTD_count_2segments(split, pMatch, iend, matchEnd, lowPrefixPtr);
                            if (curForwardMatchLength < minMatchLength)
                            {
                                continue;
                            }

                            curBackwardMatchLength = ZSTD_ldm_countBackwardsMatch_2segments(split, anchor, pMatch, lowMatchPtr, dictStart, dictEnd);
                        }
                        else
                        {
                            byte* pMatch = @base + cur->offset;

                            curForwardMatchLength = ZSTD_count(split, pMatch, iend);
                            if (curForwardMatchLength < minMatchLength)
                            {
                                continue;
                            }

                            curBackwardMatchLength = ZSTD_ldm_countBackwardsMatch(split, anchor, pMatch, lowPrefixPtr);
                        }

                        curTotalMatchLength = curForwardMatchLength + curBackwardMatchLength;
                        if (curTotalMatchLength > bestMatchLength)
                        {
                            bestMatchLength = curTotalMatchLength;
                            forwardMatchLength = curForwardMatchLength;
                            backwardMatchLength = curBackwardMatchLength;
                            bestEntry = cur;
                        }
                    }

                    if (bestEntry == null)
                    {
                        ZSTD_ldm_insertEntry(ldmState, hash, newEntry, *@params);
                        continue;
                    }

                    offset = (uint)(split - @base) - bestEntry->offset;
                    mLength = forwardMatchLength + backwardMatchLength;

                    {
                        rawSeq* seq = rawSeqStore->seq + rawSeqStore->size;

                        if (rawSeqStore->size == rawSeqStore->capacity)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                        }

                        seq->litLength = (uint)(split - backwardMatchLength - anchor);
                        seq->matchLength = (uint)(mLength);
                        seq->offset = offset;
                        rawSeqStore->size++;
                    }

                    ZSTD_ldm_insertEntry(ldmState, hash, newEntry, *@params);
                    anchor = split + forwardMatchLength;
                    if (anchor > ip + hashed)
                    {
                        ZSTD_ldm_gear_reset(&hashState, anchor - minMatchLength, minMatchLength);
                        ip = anchor - hashed;
                        break;
                    }
                }

                ip += hashed;
            }

            return (nuint)(iend - anchor);
        }

        /*! ZSTD_ldm_reduceTable() :
         *  reduce table indexes by `reducerValue` */
        private static void ZSTD_ldm_reduceTable(ldmEntry_t* table, uint size, uint reducerValue)
        {
            uint u;

            for (u = 0; u < size; u++)
            {
                if (table[u].offset < reducerValue)
                {
                    table[u].offset = 0;
                }
                else
                {
                    table[u].offset -= reducerValue;
                }
            }
        }

        /**
         * ZSTD_ldm_generateSequences():
         *
         * Generates the sequences using the long distance match finder.
         * Generates long range matching sequences in `sequences`, which parse a prefix
         * of the source. `sequences` must be large enough to store every sequence,
         * which can be checked with `ZSTD_ldm_getMaxNbSeq()`.
         * @returns 0 or an error code.
         *
         * NOTE: The user must have called ZSTD_window_update() for all of the input
         * they have, even if they pass it to ZSTD_ldm_generateSequences() in chunks.
         * NOTE: This function returns an error if it runs out of space to store
         *       sequences.
         */
        public static nuint ZSTD_ldm_generateSequences(ldmState_t* ldmState, rawSeqStore_t* sequences, ldmParams_t* @params, void* src, nuint srcSize)
        {
            uint maxDist = 1U << (int)@params->windowLog;
            byte* istart = (byte*)(src);
            byte* iend = istart + srcSize;
            nuint kMaxChunkSize = (nuint)(1 << 20);
            nuint nbChunks = (srcSize / kMaxChunkSize) + (uint)(((((srcSize % kMaxChunkSize) != 0)) ? 1 : 0));
            nuint chunk;
            nuint leftoverSize = 0;

            assert(((unchecked((uint)(-1))) - ((3U << 29) + (1U << ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))))) >= kMaxChunkSize);
            assert(ldmState->window.nextSrc >= (byte*)(src) + srcSize);
            assert(sequences->pos <= sequences->size);
            assert(sequences->size <= sequences->capacity);
            for (chunk = 0; chunk < nbChunks && sequences->size < sequences->capacity; ++chunk)
            {
                byte* chunkStart = istart + chunk * kMaxChunkSize;
                nuint remaining = (nuint)(iend - chunkStart);
                byte* chunkEnd = (remaining < kMaxChunkSize) ? iend : chunkStart + kMaxChunkSize;
                nuint chunkSize = (nuint)(chunkEnd - chunkStart);
                nuint newLeftoverSize;
                nuint prevSize = sequences->size;

                assert(chunkStart < iend);
                if ((ZSTD_window_needOverflowCorrection(ldmState->window, 0, maxDist, ldmState->loadedDictEnd, (void*)chunkStart, (void*)chunkEnd)) != 0)
                {
                    uint ldmHSize = 1U << (int)@params->hashLog;
                    uint correction = ZSTD_window_correctOverflow(&ldmState->window, 0, maxDist, (void*)chunkStart);

                    ZSTD_ldm_reduceTable(ldmState->hashTable, ldmHSize, correction);
                    ldmState->loadedDictEnd = 0;
                }

                ZSTD_window_enforceMaxDist(&ldmState->window, (void*)chunkEnd, maxDist, &ldmState->loadedDictEnd, (ZSTD_matchState_t**)null);
                newLeftoverSize = ZSTD_ldm_generateSequences_internal(ldmState, sequences, @params, (void*)chunkStart, chunkSize);
                if ((ERR_isError(newLeftoverSize)) != 0)
                {
                    return newLeftoverSize;
                }

                if (prevSize < sequences->size)
                {
                    sequences->seq[prevSize].litLength += (uint)(leftoverSize);
                    leftoverSize = newLeftoverSize;
                }
                else
                {
                    assert(newLeftoverSize == chunkSize);
                    leftoverSize += chunkSize;
                }
            }

            return 0;
        }

        /**
         * ZSTD_ldm_skipSequences():
         *
         * Skip past `srcSize` bytes worth of sequences in `rawSeqStore`.
         * Avoids emitting matches less than `minMatch` bytes.
         * Must be called for data that is not passed to ZSTD_ldm_blockCompress().
         */
        public static void ZSTD_ldm_skipSequences(rawSeqStore_t* rawSeqStore, nuint srcSize, uint minMatch)
        {
            while (srcSize > 0 && rawSeqStore->pos < rawSeqStore->size)
            {
                rawSeq* seq = rawSeqStore->seq + rawSeqStore->pos;

                if (srcSize <= seq->litLength)
                {
                    seq->litLength -= (uint)(srcSize);
                    return;
                }

                srcSize -= seq->litLength;
                seq->litLength = 0;
                if (srcSize < seq->matchLength)
                {
                    seq->matchLength -= (uint)(srcSize);
                    if (seq->matchLength < minMatch)
                    {
                        if (rawSeqStore->pos + 1 < rawSeqStore->size)
                        {
                            seq[1].litLength += seq[0].matchLength;
                        }

                        rawSeqStore->pos++;
                    }

                    return;
                }

                srcSize -= seq->matchLength;
                seq->matchLength = 0;
                rawSeqStore->pos++;
            }
        }

        /**
         * If the sequence length is longer than remaining then the sequence is split
         * between this block and the next.
         *
         * Returns the current sequence to handle, or if the rest of the block should
         * be literals, it returns a sequence with offset == 0.
         */
        private static rawSeq maybeSplitSequence(rawSeqStore_t* rawSeqStore, uint remaining, uint minMatch)
        {
            rawSeq sequence = rawSeqStore->seq[rawSeqStore->pos];

            assert(sequence.offset > 0);
            if (remaining >= sequence.litLength + sequence.matchLength)
            {
                rawSeqStore->pos++;
                return sequence;
            }

            if (remaining <= sequence.litLength)
            {
                sequence.offset = 0;
            }
            else if (remaining < sequence.litLength + sequence.matchLength)
            {
                sequence.matchLength = remaining - sequence.litLength;
                if (sequence.matchLength < minMatch)
                {
                    sequence.offset = 0;
                }
            }

            ZSTD_ldm_skipSequences(rawSeqStore, remaining, minMatch);
            return sequence;
        }

        /* ZSTD_ldm_skipRawSeqStoreBytes():
         * Moves forward in rawSeqStore by nbBytes, updating fields 'pos' and 'posInSequence'.
         * Not to be used in conjunction with ZSTD_ldm_skipSequences().
         * Must be called for data with is not passed to ZSTD_ldm_blockCompress().
         */
        public static void ZSTD_ldm_skipRawSeqStoreBytes(rawSeqStore_t* rawSeqStore, nuint nbBytes)
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

        /**
         * ZSTD_ldm_blockCompress():
         *
         * Compresses a block using the predefined sequences, along with a secondary
         * block compressor. The literals section of every sequence is passed to the
         * secondary block compressor, and those sequences are interspersed with the
         * predefined sequences. Returns the length of the last literals.
         * Updates `rawSeqStore.pos` to indicate how many sequences have been consumed.
         * `rawSeqStore.seq` may also be updated to split the last sequence between two
         * blocks.
         * @return The length of the last literals.
         *
         * NOTE: The source must be at most the maximum block size, but the predefined
         * sequences can be any size, and may be longer than the block. In the case that
         * they are longer than the block, the last sequences may need to be split into
         * two. We handle that case correctly, and update `rawSeqStore` appropriately.
         * NOTE: This function does not return any errors.
         */
        public static nuint ZSTD_ldm_blockCompress(rawSeqStore_t* rawSeqStore, ZSTD_matchState_t* ms, seqStore_t* seqStore, uint* rep, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, void* src, nuint srcSize)
        {
            ZSTD_compressionParameters* cParams = &ms->cParams;
            uint minMatch = cParams->minMatch;
            ZSTD_blockCompressor blockCompressor = ZSTD_selectBlockCompressor(cParams -> strategy, useRowMatchFinder, ZSTD_matchState_dictMode(ms)) ?? throw new InvalidOperationException();   
            byte* istart = (byte*)(src);
            byte* iend = istart + srcSize;
            byte* ip = istart;

            if (cParams->strategy >= ZSTD_strategy.ZSTD_btopt)
            {
                nuint lastLLSize;

                ms->ldmSeqStore = rawSeqStore;
                lastLLSize = blockCompressor(ms, seqStore, rep, src, srcSize);
                ZSTD_ldm_skipRawSeqStoreBytes(rawSeqStore, srcSize);
                return lastLLSize;
            }

            assert(rawSeqStore->pos <= rawSeqStore->size);
            assert(rawSeqStore->size <= rawSeqStore->capacity);
            while (rawSeqStore->pos < rawSeqStore->size && ip < iend)
            {
                rawSeq sequence = maybeSplitSequence(rawSeqStore, (uint)(iend - ip), minMatch);
                int i;

                if (sequence.offset == 0)
                {
                    break;
                }

                assert(ip + sequence.litLength + sequence.matchLength <= iend);
                ZSTD_ldm_limitTableUpdate(ms, ip);
                ZSTD_ldm_fillFastTables(ms, (void*)ip);

                {
                    nuint newLitLength = blockCompressor(ms, seqStore, rep, (void*)ip, sequence.litLength);

                    ip += sequence.litLength;
                    for (i = 3 - 1; i > 0; i--)
                    {
                        rep[i] = rep[i - 1];
                    }

                    rep[0] = sequence.offset;
                    ZSTD_storeSeq(seqStore, newLitLength, ip - newLitLength, iend, sequence.offset + (uint)((3 - 1)), sequence.matchLength - 3);
                    ip += sequence.matchLength;
                }
            }

            ZSTD_ldm_limitTableUpdate(ms, ip);
            ZSTD_ldm_fillFastTables(ms, (void*)ip);
            return blockCompressor(ms, seqStore, rep, (void*)ip, (nuint)(iend - ip));
        }
    }
}
