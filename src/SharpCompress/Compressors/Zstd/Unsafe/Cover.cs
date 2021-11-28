using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static int g_displayLevel = 2;

        /**
         * Returns the sum of the sample sizes.
         */
        public static nuint COVER_sum(nuint* samplesSizes, uint nbSamples)
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
        public static void COVER_warnOnSmallCorpus(nuint maxDictSize, nuint nbDmers, int displayLevel)
        {
            double ratio = (double)(nbDmers) / maxDictSize;

            if (ratio >= 10)
            {
                return;
            }

            if (displayLevel >= 1)
            {
        ;
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
        public static COVER_epoch_info_t COVER_computeEpochs(uint maxDictSize, uint nbDmers, uint k, uint passes)
        {
            uint minEpochSize = k * 10;
            COVER_epoch_info_t epochs;

            epochs.num = (uint)((1) > (maxDictSize / k / passes) ? (1) : (maxDictSize / k / passes));
            epochs.size = nbDmers / epochs.num;
            if (epochs.size >= minEpochSize)
            {
                assert(epochs.size * epochs.num <= nbDmers);
                return epochs;
            }

            epochs.size = ((minEpochSize) < (nbDmers) ? (minEpochSize) : (nbDmers));
            epochs.num = nbDmers / epochs.size;
            assert(epochs.size * epochs.num <= nbDmers);
            return epochs;
        }

        /**
         *  Checks total compressed size of a dictionary
         */
        public static nuint COVER_checkTotalCompressedSize(ZDICT_cover_params_t parameters, nuint* samplesSizes, byte* samples, nuint* offsets, nuint nbTrainSamples, nuint nbSamples, byte* dict, nuint dictBufferCapacity)
        {
            nuint totalCompressedSize = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            ZSTD_CCtx_s* cctx;
            ZSTD_CDict_s* cdict;
            void* dst;
            nuint dstCapacity;
            nuint i;


            {
                nuint maxSampleSize = 0;

                i = parameters.splitPoint < 1.0 ? nbTrainSamples : 0;
                for (; i < nbSamples; ++i)
                {
                    maxSampleSize = ((samplesSizes[i]) > (maxSampleSize) ? (samplesSizes[i]) : (maxSampleSize));
                }

                dstCapacity = ZSTD_compressBound(maxSampleSize);
                dst = malloc(dstCapacity);
            }

            cctx = ZSTD_createCCtx();
            cdict = ZSTD_createCDict((void*)dict, dictBufferCapacity, parameters.zParams.compressionLevel);
            if (dst == null || cctx == null || cdict == null)
            {
                goto _compressCleanup;
            }

            totalCompressedSize = dictBufferCapacity;
            i = parameters.splitPoint < 1.0 ? nbTrainSamples : 0;
            for (; i < nbSamples; ++i)
            {
                nuint size = ZSTD_compress_usingCDict(cctx, dst, dstCapacity, (void*)(samples + offsets[i]), samplesSizes[i], cdict);

                if ((ERR_isError(size)) != 0)
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
        public static void COVER_best_init(COVER_best_s* best)
        {
            if (best == null)
            {
                return;
            }



            best->liveJobs = 0;
            best->dict = null;
            best->dictSize = 0;
            best->compressedSize = unchecked((nuint)(-1));
            memset((void*)&best->parameters, 0, (nuint)(sizeof(ZDICT_cover_params_t)));
        }

        /**
         * Wait until liveJobs == 0.
         */
        public static void COVER_best_wait(COVER_best_s* best)
        {
            if (best == null)
            {
                return;
            }


            while (best->liveJobs != 0)
            {

            }


        }

        /**
         * Call COVER_best_wait() and then destroy the COVER_best_t.
         */
        public static void COVER_best_destroy(COVER_best_s* best)
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



        }

        /**
         * Called when a thread is about to be launched.
         * Increments liveJobs.
         */
        public static void COVER_best_start(COVER_best_s* best)
        {
            if (best == null)
            {
                return;
            }


            ++best->liveJobs;

        }

        /**
         * Called when a thread finishes executing, both on error or success.
         * Decrements liveJobs and signals any waiting threads if liveJobs == 0.
         * If this dictionary is the best so far save it and its parameters.
         */
        public static void COVER_best_finish(COVER_best_s* best, ZDICT_cover_params_t parameters, COVER_dictSelection selection)
        {
            void* dict = (void*)selection.dictContent;
            nuint compressedSize = selection.totalCompressedSize;
            nuint dictSize = selection.dictSize;

            if (best == null)
            {
                return;
            }


            {
                nuint liveJobs;


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
                            best->compressedSize = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                            best->dictSize = 0;


                            return;
                        }
                    }

                    if (dict != null)
                    {
                        memcpy(best->dict, dict, dictSize);
                        best->dictSize = dictSize;
                        best->parameters = parameters;
                        best->compressedSize = compressedSize;
                    }
                }

                if (liveJobs == 0)
                {

                }


            }
        }

        /**
          * Error function for COVER_selectDict function. Returns a struct where
          * return.totalCompressedSize is a ZSTD error.
          */
        public static COVER_dictSelection COVER_dictSelectionError(nuint error)
        {
            COVER_dictSelection selection = new COVER_dictSelection
            {
                dictContent = null,
                dictSize = 0,
                totalCompressedSize = error,
            };

            return selection;
        }

        /**
         * Error function for COVER_selectDict function. Checks if the return
         * value is an error.
         */
        public static uint COVER_dictSelectionIsError(COVER_dictSelection selection)
        {
            return ((((ERR_isError(selection.totalCompressedSize)) != 0 || selection.dictContent == null)) ? 1U : 0U);
        }

        /**
         * Always call after selectDict is called to free up used memory from
         * newly created dictionary.
         */
        public static void COVER_dictSelectionFree(COVER_dictSelection selection)
        {
            free((void*)selection.dictContent);
        }

        /**
         * Called to finalize the dictionary and select one based on whether or not
         * the shrink-dict flag was enabled. If enabled the dictionary used is the
         * smallest dictionary within a specified regression of the compressed size
         * from the largest dictionary.
         */
        public static COVER_dictSelection COVER_selectDict(byte* customDictContent, nuint dictBufferCapacity, nuint dictContentSize, byte* samplesBuffer, nuint* samplesSizes, uint nbFinalizeSamples, nuint nbCheckSamples, nuint nbSamples, ZDICT_cover_params_t @params, nuint* offsets, nuint totalCompressedSize)
        {
            nuint largestDict = 0;
            nuint largestCompressed = 0;
            byte* customDictContentEnd = customDictContent + dictContentSize;
            byte* largestDictbuffer = (byte*)(malloc(dictBufferCapacity));
            byte* candidateDictBuffer = (byte*)(malloc(dictBufferCapacity));
            double regressionTolerance = ((double)(@params.shrinkDictMaxRegression) / 100.0) + 1.00;

            if (largestDictbuffer == null || candidateDictBuffer == null)
            {
                free((void*)largestDictbuffer);
                free((void*)candidateDictBuffer);
                return COVER_dictSelectionError(dictContentSize);
            }

            memcpy((void*)largestDictbuffer, (void*)customDictContent, dictContentSize);
            dictContentSize = ZDICT_finalizeDictionary((void*)largestDictbuffer, dictBufferCapacity, (void*)customDictContent, dictContentSize, (void*)samplesBuffer, samplesSizes, nbFinalizeSamples, @params.zParams);
            if ((ZDICT_isError(dictContentSize)) != 0)
            {
                free((void*)largestDictbuffer);
                free((void*)candidateDictBuffer);
                return COVER_dictSelectionError(dictContentSize);
            }

            totalCompressedSize = COVER_checkTotalCompressedSize(@params, samplesSizes, samplesBuffer, offsets, nbCheckSamples, nbSamples, largestDictbuffer, dictContentSize);
            if ((ERR_isError(totalCompressedSize)) != 0)
            {
                free((void*)largestDictbuffer);
                free((void*)candidateDictBuffer);
                return COVER_dictSelectionError(totalCompressedSize);
            }

            if (@params.shrinkDict == 0)
            {
                COVER_dictSelection selection = new COVER_dictSelection
                {
                    dictContent = largestDictbuffer,
                    dictSize = dictContentSize,
                    totalCompressedSize = totalCompressedSize,
                };

                free((void*)candidateDictBuffer);
                return selection;
            }

            largestDict = dictContentSize;
            largestCompressed = totalCompressedSize;
            dictContentSize = 256;
            while (dictContentSize < largestDict)
            {
                memcpy((void*)candidateDictBuffer, (void*)largestDictbuffer, largestDict);
                dictContentSize = ZDICT_finalizeDictionary((void*)candidateDictBuffer, dictBufferCapacity, (void*)(customDictContentEnd - dictContentSize), dictContentSize, (void*)samplesBuffer, samplesSizes, nbFinalizeSamples, @params.zParams);
                if ((ZDICT_isError(dictContentSize)) != 0)
                {
                    free((void*)largestDictbuffer);
                    free((void*)candidateDictBuffer);
                    return COVER_dictSelectionError(dictContentSize);
                }

                totalCompressedSize = COVER_checkTotalCompressedSize(@params, samplesSizes, samplesBuffer, offsets, nbCheckSamples, nbSamples, candidateDictBuffer, dictContentSize);
                if ((ERR_isError(totalCompressedSize)) != 0)
                {
                    free((void*)largestDictbuffer);
                    free((void*)candidateDictBuffer);
                    return COVER_dictSelectionError(totalCompressedSize);
                }

                if (totalCompressedSize <= largestCompressed * regressionTolerance)
                {
                    COVER_dictSelection selection = new COVER_dictSelection
                    {
                        dictContent = candidateDictBuffer,
                        dictSize = dictContentSize,
                        totalCompressedSize = totalCompressedSize,
                    };

                    free((void*)largestDictbuffer);
                    return selection;
                }

                dictContentSize *= 2;
            }

            dictContentSize = largestDict;
            totalCompressedSize = largestCompressed;

            {
                COVER_dictSelection selection = new COVER_dictSelection
                {
                    dictContent = largestDictbuffer,
                    dictSize = dictContentSize,
                    totalCompressedSize = totalCompressedSize,
                };

                free((void*)candidateDictBuffer);
                return selection;
            }
        }
    }
}
