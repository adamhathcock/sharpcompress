using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static int g_displayLevel = 0;

    /**
     * Returns the sum of the sample sizes.
     */
    private static nuint COVER_sum(nuint* samplesSizes, uint nbSamples)
    {
        nuint sum = 0;
        uint i;
        for (i = 0; i < nbSamples; ++i)
        {
            sum += samplesSizes[i];
        }

        return sum;
    }

    /**
     * Warns the user when their corpus is too small.
     */
    private static void COVER_warnOnSmallCorpus(nuint maxDictSize, nuint nbDmers, int displayLevel)
    {
        double ratio = nbDmers / (double)maxDictSize;
        if (ratio >= 10)
        {
            return;
        }
    }

    /**
     * Computes the number of epochs and the size of each epoch.
     * We will make sure that each epoch gets at least 10 * k bytes.
     *
     * The COVER algorithms divide the data up into epochs of equal size and
     * select one segment from each epoch.
     *
     * @param maxDictSize The maximum allowed dictionary size.
     * @param nbDmers     The number of dmers we are training on.
     * @param k           The parameter k (segment size).
     * @param passes      The target number of passes over the dmer corpus.
     *                    More passes means a better dictionary.
     */
    private static COVER_epoch_info_t COVER_computeEpochs(
        uint maxDictSize,
        uint nbDmers,
        uint k,
        uint passes
    )
    {
        uint minEpochSize = k * 10;
        COVER_epoch_info_t epochs;
        epochs.num = 1 > maxDictSize / k / passes ? 1 : maxDictSize / k / passes;
        epochs.size = nbDmers / epochs.num;
        if (epochs.size >= minEpochSize)
        {
            assert(epochs.size * epochs.num <= nbDmers);
            return epochs;
        }

        epochs.size = minEpochSize < nbDmers ? minEpochSize : nbDmers;
        epochs.num = nbDmers / epochs.size;
        assert(epochs.size * epochs.num <= nbDmers);
        return epochs;
    }

    /**
     *  Checks total compressed size of a dictionary
     */
    private static nuint COVER_checkTotalCompressedSize(
        ZDICT_cover_params_t parameters,
        nuint* samplesSizes,
        byte* samples,
        nuint* offsets,
        nuint nbTrainSamples,
        nuint nbSamples,
        byte* dict,
        nuint dictBufferCapacity
    )
    {
        nuint totalCompressedSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        /* Pointers */
        ZSTD_CCtx_s* cctx;
        ZSTD_CDict_s* cdict;
        void* dst;
        /* Local variables */
        nuint dstCapacity;
        nuint i;
        {
            nuint maxSampleSize = 0;
            i = parameters.splitPoint < 1 ? nbTrainSamples : 0;
            for (; i < nbSamples; ++i)
            {
                maxSampleSize = samplesSizes[i] > maxSampleSize ? samplesSizes[i] : maxSampleSize;
            }

            dstCapacity = ZSTD_compressBound(maxSampleSize);
            dst = malloc(dstCapacity);
        }

        cctx = ZSTD_createCCtx();
        cdict = ZSTD_createCDict(dict, dictBufferCapacity, parameters.zParams.compressionLevel);
        if (dst == null || cctx == null || cdict == null)
        {
            goto _compressCleanup;
        }

        totalCompressedSize = dictBufferCapacity;
        i = parameters.splitPoint < 1 ? nbTrainSamples : 0;
        for (; i < nbSamples; ++i)
        {
            nuint size = ZSTD_compress_usingCDict(
                cctx,
                dst,
                dstCapacity,
                samples + offsets[i],
                samplesSizes[i],
                cdict
            );
            if (ERR_isError(size))
            {
                totalCompressedSize = size;
                goto _compressCleanup;
            }

            totalCompressedSize += size;
        }

        _compressCleanup:
        ZSTD_freeCCtx(cctx);
        ZSTD_freeCDict(cdict);
        if (dst != null)
        {
            free(dst);
        }

        return totalCompressedSize;
    }

    /**
     * Initialize the `COVER_best_t`.
     */
    private static void COVER_best_init(COVER_best_s* best)
    {
        if (best == null)
            return;
        SynchronizationWrapper.Init(&best->mutex);
        best->liveJobs = 0;
        best->dict = null;
        best->dictSize = 0;
        best->compressedSize = unchecked((nuint)(-1));
        best->parameters = new ZDICT_cover_params_t();
    }

    /**
     * Wait until liveJobs == 0.
     */
    private static void COVER_best_wait(COVER_best_s* best)
    {
        if (best == null)
        {
            return;
        }

        SynchronizationWrapper.Enter(&best->mutex);
        while (best->liveJobs != 0)
        {
            SynchronizationWrapper.Wait(&best->mutex);
        }

        SynchronizationWrapper.Exit(&best->mutex);
    }

    /**
     * Call COVER_best_wait() and then destroy the COVER_best_t.
     */
    private static void COVER_best_destroy(COVER_best_s* best)
    {
        if (best == null)
        {
            return;
        }

        COVER_best_wait(best);
        if (best->dict != null)
        {
            free(best->dict);
        }

        SynchronizationWrapper.Free(&best->mutex);
    }

    /**
     * Called when a thread is about to be launched.
     * Increments liveJobs.
     */
    private static void COVER_best_start(COVER_best_s* best)
    {
        if (best == null)
        {
            return;
        }

        SynchronizationWrapper.Enter(&best->mutex);
        ++best->liveJobs;
        SynchronizationWrapper.Exit(&best->mutex);
    }

    /**
     * Called when a thread finishes executing, both on error or success.
     * Decrements liveJobs and signals any waiting threads if liveJobs == 0.
     * If this dictionary is the best so far save it and its parameters.
     */
    private static void COVER_best_finish(
        COVER_best_s* best,
        ZDICT_cover_params_t parameters,
        COVER_dictSelection selection
    )
    {
        void* dict = selection.dictContent;
        nuint compressedSize = selection.totalCompressedSize;
        nuint dictSize = selection.dictSize;
        if (best == null)
        {
            return;
        }

        {
            nuint liveJobs;
            SynchronizationWrapper.Enter(&best->mutex);
            --best->liveJobs;
            liveJobs = best->liveJobs;
            if (compressedSize < best->compressedSize)
            {
                if (best->dict == null || best->dictSize < dictSize)
                {
                    if (best->dict != null)
                    {
                        free(best->dict);
                    }

                    best->dict = malloc(dictSize);
                    if (best->dict == null)
                    {
                        best->compressedSize = unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)
                        );
                        best->dictSize = 0;
                        SynchronizationWrapper.Pulse(&best->mutex);
                        SynchronizationWrapper.Exit(&best->mutex);
                        return;
                    }
                }

                if (dict != null)
                {
                    memcpy(best->dict, dict, (uint)dictSize);
                    best->dictSize = dictSize;
                    best->parameters = parameters;
                    best->compressedSize = compressedSize;
                }
            }

            if (liveJobs == 0)
            {
                SynchronizationWrapper.PulseAll(&best->mutex);
            }

            SynchronizationWrapper.Exit(&best->mutex);
        }
    }

    private static COVER_dictSelection setDictSelection(byte* buf, nuint s, nuint csz)
    {
        COVER_dictSelection ds;
        ds.dictContent = buf;
        ds.dictSize = s;
        ds.totalCompressedSize = csz;
        return ds;
    }

    /**
     * Error function for COVER_selectDict function. Returns a struct where
     * return.totalCompressedSize is a ZSTD error.
     */
    private static COVER_dictSelection COVER_dictSelectionError(nuint error)
    {
        return setDictSelection(null, 0, error);
    }

    /**
     * Error function for COVER_selectDict function. Checks if the return
     * value is an error.
     */
    private static uint COVER_dictSelectionIsError(COVER_dictSelection selection)
    {
        return ERR_isError(selection.totalCompressedSize) || selection.dictContent == null
            ? 1U
            : 0U;
    }

    /**
     * Always call after selectDict is called to free up used memory from
     * newly created dictionary.
     */
    private static void COVER_dictSelectionFree(COVER_dictSelection selection)
    {
        free(selection.dictContent);
    }

    /**
     * Called to finalize the dictionary and select one based on whether or not
     * the shrink-dict flag was enabled. If enabled the dictionary used is the
     * smallest dictionary within a specified regression of the compressed size
     * from the largest dictionary.
     */
    private static COVER_dictSelection COVER_selectDict(
        byte* customDictContent,
        nuint dictBufferCapacity,
        nuint dictContentSize,
        byte* samplesBuffer,
        nuint* samplesSizes,
        uint nbFinalizeSamples,
        nuint nbCheckSamples,
        nuint nbSamples,
        ZDICT_cover_params_t @params,
        nuint* offsets,
        nuint totalCompressedSize
    )
    {
        nuint largestDict = 0;
        nuint largestCompressed = 0;
        byte* customDictContentEnd = customDictContent + dictContentSize;
        byte* largestDictbuffer = (byte*)malloc(dictBufferCapacity);
        byte* candidateDictBuffer = (byte*)malloc(dictBufferCapacity);
        double regressionTolerance = (double)@params.shrinkDictMaxRegression / 100 + 1;
        if (largestDictbuffer == null || candidateDictBuffer == null)
        {
            free(largestDictbuffer);
            free(candidateDictBuffer);
            return COVER_dictSelectionError(dictContentSize);
        }

        memcpy(largestDictbuffer, customDictContent, (uint)dictContentSize);
        dictContentSize = ZDICT_finalizeDictionary(
            largestDictbuffer,
            dictBufferCapacity,
            customDictContent,
            dictContentSize,
            samplesBuffer,
            samplesSizes,
            nbFinalizeSamples,
            @params.zParams
        );
        if (ZDICT_isError(dictContentSize))
        {
            free(largestDictbuffer);
            free(candidateDictBuffer);
            return COVER_dictSelectionError(dictContentSize);
        }

        totalCompressedSize = COVER_checkTotalCompressedSize(
            @params,
            samplesSizes,
            samplesBuffer,
            offsets,
            nbCheckSamples,
            nbSamples,
            largestDictbuffer,
            dictContentSize
        );
        if (ERR_isError(totalCompressedSize))
        {
            free(largestDictbuffer);
            free(candidateDictBuffer);
            return COVER_dictSelectionError(totalCompressedSize);
        }

        if (@params.shrinkDict == 0)
        {
            free(candidateDictBuffer);
            return setDictSelection(largestDictbuffer, dictContentSize, totalCompressedSize);
        }

        largestDict = dictContentSize;
        largestCompressed = totalCompressedSize;
        dictContentSize = 256;
        while (dictContentSize < largestDict)
        {
            memcpy(candidateDictBuffer, largestDictbuffer, (uint)largestDict);
            dictContentSize = ZDICT_finalizeDictionary(
                candidateDictBuffer,
                dictBufferCapacity,
                customDictContentEnd - dictContentSize,
                dictContentSize,
                samplesBuffer,
                samplesSizes,
                nbFinalizeSamples,
                @params.zParams
            );
            if (ZDICT_isError(dictContentSize))
            {
                free(largestDictbuffer);
                free(candidateDictBuffer);
                return COVER_dictSelectionError(dictContentSize);
            }

            totalCompressedSize = COVER_checkTotalCompressedSize(
                @params,
                samplesSizes,
                samplesBuffer,
                offsets,
                nbCheckSamples,
                nbSamples,
                candidateDictBuffer,
                dictContentSize
            );
            if (ERR_isError(totalCompressedSize))
            {
                free(largestDictbuffer);
                free(candidateDictBuffer);
                return COVER_dictSelectionError(totalCompressedSize);
            }

            if (totalCompressedSize <= largestCompressed * regressionTolerance)
            {
                free(largestDictbuffer);
                return setDictSelection(candidateDictBuffer, dictContentSize, totalCompressedSize);
            }

            dictContentSize *= 2;
        }

        dictContentSize = largestDict;
        totalCompressedSize = largestCompressed;
        free(candidateDictBuffer);
        return setDictSelection(largestDictbuffer, dictContentSize, totalCompressedSize);
    }
}
