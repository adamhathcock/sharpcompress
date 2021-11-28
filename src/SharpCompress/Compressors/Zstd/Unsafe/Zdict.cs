using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /*-********************************************************
        *  Helper functions
        **********************************************************/
        public static uint ZDICT_isError(nuint errorCode)
        {
            return ERR_isError(errorCode);
        }

        public static string ZDICT_getErrorName(nuint errorCode)
        {
            return ERR_getErrorName(errorCode);
        }

        private static void ZDICT_countEStats(EStats_ress_t esr, ZSTD_parameters* @params, uint* countLit, uint* offsetcodeCount, uint* matchlengthCount, uint* litlengthCount, uint* repOffsets, void* src, nuint srcSize, uint notificationLevel)
        {
            nuint blockSizeMax = (nuint)((((1 << 17)) < (1 << (int)@params->cParams.windowLog) ? ((1 << 17)) : (1 << (int)@params->cParams.windowLog)));
            nuint cSize;

            if (srcSize > blockSizeMax)
            {
                srcSize = blockSizeMax;
            }


            {
                nuint errorCode = ZSTD_compressBegin_usingCDict(esr.zc, esr.dict);

                if ((ERR_isError(errorCode)) != 0)
                {
                    return;
                }
            }

            cSize = ZSTD_compressBlock(esr.zc, esr.workPlace, (nuint)((1 << 17)), src, srcSize);
            if ((ERR_isError(cSize)) != 0)
            {
                return;
            }

            if (cSize != 0)
            {
                seqStore_t* seqStorePtr = ZSTD_getSeqStore(esr.zc);


                {
                    byte* bytePtr;

                    for (bytePtr = seqStorePtr->litStart; bytePtr < seqStorePtr->lit; bytePtr++)
                    {
                        countLit[*bytePtr]++;
                    }
                }


                {
                    uint nbSeq = (uint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);

                    ZSTD_seqToCodes(seqStorePtr);

                    {
                        byte* codePtr = seqStorePtr->ofCode;
                        uint u;

                        for (u = 0; u < nbSeq; u++)
                        {
                            offsetcodeCount[codePtr[u]]++;
                        }
                    }


                    {
                        byte* codePtr = seqStorePtr->mlCode;
                        uint u;

                        for (u = 0; u < nbSeq; u++)
                        {
                            matchlengthCount[codePtr[u]]++;
                        }
                    }


                    {
                        byte* codePtr = seqStorePtr->llCode;
                        uint u;

                        for (u = 0; u < nbSeq; u++)
                        {
                            litlengthCount[codePtr[u]]++;
                        }
                    }

                    if (nbSeq >= 2)
                    {
                        seqDef_s* seq = seqStorePtr->sequencesStart;
                        uint offset1 = seq[0].offset - 3;
                        uint offset2 = seq[1].offset - 3;

                        if (offset1 >= 1024)
                        {
                            offset1 = 0;
                        }

                        if (offset2 >= 1024)
                        {
                            offset2 = 0;
                        }

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
            {
                total += fileSizes[u];
            }

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
                {
                    break;
                }

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
            {
                countLit[u] = 2;
            }

            countLit[0] = 4;
            countLit[253] = 1;
            countLit[254] = 1;
        }

        private static nuint ZDICT_analyzeEntropy(void* dstBuffer, nuint maxDstSize, int compressionLevel, void* srcBuffer, nuint* fileSizes, uint nbFiles, void* dictBuffer, nuint dictBufferSize, uint notificationLevel)
        {
            uint* countLit = stackalloc uint[256];
            HUF_CElt_s* hufTable = stackalloc HUF_CElt_s[256];
            uint* offcodeCount = stackalloc uint[31];
            short* offcodeNCount = stackalloc short[31];
            uint offcodeMax = ZSTD_highbit32((uint)(dictBufferSize + (uint)(128 * (1 << 10))));
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
            uint u, huffLog = 11, Offlog = 8, mlLog = 9, llLog = 9, total;
            nuint pos = 0, errorCode;
            nuint eSize = 0;
            nuint totalSrcSize = ZDICT_totalSampleSize(fileSizes, nbFiles);
            nuint averageSampleSize = totalSrcSize / (nbFiles + (uint)(nbFiles == 0 ? 1 : 0));
            byte* dstPtr = (byte*)(dstBuffer);

            if (offcodeMax > 30)
            {
                eSize = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionaryCreation_failed)));
                goto _cleanup;
            }

            for (u = 0; u < 256; u++)
            {
                countLit[u] = 1;
            }

            for (u = 0; u <= offcodeMax; u++)
            {
                offcodeCount[u] = 1;
            }

            for (u = 0; u <= 52; u++)
            {
                matchLengthCount[u] = 1;
            }

            for (u = 0; u <= 35; u++)
            {
                litLengthCount[u] = 1;
            }

            memset((void*)repOffset, 0, (nuint)(sizeof(uint) * 1024));
            repOffset[1] = repOffset[4] = repOffset[8] = 1;
            memset((void*)bestRepOffset, 0, (nuint)(sizeof(offsetCount_t) * 4));
            if (compressionLevel == 0)
            {
                compressionLevel = 3;
            }

            @params = ZSTD_getParams(compressionLevel, (ulong)averageSampleSize, dictBufferSize);
            esr.dict = ZSTD_createCDict_advanced(dictBuffer, dictBufferSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef, ZSTD_dictContentType_e.ZSTD_dct_rawContent, @params.cParams, ZSTD_defaultCMem);
            esr.zc = ZSTD_createCCtx();
            esr.workPlace = malloc((nuint)((1 << 17)));
            if (esr.dict == null || esr.zc == null || esr.workPlace == null)
            {
                eSize = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                goto _cleanup;
            }

            for (u = 0; u < nbFiles; u++)
            {
                ZDICT_countEStats(esr, &@params, (uint*)countLit, (uint*)offcodeCount, (uint*)matchLengthCount, (uint*)litLengthCount, (uint*)repOffset, (void*)((sbyte*)(srcBuffer) + pos), fileSizes[u], notificationLevel);
                pos += fileSizes[u];
            }


            {
                nuint maxNbBits = HUF_buildCTable((HUF_CElt_s*)hufTable, (uint*)countLit, 255, huffLog);

                if ((ERR_isError(maxNbBits)) != 0)
                {
                    eSize = maxNbBits;
                    goto _cleanup;
                }

                if (maxNbBits == 8)
                {
                    ZDICT_flatLit((uint*)countLit);
                    maxNbBits = HUF_buildCTable((HUF_CElt_s*)hufTable, (uint*)countLit, 255, huffLog);
                    assert(maxNbBits == 9);
                }

                huffLog = (uint)(maxNbBits);
            }


            {
                uint offset;

                for (offset = 1; offset < 1024; offset++)
                {
                    ZDICT_insertSortCount(bestRepOffset, offset, repOffset[offset]);
                }
            }

            total = 0;
            for (u = 0; u <= offcodeMax; u++)
            {
                total += offcodeCount[u];
            }

            errorCode = FSE_normalizeCount((short*)offcodeNCount, Offlog, (uint*)offcodeCount, total, offcodeMax, 1);
            if ((ERR_isError(errorCode)) != 0)
            {
                eSize = errorCode;
                goto _cleanup;
            }

            Offlog = (uint)(errorCode);
            total = 0;
            for (u = 0; u <= 52; u++)
            {
                total += matchLengthCount[u];
            }

            errorCode = FSE_normalizeCount((short*)matchLengthNCount, mlLog, (uint*)matchLengthCount, total, 52, 1);
            if ((ERR_isError(errorCode)) != 0)
            {
                eSize = errorCode;
                goto _cleanup;
            }

            mlLog = (uint)(errorCode);
            total = 0;
            for (u = 0; u <= 35; u++)
            {
                total += litLengthCount[u];
            }

            errorCode = FSE_normalizeCount((short*)litLengthNCount, llLog, (uint*)litLengthCount, total, 35, 1);
            if ((ERR_isError(errorCode)) != 0)
            {
                eSize = errorCode;
                goto _cleanup;
            }

            llLog = (uint)(errorCode);

            {
                nuint hhSize = HUF_writeCTable((void*)dstPtr, maxDstSize, (HUF_CElt_s*)hufTable, 255, huffLog);

                if ((ERR_isError(hhSize)) != 0)
                {
                    eSize = hhSize;
                    goto _cleanup;
                }

                dstPtr += hhSize;
                maxDstSize -= hhSize;
                eSize += hhSize;
            }


            {
                nuint ohSize = FSE_writeNCount((void*)dstPtr, maxDstSize, (short*)offcodeNCount, 30, Offlog);

                if ((ERR_isError(ohSize)) != 0)
                {
                    eSize = ohSize;
                    goto _cleanup;
                }

                dstPtr += ohSize;
                maxDstSize -= ohSize;
                eSize += ohSize;
            }


            {
                nuint mhSize = FSE_writeNCount((void*)dstPtr, maxDstSize, (short*)matchLengthNCount, 52, mlLog);

                if ((ERR_isError(mhSize)) != 0)
                {
                    eSize = mhSize;
                    goto _cleanup;
                }

                dstPtr += mhSize;
                maxDstSize -= mhSize;
                eSize += mhSize;
            }


            {
                nuint lhSize = FSE_writeNCount((void*)dstPtr, maxDstSize, (short*)litLengthNCount, 35, llLog);

                if ((ERR_isError(lhSize)) != 0)
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
                eSize = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                goto _cleanup;
            }

            MEM_writeLE32((void*)(dstPtr + 0), repStartValue[0]);
            MEM_writeLE32((void*)(dstPtr + 4), repStartValue[1]);
            MEM_writeLE32((void*)(dstPtr + 8), repStartValue[2]);
            eSize += 12;
            _cleanup:
            ZSTD_freeCDict(esr.dict);
            ZSTD_freeCCtx(esr.zc);
            free(esr.workPlace);
            return eSize;
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
         * `dictContentSize` must be >= ZDICT_CONTENTSIZE_MIN bytes.
         * `maxDictSize` must be >= max(dictContentSize, ZSTD_DICTSIZE_MIN).
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
        public static nuint ZDICT_finalizeDictionary(void* dictBuffer, nuint dictBufferCapacity, void* customDictContent, nuint dictContentSize, void* samplesBuffer, nuint* samplesSizes, uint nbSamples, ZDICT_params_t @params)
        {
            nuint hSize;
            byte* header = stackalloc byte[256];
            int compressionLevel = (@params.compressionLevel == 0) ? 3 : @params.compressionLevel;
            uint notificationLevel = @params.notificationLevel;

            if (dictBufferCapacity < dictContentSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (dictContentSize < 128)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if (dictBufferCapacity < 256)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            MEM_writeLE32((void*)header, 0xEC30A437);

            {
                ulong randomID = XXH64(customDictContent, dictContentSize, 0);
                uint compliantID = (uint)((randomID % ((1U << 31) - 32768)) + 32768);
                uint dictID = @params.dictID != 0 ? @params.dictID : compliantID;

                MEM_writeLE32((void*)(header + 4), dictID);
            }

            hSize = 8;

            {
                nuint eSize = ZDICT_analyzeEntropy((void*)(header + hSize), 256 - hSize, compressionLevel, samplesBuffer, samplesSizes, nbSamples, customDictContent, dictContentSize, notificationLevel);

                if ((ZDICT_isError(eSize)) != 0)
                {
                    return eSize;
                }

                hSize += eSize;
            }

            if (hSize + dictContentSize > dictBufferCapacity)
            {
                dictContentSize = dictBufferCapacity - hSize;
            }


            {
                nuint dictSize = hSize + dictContentSize;
                sbyte* dictEnd = (sbyte*)(dictBuffer) + dictSize;

                memmove((void*)(dictEnd - dictContentSize), customDictContent, dictContentSize);
                memcpy(dictBuffer, (void*)header, hSize);
                return dictSize;
            }
        }

        private static nuint ZDICT_addEntropyTablesFromBuffer_advanced(void* dictBuffer, nuint dictContentSize, nuint dictBufferCapacity, void* samplesBuffer, nuint* samplesSizes, uint nbSamples, ZDICT_params_t @params)
        {
            int compressionLevel = (@params.compressionLevel == 0) ? 3 : @params.compressionLevel;
            uint notificationLevel = @params.notificationLevel;
            nuint hSize = 8;


            {
                nuint eSize = ZDICT_analyzeEntropy((void*)((sbyte*)(dictBuffer) + hSize), dictBufferCapacity - hSize, compressionLevel, samplesBuffer, samplesSizes, nbSamples, (void*)((sbyte*)(dictBuffer) + dictBufferCapacity - dictContentSize), dictContentSize, notificationLevel);

                if ((ZDICT_isError(eSize)) != 0)
                {
                    return eSize;
                }

                hSize += eSize;
            }

            MEM_writeLE32(dictBuffer, 0xEC30A437);

            {
                ulong randomID = XXH64((void*)((sbyte*)(dictBuffer) + dictBufferCapacity - dictContentSize), dictContentSize, 0);
                uint compliantID = (uint)((randomID % ((1U << 31) - 32768)) + 32768);
                uint dictID = @params.dictID != 0 ? @params.dictID : compliantID;

                MEM_writeLE32((void*)((sbyte*)(dictBuffer) + 4), dictID);
            }

            if (hSize + dictContentSize < dictBufferCapacity)
            {
                memmove((void*)((sbyte*)(dictBuffer) + hSize), (void*)((sbyte*)(dictBuffer) + dictBufferCapacity - dictContentSize), dictContentSize);
            }

            return ((dictBufferCapacity) < (hSize + dictContentSize) ? (dictBufferCapacity) : (hSize + dictContentSize));
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
        public static nuint ZDICT_trainFromBuffer(void* dictBuffer, nuint dictBufferCapacity, void* samplesBuffer, nuint* samplesSizes, uint nbSamples)
        {
            ZDICT_fastCover_params_t @params;

            memset((void*)&@params, 0, (nuint)(sizeof(ZDICT_fastCover_params_t)));
            @params.d = 8;
            @params.steps = 4;
            @params.zParams.compressionLevel = 3;
            return ZDICT_optimizeTrainFromBuffer_fastCover(dictBuffer, dictBufferCapacity, samplesBuffer, samplesSizes, nbSamples, &@params);
        }

        public static nuint ZDICT_addEntropyTablesFromBuffer(void* dictBuffer, nuint dictContentSize, nuint dictBufferCapacity, void* samplesBuffer, nuint* samplesSizes, uint nbSamples)
        {
            ZDICT_params_t @params;

            memset((void*)&@params, 0, (nuint)(sizeof(ZDICT_params_t)));
            return ZDICT_addEntropyTablesFromBuffer_advanced(dictBuffer, dictContentSize, dictBufferCapacity, samplesBuffer, samplesSizes, nbSamples, @params);
        }
    }
}
