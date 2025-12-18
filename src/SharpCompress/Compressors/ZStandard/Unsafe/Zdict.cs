using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*-********************************************************
     *  Helper functions
     **********************************************************/
    public static bool ZDICT_isError(nuint errorCode)
    {
        return ERR_isError(errorCode);
    }

    public static string ZDICT_getErrorName(nuint errorCode)
    {
        return ERR_getErrorName(errorCode);
    }

    private static void ZDICT_countEStats(
        EStats_ress_t esr,
        ZSTD_parameters* @params,
        uint* countLit,
        uint* offsetcodeCount,
        uint* matchlengthCount,
        uint* litlengthCount,
        uint* repOffsets,
        void* src,
        nuint srcSize,
        uint notificationLevel
    )
    {
        nuint blockSizeMax = (nuint)(
            1 << 17 < 1 << (int)@params->cParams.windowLog
                ? 1 << 17
                : 1 << (int)@params->cParams.windowLog
        );
        nuint cSize;
        if (srcSize > blockSizeMax)
            srcSize = blockSizeMax;
        {
            nuint errorCode = ZSTD_compressBegin_usingCDict_deprecated(esr.zc, esr.dict);
            if (ERR_isError(errorCode))
            {
                return;
            }
        }

        cSize = ZSTD_compressBlock_deprecated(esr.zc, esr.workPlace, 1 << 17, src, srcSize);
        if (ERR_isError(cSize))
        {
            return;
        }

        if (cSize != 0)
        {
            SeqStore_t* seqStorePtr = ZSTD_getSeqStore(esr.zc);
            {
                byte* bytePtr;
                for (bytePtr = seqStorePtr->litStart; bytePtr < seqStorePtr->lit; bytePtr++)
                    countLit[*bytePtr]++;
            }

            {
                uint nbSeq = (uint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
                ZSTD_seqToCodes(seqStorePtr);
                {
                    byte* codePtr = seqStorePtr->ofCode;
                    uint u;
                    for (u = 0; u < nbSeq; u++)
                        offsetcodeCount[codePtr[u]]++;
                }

                {
                    byte* codePtr = seqStorePtr->mlCode;
                    uint u;
                    for (u = 0; u < nbSeq; u++)
                        matchlengthCount[codePtr[u]]++;
                }

                {
                    byte* codePtr = seqStorePtr->llCode;
                    uint u;
                    for (u = 0; u < nbSeq; u++)
                        litlengthCount[codePtr[u]]++;
                }

                if (nbSeq >= 2)
                {
                    SeqDef_s* seq = seqStorePtr->sequencesStart;
                    uint offset1 = seq[0].offBase - 3;
                    uint offset2 = seq[1].offBase - 3;
                    if (offset1 >= 1024)
                        offset1 = 0;
                    if (offset2 >= 1024)
                        offset2 = 0;
                    repOffsets[offset1] += 3;
                    repOffsets[offset2] += 1;
                }
            }
        }
    }

    private static nuint ZDICT_totalSampleSize(nuint* fileSizes, uint nbFiles)
    {
        nuint total = 0;
        uint u;
        for (u = 0; u < nbFiles; u++)
            total += fileSizes[u];
        return total;
    }

    private static void ZDICT_insertSortCount(offsetCount_t* table, uint val, uint count)
    {
        uint u;
        table[3].offset = val;
        table[3].count = count;
        for (u = 3; u > 0; u--)
        {
            offsetCount_t tmp;
            if (table[u - 1].count >= table[u].count)
                break;
            tmp = table[u - 1];
            table[u - 1] = table[u];
            table[u] = tmp;
        }
    }

    /* ZDICT_flatLit() :
     * rewrite `countLit` to contain a mostly flat but still compressible distribution of literals.
     * necessary to avoid generating a non-compressible distribution that HUF_writeCTable() cannot encode.
     */
    private static void ZDICT_flatLit(uint* countLit)
    {
        int u;
        for (u = 1; u < 256; u++)
            countLit[u] = 2;
        countLit[0] = 4;
        countLit[253] = 1;
        countLit[254] = 1;
    }

    private static nuint ZDICT_analyzeEntropy(
        void* dstBuffer,
        nuint maxDstSize,
        int compressionLevel,
        void* srcBuffer,
        nuint* fileSizes,
        uint nbFiles,
        void* dictBuffer,
        nuint dictBufferSize,
        uint notificationLevel
    )
    {
        uint* countLit = stackalloc uint[256];
        /* no final ; */
        nuint* hufTable = stackalloc nuint[257];
        uint* offcodeCount = stackalloc uint[31];
        short* offcodeNCount = stackalloc short[31];
        uint offcodeMax = ZSTD_highbit32((uint)(dictBufferSize + 128 * (1 << 10)));
        uint* matchLengthCount = stackalloc uint[53];
        short* matchLengthNCount = stackalloc short[53];
        uint* litLengthCount = stackalloc uint[36];
        short* litLengthNCount = stackalloc short[36];
        uint* repOffset = stackalloc uint[1024];
        offsetCount_t* bestRepOffset = stackalloc offsetCount_t[4];
        EStats_ress_t esr = new EStats_ress_t
        {
            dict = null,
            zc = null,
            workPlace = null,
        };
        ZSTD_parameters @params;
        uint u,
            huffLog = 11,
            Offlog = 8,
            mlLog = 9,
            llLog = 9,
            total;
        nuint pos = 0,
            errorCode;
        nuint eSize = 0;
        nuint totalSrcSize = ZDICT_totalSampleSize(fileSizes, nbFiles);
        nuint averageSampleSize = totalSrcSize / (nbFiles + (uint)(nbFiles == 0 ? 1 : 0));
        byte* dstPtr = (byte*)dstBuffer;
        uint* wksp = stackalloc uint[1216];
        if (offcodeMax > 30)
        {
            eSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionaryCreation_failed));
            goto _cleanup;
        }

        for (u = 0; u < 256; u++)
            countLit[u] = 1;
        for (u = 0; u <= offcodeMax; u++)
            offcodeCount[u] = 1;
        for (u = 0; u <= 52; u++)
            matchLengthCount[u] = 1;
        for (u = 0; u <= 35; u++)
            litLengthCount[u] = 1;
        memset(repOffset, 0, sizeof(uint) * 1024);
        repOffset[1] = repOffset[4] = repOffset[8] = 1;
        memset(bestRepOffset, 0, (uint)(sizeof(offsetCount_t) * 4));
        if (compressionLevel == 0)
            compressionLevel = 3;
        @params = ZSTD_getParams(compressionLevel, averageSampleSize, dictBufferSize);
        esr.dict = ZSTD_createCDict_advanced(
            dictBuffer,
            dictBufferSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
            ZSTD_dictContentType_e.ZSTD_dct_rawContent,
            @params.cParams,
            ZSTD_defaultCMem
        );
        esr.zc = ZSTD_createCCtx();
        esr.workPlace = malloc(1 << 17);
        if (esr.dict == null || esr.zc == null || esr.workPlace == null)
        {
            eSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            goto _cleanup;
        }

        for (u = 0; u < nbFiles; u++)
        {
            ZDICT_countEStats(
                esr,
                &@params,
                countLit,
                offcodeCount,
                matchLengthCount,
                litLengthCount,
                repOffset,
                (sbyte*)srcBuffer + pos,
                fileSizes[u],
                notificationLevel
            );
            pos += fileSizes[u];
        }

        if (notificationLevel >= 4)
        {
            for (u = 0; u <= offcodeMax; u++) { }
        }

        {
            nuint maxNbBits = HUF_buildCTable_wksp(
                hufTable,
                countLit,
                255,
                huffLog,
                wksp,
                sizeof(uint) * 1216
            );
            if (ERR_isError(maxNbBits))
            {
                eSize = maxNbBits;
                goto _cleanup;
            }

            if (maxNbBits == 8)
            {
                ZDICT_flatLit(countLit);
                maxNbBits = HUF_buildCTable_wksp(
                    hufTable,
                    countLit,
                    255,
                    huffLog,
                    wksp,
                    sizeof(uint) * 1216
                );
                assert(maxNbBits == 9);
            }

            huffLog = (uint)maxNbBits;
        }

        {
            uint offset;
            for (offset = 1; offset < 1024; offset++)
                ZDICT_insertSortCount(bestRepOffset, offset, repOffset[offset]);
        }

        total = 0;
        for (u = 0; u <= offcodeMax; u++)
            total += offcodeCount[u];
        errorCode = FSE_normalizeCount(offcodeNCount, Offlog, offcodeCount, total, offcodeMax, 1);
        if (ERR_isError(errorCode))
        {
            eSize = errorCode;
            goto _cleanup;
        }

        Offlog = (uint)errorCode;
        total = 0;
        for (u = 0; u <= 52; u++)
            total += matchLengthCount[u];
        errorCode = FSE_normalizeCount(matchLengthNCount, mlLog, matchLengthCount, total, 52, 1);
        if (ERR_isError(errorCode))
        {
            eSize = errorCode;
            goto _cleanup;
        }

        mlLog = (uint)errorCode;
        total = 0;
        for (u = 0; u <= 35; u++)
            total += litLengthCount[u];
        errorCode = FSE_normalizeCount(litLengthNCount, llLog, litLengthCount, total, 35, 1);
        if (ERR_isError(errorCode))
        {
            eSize = errorCode;
            goto _cleanup;
        }

        llLog = (uint)errorCode;
        {
            nuint hhSize = HUF_writeCTable_wksp(
                dstPtr,
                maxDstSize,
                hufTable,
                255,
                huffLog,
                wksp,
                sizeof(uint) * 1216
            );
            if (ERR_isError(hhSize))
            {
                eSize = hhSize;
                goto _cleanup;
            }

            dstPtr += hhSize;
            maxDstSize -= hhSize;
            eSize += hhSize;
        }

        {
            nuint ohSize = FSE_writeNCount(dstPtr, maxDstSize, offcodeNCount, 30, Offlog);
            if (ERR_isError(ohSize))
            {
                eSize = ohSize;
                goto _cleanup;
            }

            dstPtr += ohSize;
            maxDstSize -= ohSize;
            eSize += ohSize;
        }

        {
            nuint mhSize = FSE_writeNCount(dstPtr, maxDstSize, matchLengthNCount, 52, mlLog);
            if (ERR_isError(mhSize))
            {
                eSize = mhSize;
                goto _cleanup;
            }

            dstPtr += mhSize;
            maxDstSize -= mhSize;
            eSize += mhSize;
        }

        {
            nuint lhSize = FSE_writeNCount(dstPtr, maxDstSize, litLengthNCount, 35, llLog);
            if (ERR_isError(lhSize))
            {
                eSize = lhSize;
                goto _cleanup;
            }

            dstPtr += lhSize;
            maxDstSize -= lhSize;
            eSize += lhSize;
        }

        if (maxDstSize < 12)
        {
            eSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            goto _cleanup;
        }

        MEM_writeLE32(dstPtr + 0, repStartValue[0]);
        MEM_writeLE32(dstPtr + 4, repStartValue[1]);
        MEM_writeLE32(dstPtr + 8, repStartValue[2]);
        eSize += 12;
        _cleanup:
        ZSTD_freeCDict(esr.dict);
        ZSTD_freeCCtx(esr.zc);
        free(esr.workPlace);
        return eSize;
    }

    /**
     * @returns the maximum repcode value
     */
    private static uint ZDICT_maxRep(uint* reps)
    {
        uint maxRep = reps[0];
        int r;
        for (r = 1; r < 3; ++r)
            maxRep = maxRep > reps[r] ? maxRep : reps[r];
        return maxRep;
    }

    /*! ZDICT_finalizeDictionary():
     * Given a custom content as a basis for dictionary, and a set of samples,
     * finalize dictionary by adding headers and statistics according to the zstd
     * dictionary format.
     *
     * Samples must be stored concatenated in a flat buffer `samplesBuffer`,
     * supplied with an array of sizes `samplesSizes`, providing the size of each
     * sample in order. The samples are used to construct the statistics, so they
     * should be representative of what you will compress with this dictionary.
     *
     * The compression level can be set in `parameters`. You should pass the
     * compression level you expect to use in production. The statistics for each
     * compression level differ, so tuning the dictionary for the compression level
     * can help quite a bit.
     *
     * You can set an explicit dictionary ID in `parameters`, or allow us to pick
     * a random dictionary ID for you, but we can't guarantee no collisions.
     *
     * The dstDictBuffer and the dictContent may overlap, and the content will be
     * appended to the end of the header. If the header + the content doesn't fit in
     * maxDictSize the beginning of the content is truncated to make room, since it
     * is presumed that the most profitable content is at the end of the dictionary,
     * since that is the cheapest to reference.
     *
     * `maxDictSize` must be >= max(dictContentSize, ZDICT_DICTSIZE_MIN).
     *
     * @return: size of dictionary stored into `dstDictBuffer` (<= `maxDictSize`),
     *          or an error code, which can be tested by ZDICT_isError().
     * Note: ZDICT_finalizeDictionary() will push notifications into stderr if
     *       instructed to, using notificationLevel>0.
     * NOTE: This function currently may fail in several edge cases including:
     *         * Not enough samples
     *         * Samples are uncompressible
     *         * Samples are all exactly the same
     */
    public static nuint ZDICT_finalizeDictionary(
        void* dictBuffer,
        nuint dictBufferCapacity,
        void* customDictContent,
        nuint dictContentSize,
        void* samplesBuffer,
        nuint* samplesSizes,
        uint nbSamples,
        ZDICT_params_t @params
    )
    {
        nuint hSize;
        byte* header = stackalloc byte[256];
        int compressionLevel = @params.compressionLevel == 0 ? 3 : @params.compressionLevel;
        uint notificationLevel = @params.notificationLevel;
        /* The final dictionary content must be at least as large as the largest repcode */
        nuint minContentSize = ZDICT_maxRep(repStartValue);
        nuint paddingSize;
        if (dictBufferCapacity < dictContentSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        if (dictBufferCapacity < 256)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        MEM_writeLE32(header, 0xEC30A437);
        {
            ulong randomID = ZSTD_XXH64(customDictContent, dictContentSize, 0);
            uint compliantID = (uint)(randomID % ((1U << 31) - 32768) + 32768);
            uint dictID = @params.dictID != 0 ? @params.dictID : compliantID;
            MEM_writeLE32(header + 4, dictID);
        }

        hSize = 8;
        {
            nuint eSize = ZDICT_analyzeEntropy(
                header + hSize,
                256 - hSize,
                compressionLevel,
                samplesBuffer,
                samplesSizes,
                nbSamples,
                customDictContent,
                dictContentSize,
                notificationLevel
            );
            if (ZDICT_isError(eSize))
                return eSize;
            hSize += eSize;
        }

        if (hSize + dictContentSize > dictBufferCapacity)
        {
            dictContentSize = dictBufferCapacity - hSize;
        }

        if (dictContentSize < minContentSize)
        {
            if (hSize + minContentSize > dictBufferCapacity)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            paddingSize = minContentSize - dictContentSize;
        }
        else
        {
            paddingSize = 0;
        }

        {
            nuint dictSize = hSize + paddingSize + dictContentSize;
            /* The dictionary consists of the header, optional padding, and the content.
             * The padding comes before the content because the "best" position in the
             * dictionary is the last byte.
             */
            byte* outDictHeader = (byte*)dictBuffer;
            byte* outDictPadding = outDictHeader + hSize;
            byte* outDictContent = outDictPadding + paddingSize;
            assert(dictSize <= dictBufferCapacity);
            assert(outDictContent + dictContentSize == (byte*)dictBuffer + dictSize);
            memmove(outDictContent, customDictContent, dictContentSize);
            memcpy(outDictHeader, header, (uint)hSize);
            memset(outDictPadding, 0, (uint)paddingSize);
            return dictSize;
        }
    }

    private static nuint ZDICT_addEntropyTablesFromBuffer_advanced(
        void* dictBuffer,
        nuint dictContentSize,
        nuint dictBufferCapacity,
        void* samplesBuffer,
        nuint* samplesSizes,
        uint nbSamples,
        ZDICT_params_t @params
    )
    {
        int compressionLevel = @params.compressionLevel == 0 ? 3 : @params.compressionLevel;
        uint notificationLevel = @params.notificationLevel;
        nuint hSize = 8;
        {
            nuint eSize = ZDICT_analyzeEntropy(
                (sbyte*)dictBuffer + hSize,
                dictBufferCapacity - hSize,
                compressionLevel,
                samplesBuffer,
                samplesSizes,
                nbSamples,
                (sbyte*)dictBuffer + dictBufferCapacity - dictContentSize,
                dictContentSize,
                notificationLevel
            );
            if (ZDICT_isError(eSize))
                return eSize;
            hSize += eSize;
        }

        MEM_writeLE32(dictBuffer, 0xEC30A437);
        {
            ulong randomID = ZSTD_XXH64(
                (sbyte*)dictBuffer + dictBufferCapacity - dictContentSize,
                dictContentSize,
                0
            );
            uint compliantID = (uint)(randomID % ((1U << 31) - 32768) + 32768);
            uint dictID = @params.dictID != 0 ? @params.dictID : compliantID;
            MEM_writeLE32((sbyte*)dictBuffer + 4, dictID);
        }

        if (hSize + dictContentSize < dictBufferCapacity)
            memmove(
                (sbyte*)dictBuffer + hSize,
                (sbyte*)dictBuffer + dictBufferCapacity - dictContentSize,
                dictContentSize
            );
        return dictBufferCapacity < hSize + dictContentSize
            ? dictBufferCapacity
            : hSize + dictContentSize;
    }

    /*! ZDICT_trainFromBuffer():
     *  Train a dictionary from an array of samples.
     *  Redirect towards ZDICT_optimizeTrainFromBuffer_fastCover() single-threaded, with d=8, steps=4,
     *  f=20, and accel=1.
     *  Samples must be stored concatenated in a single flat buffer `samplesBuffer`,
     *  supplied with an array of sizes `samplesSizes`, providing the size of each sample, in order.
     *  The resulting dictionary will be saved into `dictBuffer`.
     * @return: size of dictionary stored into `dictBuffer` (<= `dictBufferCapacity`)
     *          or an error code, which can be tested with ZDICT_isError().
     *  Note:  Dictionary training will fail if there are not enough samples to construct a
     *         dictionary, or if most of the samples are too small (< 8 bytes being the lower limit).
     *         If dictionary training fails, you should use zstd without a dictionary, as the dictionary
     *         would've been ineffective anyways. If you believe your samples would benefit from a dictionary
     *         please open an issue with details, and we can look into it.
     *  Note: ZDICT_trainFromBuffer()'s memory usage is about 6 MB.
     *  Tips: In general, a reasonable dictionary has a size of ~ 100 KB.
     *        It's possible to select smaller or larger size, just by specifying `dictBufferCapacity`.
     *        In general, it's recommended to provide a few thousands samples, though this can vary a lot.
     *        It's recommended that total size of all samples be about ~x100 times the target size of dictionary.
     */
    public static nuint ZDICT_trainFromBuffer(
        void* dictBuffer,
        nuint dictBufferCapacity,
        void* samplesBuffer,
        nuint* samplesSizes,
        uint nbSamples
    )
    {
        ZDICT_fastCover_params_t @params;
        @params = new ZDICT_fastCover_params_t { d = 8, steps = 4 };
        @params.zParams.compressionLevel = 3;
        return ZDICT_optimizeTrainFromBuffer_fastCover(
            dictBuffer,
            dictBufferCapacity,
            samplesBuffer,
            samplesSizes,
            nbSamples,
            &@params
        );
    }

    public static nuint ZDICT_addEntropyTablesFromBuffer(
        void* dictBuffer,
        nuint dictContentSize,
        nuint dictBufferCapacity,
        void* samplesBuffer,
        nuint* samplesSizes,
        uint nbSamples
    )
    {
        ZDICT_params_t @params;
        @params = new ZDICT_params_t();
        return ZDICT_addEntropyTablesFromBuffer_advanced(
            dictBuffer,
            dictContentSize,
            dictBufferCapacity,
            samplesBuffer,
            samplesSizes,
            nbSamples,
            @params
        );
    }
}
