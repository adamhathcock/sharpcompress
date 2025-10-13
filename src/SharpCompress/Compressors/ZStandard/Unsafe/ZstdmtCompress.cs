using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static readonly buffer_s g_nullBuffer = new buffer_s(start: null, capacity: 0);

    private static void ZSTDMT_freeBufferPool(ZSTDMT_bufferPool_s* bufPool)
    {
        if (bufPool == null)
            return;
        if (bufPool->buffers != null)
        {
            uint u;
            for (u = 0; u < bufPool->totalBuffers; u++)
            {
                ZSTD_customFree(bufPool->buffers[u].start, bufPool->cMem);
            }

            ZSTD_customFree(bufPool->buffers, bufPool->cMem);
        }

        SynchronizationWrapper.Free(&bufPool->poolMutex);
        ZSTD_customFree(bufPool, bufPool->cMem);
    }

    private static ZSTDMT_bufferPool_s* ZSTDMT_createBufferPool(
        uint maxNbBuffers,
        ZSTD_customMem cMem
    )
    {
        ZSTDMT_bufferPool_s* bufPool = (ZSTDMT_bufferPool_s*)ZSTD_customCalloc(
            (nuint)sizeof(ZSTDMT_bufferPool_s),
            cMem
        );
        if (bufPool == null)
            return null;
        SynchronizationWrapper.Init(&bufPool->poolMutex);
        bufPool->buffers = (buffer_s*)ZSTD_customCalloc(
            maxNbBuffers * (uint)sizeof(buffer_s),
            cMem
        );
        if (bufPool->buffers == null)
        {
            ZSTDMT_freeBufferPool(bufPool);
            return null;
        }

        bufPool->bufferSize = 64 * (1 << 10);
        bufPool->totalBuffers = maxNbBuffers;
        bufPool->nbBuffers = 0;
        bufPool->cMem = cMem;
        return bufPool;
    }

    /* only works at initialization, not during compression */
    private static nuint ZSTDMT_sizeof_bufferPool(ZSTDMT_bufferPool_s* bufPool)
    {
        nuint poolSize = (nuint)sizeof(ZSTDMT_bufferPool_s);
        nuint arraySize = bufPool->totalBuffers * (uint)sizeof(buffer_s);
        uint u;
        nuint totalBufferSize = 0;
        SynchronizationWrapper.Enter(&bufPool->poolMutex);
        for (u = 0; u < bufPool->totalBuffers; u++)
            totalBufferSize += bufPool->buffers[u].capacity;
        SynchronizationWrapper.Exit(&bufPool->poolMutex);
        return poolSize + arraySize + totalBufferSize;
    }

    /* ZSTDMT_setBufferSize() :
     * all future buffers provided by this buffer pool will have _at least_ this size
     * note : it's better for all buffers to have same size,
     * as they become freely interchangeable, reducing malloc/free usages and memory fragmentation */
    private static void ZSTDMT_setBufferSize(ZSTDMT_bufferPool_s* bufPool, nuint bSize)
    {
        SynchronizationWrapper.Enter(&bufPool->poolMutex);
        bufPool->bufferSize = bSize;
        SynchronizationWrapper.Exit(&bufPool->poolMutex);
    }

    private static ZSTDMT_bufferPool_s* ZSTDMT_expandBufferPool(
        ZSTDMT_bufferPool_s* srcBufPool,
        uint maxNbBuffers
    )
    {
        if (srcBufPool == null)
            return null;
        if (srcBufPool->totalBuffers >= maxNbBuffers)
            return srcBufPool;
        {
            ZSTD_customMem cMem = srcBufPool->cMem;
            /* forward parameters */
            nuint bSize = srcBufPool->bufferSize;
            ZSTDMT_bufferPool_s* newBufPool;
            ZSTDMT_freeBufferPool(srcBufPool);
            newBufPool = ZSTDMT_createBufferPool(maxNbBuffers, cMem);
            if (newBufPool == null)
                return newBufPool;
            ZSTDMT_setBufferSize(newBufPool, bSize);
            return newBufPool;
        }
    }

    /** ZSTDMT_getBuffer() :
     *  assumption : bufPool must be valid
     * @return : a buffer, with start pointer and size
     *  note: allocation may fail, in this case, start==NULL and size==0 */
    private static buffer_s ZSTDMT_getBuffer(ZSTDMT_bufferPool_s* bufPool)
    {
        nuint bSize = bufPool->bufferSize;
        SynchronizationWrapper.Enter(&bufPool->poolMutex);
        if (bufPool->nbBuffers != 0)
        {
            buffer_s buf = bufPool->buffers[--bufPool->nbBuffers];
            nuint availBufferSize = buf.capacity;
            bufPool->buffers[bufPool->nbBuffers] = g_nullBuffer;
            if (availBufferSize >= bSize && availBufferSize >> 3 <= bSize)
            {
                SynchronizationWrapper.Exit(&bufPool->poolMutex);
                return buf;
            }

            ZSTD_customFree(buf.start, bufPool->cMem);
        }

        SynchronizationWrapper.Exit(&bufPool->poolMutex);
        {
            buffer_s buffer;
            void* start = ZSTD_customMalloc(bSize, bufPool->cMem);
            buffer.start = start;
            buffer.capacity = start == null ? 0 : bSize;
            return buffer;
        }
    }

    /* store buffer for later re-use, up to pool capacity */
    private static void ZSTDMT_releaseBuffer(ZSTDMT_bufferPool_s* bufPool, buffer_s buf)
    {
        if (buf.start == null)
            return;
        SynchronizationWrapper.Enter(&bufPool->poolMutex);
        if (bufPool->nbBuffers < bufPool->totalBuffers)
        {
            bufPool->buffers[bufPool->nbBuffers++] = buf;
            SynchronizationWrapper.Exit(&bufPool->poolMutex);
            return;
        }

        SynchronizationWrapper.Exit(&bufPool->poolMutex);
        ZSTD_customFree(buf.start, bufPool->cMem);
    }

    private static nuint ZSTDMT_sizeof_seqPool(ZSTDMT_bufferPool_s* seqPool)
    {
        return ZSTDMT_sizeof_bufferPool(seqPool);
    }

    private static RawSeqStore_t bufferToSeq(buffer_s buffer)
    {
        RawSeqStore_t seq = kNullRawSeqStore;
        seq.seq = (rawSeq*)buffer.start;
        seq.capacity = buffer.capacity / (nuint)sizeof(rawSeq);
        return seq;
    }

    private static buffer_s seqToBuffer(RawSeqStore_t seq)
    {
        buffer_s buffer;
        buffer.start = seq.seq;
        buffer.capacity = seq.capacity * (nuint)sizeof(rawSeq);
        return buffer;
    }

    private static RawSeqStore_t ZSTDMT_getSeq(ZSTDMT_bufferPool_s* seqPool)
    {
        if (seqPool->bufferSize == 0)
        {
            return kNullRawSeqStore;
        }

        return bufferToSeq(ZSTDMT_getBuffer(seqPool));
    }

    private static void ZSTDMT_releaseSeq(ZSTDMT_bufferPool_s* seqPool, RawSeqStore_t seq)
    {
        ZSTDMT_releaseBuffer(seqPool, seqToBuffer(seq));
    }

    private static void ZSTDMT_setNbSeq(ZSTDMT_bufferPool_s* seqPool, nuint nbSeq)
    {
        ZSTDMT_setBufferSize(seqPool, nbSeq * (nuint)sizeof(rawSeq));
    }

    private static ZSTDMT_bufferPool_s* ZSTDMT_createSeqPool(uint nbWorkers, ZSTD_customMem cMem)
    {
        ZSTDMT_bufferPool_s* seqPool = ZSTDMT_createBufferPool(nbWorkers, cMem);
        if (seqPool == null)
            return null;
        ZSTDMT_setNbSeq(seqPool, 0);
        return seqPool;
    }

    private static void ZSTDMT_freeSeqPool(ZSTDMT_bufferPool_s* seqPool)
    {
        ZSTDMT_freeBufferPool(seqPool);
    }

    private static ZSTDMT_bufferPool_s* ZSTDMT_expandSeqPool(
        ZSTDMT_bufferPool_s* pool,
        uint nbWorkers
    )
    {
        return ZSTDMT_expandBufferPool(pool, nbWorkers);
    }

    /* note : all CCtx borrowed from the pool must be reverted back to the pool _before_ freeing the pool */
    private static void ZSTDMT_freeCCtxPool(ZSTDMT_CCtxPool* pool)
    {
        if (pool == null)
            return;
        SynchronizationWrapper.Free(&pool->poolMutex);
        if (pool->cctxs != null)
        {
            int cid;
            for (cid = 0; cid < pool->totalCCtx; cid++)
                ZSTD_freeCCtx(pool->cctxs[cid]);
            ZSTD_customFree(pool->cctxs, pool->cMem);
        }

        ZSTD_customFree(pool, pool->cMem);
    }

    /* ZSTDMT_createCCtxPool() :
     * implies nbWorkers >= 1 , checked by caller ZSTDMT_createCCtx() */
    private static ZSTDMT_CCtxPool* ZSTDMT_createCCtxPool(int nbWorkers, ZSTD_customMem cMem)
    {
        ZSTDMT_CCtxPool* cctxPool = (ZSTDMT_CCtxPool*)ZSTD_customCalloc(
            (nuint)sizeof(ZSTDMT_CCtxPool),
            cMem
        );
        assert(nbWorkers > 0);
        if (cctxPool == null)
            return null;
        SynchronizationWrapper.Init(&cctxPool->poolMutex);
        cctxPool->totalCCtx = nbWorkers;
        cctxPool->cctxs = (ZSTD_CCtx_s**)ZSTD_customCalloc(
            (nuint)(nbWorkers * sizeof(ZSTD_CCtx_s*)),
            cMem
        );
        if (cctxPool->cctxs == null)
        {
            ZSTDMT_freeCCtxPool(cctxPool);
            return null;
        }

        cctxPool->cMem = cMem;
        cctxPool->cctxs[0] = ZSTD_createCCtx_advanced(cMem);
        if (cctxPool->cctxs[0] == null)
        {
            ZSTDMT_freeCCtxPool(cctxPool);
            return null;
        }

        cctxPool->availCCtx = 1;
        return cctxPool;
    }

    private static ZSTDMT_CCtxPool* ZSTDMT_expandCCtxPool(ZSTDMT_CCtxPool* srcPool, int nbWorkers)
    {
        if (srcPool == null)
            return null;
        if (nbWorkers <= srcPool->totalCCtx)
            return srcPool;
        {
            ZSTD_customMem cMem = srcPool->cMem;
            ZSTDMT_freeCCtxPool(srcPool);
            return ZSTDMT_createCCtxPool(nbWorkers, cMem);
        }
    }

    /* only works during initialization phase, not during compression */
    private static nuint ZSTDMT_sizeof_CCtxPool(ZSTDMT_CCtxPool* cctxPool)
    {
        SynchronizationWrapper.Enter(&cctxPool->poolMutex);
        {
            uint nbWorkers = (uint)cctxPool->totalCCtx;
            nuint poolSize = (nuint)sizeof(ZSTDMT_CCtxPool);
            nuint arraySize = (nuint)(cctxPool->totalCCtx * sizeof(ZSTD_CCtx_s*));
            nuint totalCCtxSize = 0;
            uint u;
            for (u = 0; u < nbWorkers; u++)
            {
                totalCCtxSize += ZSTD_sizeof_CCtx(cctxPool->cctxs[u]);
            }

            SynchronizationWrapper.Exit(&cctxPool->poolMutex);
            assert(nbWorkers > 0);
            return poolSize + arraySize + totalCCtxSize;
        }
    }

    private static ZSTD_CCtx_s* ZSTDMT_getCCtx(ZSTDMT_CCtxPool* cctxPool)
    {
        SynchronizationWrapper.Enter(&cctxPool->poolMutex);
        if (cctxPool->availCCtx != 0)
        {
            cctxPool->availCCtx--;
            {
                ZSTD_CCtx_s* cctx = cctxPool->cctxs[cctxPool->availCCtx];
                SynchronizationWrapper.Exit(&cctxPool->poolMutex);
                return cctx;
            }
        }

        SynchronizationWrapper.Exit(&cctxPool->poolMutex);
        return ZSTD_createCCtx_advanced(cctxPool->cMem);
    }

    private static void ZSTDMT_releaseCCtx(ZSTDMT_CCtxPool* pool, ZSTD_CCtx_s* cctx)
    {
        if (cctx == null)
            return;
        SynchronizationWrapper.Enter(&pool->poolMutex);
        if (pool->availCCtx < pool->totalCCtx)
            pool->cctxs[pool->availCCtx++] = cctx;
        else
        {
            ZSTD_freeCCtx(cctx);
        }

        SynchronizationWrapper.Exit(&pool->poolMutex);
    }

    private static int ZSTDMT_serialState_reset(
        SerialState* serialState,
        ZSTDMT_bufferPool_s* seqPool,
        ZSTD_CCtx_params_s @params,
        nuint jobSize,
        void* dict,
        nuint dictSize,
        ZSTD_dictContentType_e dictContentType
    )
    {
        if (@params.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            ZSTD_ldm_adjustParameters(&@params.ldmParams, &@params.cParams);
            assert(@params.ldmParams.hashLog >= @params.ldmParams.bucketSizeLog);
            assert(@params.ldmParams.hashRateLog < 32);
        }
        else
        {
            @params.ldmParams = new ldmParams_t();
        }

        serialState->nextJobID = 0;
        if (@params.fParams.checksumFlag != 0)
            ZSTD_XXH64_reset(&serialState->xxhState, 0);
        if (@params.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            ZSTD_customMem cMem = @params.customMem;
            uint hashLog = @params.ldmParams.hashLog;
            nuint hashSize = ((nuint)1 << (int)hashLog) * (nuint)sizeof(ldmEntry_t);
            uint bucketLog = @params.ldmParams.hashLog - @params.ldmParams.bucketSizeLog;
            uint prevBucketLog =
                serialState->@params.ldmParams.hashLog
                - serialState->@params.ldmParams.bucketSizeLog;
            nuint numBuckets = (nuint)1 << (int)bucketLog;
            ZSTDMT_setNbSeq(seqPool, ZSTD_ldm_getMaxNbSeq(@params.ldmParams, jobSize));
            ZSTD_window_init(&serialState->ldmState.window);
            if (
                serialState->ldmState.hashTable == null
                || serialState->@params.ldmParams.hashLog < hashLog
            )
            {
                ZSTD_customFree(serialState->ldmState.hashTable, cMem);
                serialState->ldmState.hashTable = (ldmEntry_t*)ZSTD_customMalloc(hashSize, cMem);
            }

            if (serialState->ldmState.bucketOffsets == null || prevBucketLog < bucketLog)
            {
                ZSTD_customFree(serialState->ldmState.bucketOffsets, cMem);
                serialState->ldmState.bucketOffsets = (byte*)ZSTD_customMalloc(numBuckets, cMem);
            }

            if (
                serialState->ldmState.hashTable == null
                || serialState->ldmState.bucketOffsets == null
            )
                return 1;
            memset(serialState->ldmState.hashTable, 0, (uint)hashSize);
            memset(serialState->ldmState.bucketOffsets, 0, (uint)numBuckets);
            serialState->ldmState.loadedDictEnd = 0;
            if (dictSize > 0)
            {
                if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_rawContent)
                {
                    byte* dictEnd = (byte*)dict + dictSize;
                    ZSTD_window_update(&serialState->ldmState.window, dict, dictSize, 0);
                    ZSTD_ldm_fillHashTable(
                        &serialState->ldmState,
                        (byte*)dict,
                        dictEnd,
                        &@params.ldmParams
                    );
                    serialState->ldmState.loadedDictEnd =
                        @params.forceWindow != 0
                            ? 0
                            : (uint)(dictEnd - serialState->ldmState.window.@base);
                }
            }

            serialState->ldmWindow = serialState->ldmState.window;
        }

        serialState->@params = @params;
        serialState->@params.jobSize = (uint)jobSize;
        return 0;
    }

    private static int ZSTDMT_serialState_init(SerialState* serialState)
    {
        int initError = 0;
        *serialState = new SerialState();
        SynchronizationWrapper.Init(&serialState->mutex);
        initError |= 0;
        initError |= 0;
        SynchronizationWrapper.Init(&serialState->ldmWindowMutex);
        initError |= 0;
        initError |= 0;
        return initError;
    }

    private static void ZSTDMT_serialState_free(SerialState* serialState)
    {
        ZSTD_customMem cMem = serialState->@params.customMem;
        SynchronizationWrapper.Free(&serialState->mutex);
        SynchronizationWrapper.Free(&serialState->ldmWindowMutex);
        ZSTD_customFree(serialState->ldmState.hashTable, cMem);
        ZSTD_customFree(serialState->ldmState.bucketOffsets, cMem);
    }

    private static void ZSTDMT_serialState_genSequences(
        SerialState* serialState,
        RawSeqStore_t* seqStore,
        Range src,
        uint jobID
    )
    {
        SynchronizationWrapper.Enter(&serialState->mutex);
        while (serialState->nextJobID < jobID)
        {
            SynchronizationWrapper.Wait(&serialState->mutex);
        }

        if (serialState->nextJobID == jobID)
        {
            if (serialState->@params.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
            {
                nuint error;
                assert(
                    seqStore->seq != null
                        && seqStore->pos == 0
                        && seqStore->size == 0
                        && seqStore->capacity > 0
                );
                assert(src.size <= serialState->@params.jobSize);
                ZSTD_window_update(&serialState->ldmState.window, src.start, src.size, 0);
                error = ZSTD_ldm_generateSequences(
                    &serialState->ldmState,
                    seqStore,
                    &serialState->@params.ldmParams,
                    src.start,
                    src.size
                );
                assert(!ERR_isError(error));
                SynchronizationWrapper.Enter(&serialState->ldmWindowMutex);
                serialState->ldmWindow = serialState->ldmState.window;
                SynchronizationWrapper.Pulse(&serialState->ldmWindowMutex);
                SynchronizationWrapper.Exit(&serialState->ldmWindowMutex);
            }

            if (serialState->@params.fParams.checksumFlag != 0 && src.size > 0)
                ZSTD_XXH64_update(&serialState->xxhState, src.start, src.size);
        }

        serialState->nextJobID++;
        SynchronizationWrapper.PulseAll(&serialState->mutex);
        SynchronizationWrapper.Exit(&serialState->mutex);
    }

    private static void ZSTDMT_serialState_applySequences(
        SerialState* serialState,
        ZSTD_CCtx_s* jobCCtx,
        RawSeqStore_t* seqStore
    )
    {
        if (seqStore->size > 0)
        {
            assert(serialState->@params.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable);
            assert(jobCCtx != null);
            ZSTD_referenceExternalSequences(jobCCtx, seqStore->seq, seqStore->size);
        }
    }

    private static void ZSTDMT_serialState_ensureFinished(
        SerialState* serialState,
        uint jobID,
        nuint cSize
    )
    {
        SynchronizationWrapper.Enter(&serialState->mutex);
        if (serialState->nextJobID <= jobID)
        {
            assert(ERR_isError(cSize));
            serialState->nextJobID = jobID + 1;
            SynchronizationWrapper.PulseAll(&serialState->mutex);
            SynchronizationWrapper.Enter(&serialState->ldmWindowMutex);
            ZSTD_window_clear(&serialState->ldmWindow);
            SynchronizationWrapper.Pulse(&serialState->ldmWindowMutex);
            SynchronizationWrapper.Exit(&serialState->ldmWindowMutex);
        }

        SynchronizationWrapper.Exit(&serialState->mutex);
    }

    private static readonly Range kNullRange = new Range(start: null, size: 0);

    /* ZSTDMT_compressionJob() is a POOL_function type */
    private static void ZSTDMT_compressionJob(void* jobDescription)
    {
        ZSTDMT_jobDescription* job = (ZSTDMT_jobDescription*)jobDescription;
        /* do not modify job->params ! copy it, modify the copy */
        ZSTD_CCtx_params_s jobParams = job->@params;
        ZSTD_CCtx_s* cctx = ZSTDMT_getCCtx(job->cctxPool);
        RawSeqStore_t rawSeqStore = ZSTDMT_getSeq(job->seqPool);
        buffer_s dstBuff = job->dstBuff;
        nuint lastCBlockSize = 0;
        if (cctx == null)
        {
            SynchronizationWrapper.Enter(&job->job_mutex);
            job->cSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            SynchronizationWrapper.Exit(&job->job_mutex);
            goto _endJob;
        }

        if (dstBuff.start == null)
        {
            dstBuff = ZSTDMT_getBuffer(job->bufPool);
            if (dstBuff.start == null)
            {
                SynchronizationWrapper.Enter(&job->job_mutex);
                job->cSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
                SynchronizationWrapper.Exit(&job->job_mutex);
                goto _endJob;
            }

            job->dstBuff = dstBuff;
        }

        if (
            jobParams.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable
            && rawSeqStore.seq == null
        )
        {
            SynchronizationWrapper.Enter(&job->job_mutex);
            job->cSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            SynchronizationWrapper.Exit(&job->job_mutex);
            goto _endJob;
        }

        if (job->jobID != 0)
            jobParams.fParams.checksumFlag = 0;
        jobParams.ldmParams.enableLdm = ZSTD_paramSwitch_e.ZSTD_ps_disable;
        jobParams.nbWorkers = 0;
        ZSTDMT_serialState_genSequences(job->serial, &rawSeqStore, job->src, job->jobID);
        if (job->cdict != null)
        {
            nuint initError = ZSTD_compressBegin_advanced_internal(
                cctx,
                null,
                0,
                ZSTD_dictContentType_e.ZSTD_dct_auto,
                ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
                job->cdict,
                &jobParams,
                job->fullFrameSize
            );
            assert(job->firstJob != 0);
            if (ERR_isError(initError))
            {
                SynchronizationWrapper.Enter(&job->job_mutex);
                job->cSize = initError;
                SynchronizationWrapper.Exit(&job->job_mutex);
                goto _endJob;
            }
        }
        else
        {
            ulong pledgedSrcSize = job->firstJob != 0 ? job->fullFrameSize : job->src.size;
            {
                nuint forceWindowError = ZSTD_CCtxParams_setParameter(
                    &jobParams,
                    ZSTD_cParameter.ZSTD_c_experimentalParam3,
                    job->firstJob == 0 ? 1 : 0
                );
                if (ERR_isError(forceWindowError))
                {
                    SynchronizationWrapper.Enter(&job->job_mutex);
                    job->cSize = forceWindowError;
                    SynchronizationWrapper.Exit(&job->job_mutex);
                    goto _endJob;
                }
            }

            if (job->firstJob == 0)
            {
                nuint err = ZSTD_CCtxParams_setParameter(
                    &jobParams,
                    ZSTD_cParameter.ZSTD_c_experimentalParam15,
                    0
                );
                if (ERR_isError(err))
                {
                    SynchronizationWrapper.Enter(&job->job_mutex);
                    job->cSize = err;
                    SynchronizationWrapper.Exit(&job->job_mutex);
                    goto _endJob;
                }
            }

            {
                nuint initError = ZSTD_compressBegin_advanced_internal(
                    cctx,
                    job->prefix.start,
                    job->prefix.size,
                    ZSTD_dictContentType_e.ZSTD_dct_rawContent,
                    ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
                    null,
                    &jobParams,
                    pledgedSrcSize
                );
                if (ERR_isError(initError))
                {
                    SynchronizationWrapper.Enter(&job->job_mutex);
                    job->cSize = initError;
                    SynchronizationWrapper.Exit(&job->job_mutex);
                    goto _endJob;
                }
            }
        }

        ZSTDMT_serialState_applySequences(job->serial, cctx, &rawSeqStore);
        if (job->firstJob == 0)
        {
            nuint hSize = ZSTD_compressContinue_public(
                cctx,
                dstBuff.start,
                dstBuff.capacity,
                job->src.start,
                0
            );
            if (ERR_isError(hSize))
            {
                SynchronizationWrapper.Enter(&job->job_mutex);
                job->cSize = hSize;
                SynchronizationWrapper.Exit(&job->job_mutex);
                goto _endJob;
            }

            ZSTD_invalidateRepCodes(cctx);
        }

        {
            const nuint chunkSize = 4 * (1 << 17);
            int nbChunks = (int)((job->src.size + (chunkSize - 1)) / chunkSize);
            byte* ip = (byte*)job->src.start;
            byte* ostart = (byte*)dstBuff.start;
            byte* op = ostart;
            byte* oend = op + dstBuff.capacity;
            int chunkNb;
#if DEBUG
            if (sizeof(nuint) > sizeof(int))
                assert(job->src.size < unchecked(2147483647 * chunkSize));
#endif
            assert(job->cSize == 0);
            for (chunkNb = 1; chunkNb < nbChunks; chunkNb++)
            {
                nuint cSize = ZSTD_compressContinue_public(
                    cctx,
                    op,
                    (nuint)(oend - op),
                    ip,
                    chunkSize
                );
                if (ERR_isError(cSize))
                {
                    SynchronizationWrapper.Enter(&job->job_mutex);
                    job->cSize = cSize;
                    SynchronizationWrapper.Exit(&job->job_mutex);
                    goto _endJob;
                }

                ip += chunkSize;
                op += cSize;
                assert(op < oend);
                SynchronizationWrapper.Enter(&job->job_mutex);
                job->cSize += cSize;
                job->consumed = chunkSize * (nuint)chunkNb;
                SynchronizationWrapper.Pulse(&job->job_mutex);
                SynchronizationWrapper.Exit(&job->job_mutex);
            }

            assert(chunkSize > 0);
            assert((chunkSize & chunkSize - 1) == 0);
            if (((uint)(nbChunks > 0 ? 1 : 0) | job->lastJob) != 0)
            {
                nuint lastBlockSize1 = job->src.size & chunkSize - 1;
                nuint lastBlockSize =
                    lastBlockSize1 == 0 && job->src.size >= chunkSize ? chunkSize : lastBlockSize1;
                nuint cSize =
                    job->lastJob != 0
                        ? ZSTD_compressEnd_public(cctx, op, (nuint)(oend - op), ip, lastBlockSize)
                        : ZSTD_compressContinue_public(
                            cctx,
                            op,
                            (nuint)(oend - op),
                            ip,
                            lastBlockSize
                        );
                if (ERR_isError(cSize))
                {
                    SynchronizationWrapper.Enter(&job->job_mutex);
                    job->cSize = cSize;
                    SynchronizationWrapper.Exit(&job->job_mutex);
                    goto _endJob;
                }

                lastCBlockSize = cSize;
            }
        }

#if DEBUG
        if (job->firstJob == 0)
        {
            assert(ZSTD_window_hasExtDict(cctx->blockState.matchState.window) == 0);
        }
#endif

        ZSTD_CCtx_trace(cctx, 0);
        _endJob:
        ZSTDMT_serialState_ensureFinished(job->serial, job->jobID, job->cSize);
        ZSTDMT_releaseSeq(job->seqPool, rawSeqStore);
        ZSTDMT_releaseCCtx(job->cctxPool, cctx);
        SynchronizationWrapper.Enter(&job->job_mutex);
        if (ERR_isError(job->cSize))
            assert(lastCBlockSize == 0);
        job->cSize += lastCBlockSize;
        job->consumed = job->src.size;
        SynchronizationWrapper.Pulse(&job->job_mutex);
        SynchronizationWrapper.Exit(&job->job_mutex);
    }

    private static readonly RoundBuff_t kNullRoundBuff = new RoundBuff_t(
        buffer: null,
        capacity: 0,
        pos: 0
    );

    private static void ZSTDMT_freeJobsTable(
        ZSTDMT_jobDescription* jobTable,
        uint nbJobs,
        ZSTD_customMem cMem
    )
    {
        uint jobNb;
        if (jobTable == null)
            return;
        for (jobNb = 0; jobNb < nbJobs; jobNb++)
        {
            SynchronizationWrapper.Free(&jobTable[jobNb].job_mutex);
        }

        ZSTD_customFree(jobTable, cMem);
    }

    /* ZSTDMT_allocJobsTable()
     * allocate and init a job table.
     * update *nbJobsPtr to next power of 2 value, as size of table */
    private static ZSTDMT_jobDescription* ZSTDMT_createJobsTable(
        uint* nbJobsPtr,
        ZSTD_customMem cMem
    )
    {
        uint nbJobsLog2 = ZSTD_highbit32(*nbJobsPtr) + 1;
        uint nbJobs = (uint)(1 << (int)nbJobsLog2);
        uint jobNb;
        ZSTDMT_jobDescription* jobTable = (ZSTDMT_jobDescription*)ZSTD_customCalloc(
            nbJobs * (uint)sizeof(ZSTDMT_jobDescription),
            cMem
        );
        int initError = 0;
        if (jobTable == null)
            return null;
        *nbJobsPtr = nbJobs;
        for (jobNb = 0; jobNb < nbJobs; jobNb++)
        {
            SynchronizationWrapper.Init(&jobTable[jobNb].job_mutex);
            initError |= 0;
            initError |= 0;
        }

        if (initError != 0)
        {
            ZSTDMT_freeJobsTable(jobTable, nbJobs, cMem);
            return null;
        }

        return jobTable;
    }

    private static nuint ZSTDMT_expandJobsTable(ZSTDMT_CCtx_s* mtctx, uint nbWorkers)
    {
        uint nbJobs = nbWorkers + 2;
        if (nbJobs > mtctx->jobIDMask + 1)
        {
            ZSTDMT_freeJobsTable(mtctx->jobs, mtctx->jobIDMask + 1, mtctx->cMem);
            mtctx->jobIDMask = 0;
            mtctx->jobs = ZSTDMT_createJobsTable(&nbJobs, mtctx->cMem);
            if (mtctx->jobs == null)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            assert(nbJobs != 0 && (nbJobs & nbJobs - 1) == 0);
            mtctx->jobIDMask = nbJobs - 1;
        }

        return 0;
    }

    /* ZSTDMT_CCtxParam_setNbWorkers():
     * Internal use only */
    private static nuint ZSTDMT_CCtxParam_setNbWorkers(ZSTD_CCtx_params_s* @params, uint nbWorkers)
    {
        return ZSTD_CCtxParams_setParameter(
            @params,
            ZSTD_cParameter.ZSTD_c_nbWorkers,
            (int)nbWorkers
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ZSTDMT_CCtx_s* ZSTDMT_createCCtx_advanced_internal(
        uint nbWorkers,
        ZSTD_customMem cMem,
        void* pool
    )
    {
        ZSTDMT_CCtx_s* mtctx;
        uint nbJobs = nbWorkers + 2;
        int initError;
        if (nbWorkers < 1)
            return null;
        nbWorkers =
            nbWorkers < (uint)(sizeof(void*) == 4 ? 64 : 256)
                ? nbWorkers
                : (uint)(sizeof(void*) == 4 ? 64 : 256);
        if (((cMem.customAlloc != null ? 1 : 0) ^ (cMem.customFree != null ? 1 : 0)) != 0)
            return null;
        mtctx = (ZSTDMT_CCtx_s*)ZSTD_customCalloc((nuint)sizeof(ZSTDMT_CCtx_s), cMem);
        if (mtctx == null)
            return null;
        ZSTDMT_CCtxParam_setNbWorkers(&mtctx->@params, nbWorkers);
        mtctx->cMem = cMem;
        mtctx->allJobsCompleted = 1;
        if (pool != null)
        {
            mtctx->factory = pool;
            mtctx->providedFactory = 1;
        }
        else
        {
            mtctx->factory = POOL_create_advanced(nbWorkers, 0, cMem);
            mtctx->providedFactory = 0;
        }

        mtctx->jobs = ZSTDMT_createJobsTable(&nbJobs, cMem);
        assert(nbJobs > 0);
        assert((nbJobs & nbJobs - 1) == 0);
        mtctx->jobIDMask = nbJobs - 1;
        mtctx->bufPool = ZSTDMT_createBufferPool(2 * nbWorkers + 3, cMem);
        mtctx->cctxPool = ZSTDMT_createCCtxPool((int)nbWorkers, cMem);
        mtctx->seqPool = ZSTDMT_createSeqPool(nbWorkers, cMem);
        initError = ZSTDMT_serialState_init(&mtctx->serial);
        mtctx->roundBuff = kNullRoundBuff;
        if (
            (
                (
                    mtctx->factory == null
                    || mtctx->jobs == null
                    || mtctx->bufPool == null
                    || mtctx->cctxPool == null
                    || mtctx->seqPool == null
                        ? 1
                        : 0
                ) | initError
            ) != 0
        )
        {
            ZSTDMT_freeCCtx(mtctx);
            return null;
        }

        return mtctx;
    }

    /* Requires ZSTD_MULTITHREAD to be defined during compilation, otherwise it will return NULL. */
    private static ZSTDMT_CCtx_s* ZSTDMT_createCCtx_advanced(
        uint nbWorkers,
        ZSTD_customMem cMem,
        void* pool
    )
    {
        return ZSTDMT_createCCtx_advanced_internal(nbWorkers, cMem, pool);
    }

    /* ZSTDMT_releaseAllJobResources() :
     * note : ensure all workers are killed first ! */
    private static void ZSTDMT_releaseAllJobResources(ZSTDMT_CCtx_s* mtctx)
    {
        uint jobID;
        for (jobID = 0; jobID <= mtctx->jobIDMask; jobID++)
        {
            /* Copy the mutex/cond out */
            void* mutex = mtctx->jobs[jobID].job_mutex;
            void* cond = mtctx->jobs[jobID].job_cond;
            ZSTDMT_releaseBuffer(mtctx->bufPool, mtctx->jobs[jobID].dstBuff);
            mtctx->jobs[jobID] = new ZSTDMT_jobDescription { job_mutex = mutex, job_cond = cond };
        }

        mtctx->inBuff.buffer = g_nullBuffer;
        mtctx->inBuff.filled = 0;
        mtctx->allJobsCompleted = 1;
    }

    private static void ZSTDMT_waitForAllJobsCompleted(ZSTDMT_CCtx_s* mtctx)
    {
        while (mtctx->doneJobID < mtctx->nextJobID)
        {
            uint jobID = mtctx->doneJobID & mtctx->jobIDMask;
            SynchronizationWrapper.Enter(&mtctx->jobs[jobID].job_mutex);
            while (mtctx->jobs[jobID].consumed < mtctx->jobs[jobID].src.size)
            {
                SynchronizationWrapper.Wait(&mtctx->jobs[jobID].job_mutex);
            }

            SynchronizationWrapper.Exit(&mtctx->jobs[jobID].job_mutex);
            mtctx->doneJobID++;
        }
    }

    private static nuint ZSTDMT_freeCCtx(ZSTDMT_CCtx_s* mtctx)
    {
        if (mtctx == null)
            return 0;
        if (mtctx->providedFactory == 0)
            POOL_free(mtctx->factory);
        ZSTDMT_releaseAllJobResources(mtctx);
        ZSTDMT_freeJobsTable(mtctx->jobs, mtctx->jobIDMask + 1, mtctx->cMem);
        ZSTDMT_freeBufferPool(mtctx->bufPool);
        ZSTDMT_freeCCtxPool(mtctx->cctxPool);
        ZSTDMT_freeSeqPool(mtctx->seqPool);
        ZSTDMT_serialState_free(&mtctx->serial);
        ZSTD_freeCDict(mtctx->cdictLocal);
        if (mtctx->roundBuff.buffer != null)
            ZSTD_customFree(mtctx->roundBuff.buffer, mtctx->cMem);
        ZSTD_customFree(mtctx, mtctx->cMem);
        return 0;
    }

    private static nuint ZSTDMT_sizeof_CCtx(ZSTDMT_CCtx_s* mtctx)
    {
        if (mtctx == null)
            return 0;
        return (nuint)sizeof(ZSTDMT_CCtx_s)
            + POOL_sizeof(mtctx->factory)
            + ZSTDMT_sizeof_bufferPool(mtctx->bufPool)
            + (mtctx->jobIDMask + 1) * (uint)sizeof(ZSTDMT_jobDescription)
            + ZSTDMT_sizeof_CCtxPool(mtctx->cctxPool)
            + ZSTDMT_sizeof_seqPool(mtctx->seqPool)
            + ZSTD_sizeof_CDict(mtctx->cdictLocal)
            + mtctx->roundBuff.capacity;
    }

    /* ZSTDMT_resize() :
     * @return : error code if fails, 0 on success */
    private static nuint ZSTDMT_resize(ZSTDMT_CCtx_s* mtctx, uint nbWorkers)
    {
        if (POOL_resize(mtctx->factory, nbWorkers) != 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        {
            nuint err_code = ZSTDMT_expandJobsTable(mtctx, nbWorkers);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        mtctx->bufPool = ZSTDMT_expandBufferPool(mtctx->bufPool, 2 * nbWorkers + 3);
        if (mtctx->bufPool == null)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        mtctx->cctxPool = ZSTDMT_expandCCtxPool(mtctx->cctxPool, (int)nbWorkers);
        if (mtctx->cctxPool == null)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        mtctx->seqPool = ZSTDMT_expandSeqPool(mtctx->seqPool, nbWorkers);
        if (mtctx->seqPool == null)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        ZSTDMT_CCtxParam_setNbWorkers(&mtctx->@params, nbWorkers);
        return 0;
    }

    /*! ZSTDMT_updateCParams_whileCompressing() :
     *  Updates a selected set of compression parameters, remaining compatible with currently active frame.
     *  New parameters will be applied to next compression job. */
    private static void ZSTDMT_updateCParams_whileCompressing(
        ZSTDMT_CCtx_s* mtctx,
        ZSTD_CCtx_params_s* cctxParams
    )
    {
        /* Do not modify windowLog while compressing */
        uint saved_wlog = mtctx->@params.cParams.windowLog;
        int compressionLevel = cctxParams->compressionLevel;
        mtctx->@params.compressionLevel = compressionLevel;
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParamsFromCCtxParams(
                cctxParams,
                unchecked(0UL - 1),
                0,
                ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
            );
            cParams.windowLog = saved_wlog;
            mtctx->@params.cParams = cParams;
        }
    }

    /* ZSTDMT_getFrameProgression():
     * tells how much data has been consumed (input) and produced (output) for current frame.
     * able to count progression inside worker threads.
     * Note : mutex will be acquired during statistics collection inside workers. */
    private static ZSTD_frameProgression ZSTDMT_getFrameProgression(ZSTDMT_CCtx_s* mtctx)
    {
        ZSTD_frameProgression fps;
        fps.ingested = mtctx->consumed + mtctx->inBuff.filled;
        fps.consumed = mtctx->consumed;
        fps.produced = fps.flushed = mtctx->produced;
        fps.currentJobID = mtctx->nextJobID;
        fps.nbActiveWorkers = 0;
        {
            uint jobNb;
            uint lastJobNb = mtctx->nextJobID + (uint)mtctx->jobReady;
            assert(mtctx->jobReady <= 1);
            for (jobNb = mtctx->doneJobID; jobNb < lastJobNb; jobNb++)
            {
                uint wJobID = jobNb & mtctx->jobIDMask;
                ZSTDMT_jobDescription* jobPtr = &mtctx->jobs[wJobID];
                SynchronizationWrapper.Enter(&jobPtr->job_mutex);
                {
                    nuint cResult = jobPtr->cSize;
                    nuint produced = ERR_isError(cResult) ? 0 : cResult;
                    nuint flushed = ERR_isError(cResult) ? 0 : jobPtr->dstFlushed;
                    assert(flushed <= produced);
                    fps.ingested += jobPtr->src.size;
                    fps.consumed += jobPtr->consumed;
                    fps.produced += produced;
                    fps.flushed += flushed;
                    fps.nbActiveWorkers += jobPtr->consumed < jobPtr->src.size ? 1U : 0U;
                }

                SynchronizationWrapper.Exit(&mtctx->jobs[wJobID].job_mutex);
            }
        }

        return fps;
    }

    /*! ZSTDMT_toFlushNow()
     *  Tell how many bytes are ready to be flushed immediately.
     *  Probe the oldest active job (not yet entirely flushed) and check its output buffer.
     *  If return 0, it means there is no active job,
     *  or, it means oldest job is still active, but everything produced has been flushed so far,
     *  therefore flushing is limited by speed of oldest job. */
    private static nuint ZSTDMT_toFlushNow(ZSTDMT_CCtx_s* mtctx)
    {
        nuint toFlush;
        uint jobID = mtctx->doneJobID;
        assert(jobID <= mtctx->nextJobID);
        if (jobID == mtctx->nextJobID)
            return 0;
        {
            uint wJobID = jobID & mtctx->jobIDMask;
            ZSTDMT_jobDescription* jobPtr = &mtctx->jobs[wJobID];
            SynchronizationWrapper.Enter(&jobPtr->job_mutex);
            {
                nuint cResult = jobPtr->cSize;
                nuint produced = ERR_isError(cResult) ? 0 : cResult;
                nuint flushed = ERR_isError(cResult) ? 0 : jobPtr->dstFlushed;
                assert(flushed <= produced);
                assert(jobPtr->consumed <= jobPtr->src.size);
                toFlush = produced - flushed;
#if DEBUG
                if (toFlush == 0)
                {
                    assert(jobPtr->consumed < jobPtr->src.size);
                }
#endif
            }

            SynchronizationWrapper.Exit(&mtctx->jobs[wJobID].job_mutex);
        }

        return toFlush;
    }

    /* ------------------------------------------ */
    /* =====   Multi-threaded compression   ===== */
    /* ------------------------------------------ */
    private static uint ZSTDMT_computeTargetJobLog(ZSTD_CCtx_params_s* @params)
    {
        uint jobLog;
        if (@params->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            jobLog =
                21 > ZSTD_cycleLog(@params->cParams.chainLog, @params->cParams.strategy) + 3
                    ? 21
                    : ZSTD_cycleLog(@params->cParams.chainLog, @params->cParams.strategy) + 3;
        }
        else
        {
            jobLog = 20 > @params->cParams.windowLog + 2 ? 20 : @params->cParams.windowLog + 2;
        }

        return jobLog < (uint)(MEM_32bits ? 29 : 30) ? jobLog : (uint)(MEM_32bits ? 29 : 30);
    }

    private static int ZSTDMT_overlapLog_default(ZSTD_strategy strat)
    {
        switch (strat)
        {
            case ZSTD_strategy.ZSTD_btultra2:
                return 9;
            case ZSTD_strategy.ZSTD_btultra:
            case ZSTD_strategy.ZSTD_btopt:
                return 8;
            case ZSTD_strategy.ZSTD_btlazy2:
            case ZSTD_strategy.ZSTD_lazy2:
                return 7;
            case ZSTD_strategy.ZSTD_lazy:
            case ZSTD_strategy.ZSTD_greedy:
            case ZSTD_strategy.ZSTD_dfast:
            case ZSTD_strategy.ZSTD_fast:
            default:
                break;
        }

        return 6;
    }

    private static int ZSTDMT_overlapLog(int ovlog, ZSTD_strategy strat)
    {
        assert(0 <= ovlog && ovlog <= 9);
        if (ovlog == 0)
            return ZSTDMT_overlapLog_default(strat);
        return ovlog;
    }

    private static nuint ZSTDMT_computeOverlapSize(ZSTD_CCtx_params_s* @params)
    {
        int overlapRLog = 9 - ZSTDMT_overlapLog(@params->overlapLog, @params->cParams.strategy);
        int ovLog = (int)(overlapRLog >= 8 ? 0 : @params->cParams.windowLog - (uint)overlapRLog);
        assert(0 <= overlapRLog && overlapRLog <= 8);
        if (@params->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            ovLog = (int)(
                (
                    @params->cParams.windowLog < ZSTDMT_computeTargetJobLog(@params) - 2
                        ? @params->cParams.windowLog
                        : ZSTDMT_computeTargetJobLog(@params) - 2
                ) - (uint)overlapRLog
            );
        }

        assert(0 <= ovLog && ovLog <= (sizeof(nuint) == 4 ? 30 : 31));
        return ovLog == 0 ? 0 : (nuint)1 << ovLog;
    }

    /* ====================================== */
    /* =======      Streaming API     ======= */
    /* ====================================== */
    private static nuint ZSTDMT_initCStream_internal(
        ZSTDMT_CCtx_s* mtctx,
        void* dict,
        nuint dictSize,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s @params,
        ulong pledgedSrcSize
    )
    {
        assert(!ERR_isError(ZSTD_checkCParams(@params.cParams)));
        assert(!(dict != null && cdict != null));
        if (@params.nbWorkers != mtctx->@params.nbWorkers)
        {
            /* init */
            nuint err_code = ZSTDMT_resize(mtctx, (uint)@params.nbWorkers);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        if (@params.jobSize != 0 && @params.jobSize < 512 * (1 << 10))
            @params.jobSize = 512 * (1 << 10);
        if (@params.jobSize > (nuint)(MEM_32bits ? 512 * (1 << 20) : 1024 * (1 << 20)))
            @params.jobSize = (nuint)(MEM_32bits ? 512 * (1 << 20) : 1024 * (1 << 20));
        if (mtctx->allJobsCompleted == 0)
        {
            ZSTDMT_waitForAllJobsCompleted(mtctx);
            ZSTDMT_releaseAllJobResources(mtctx);
            mtctx->allJobsCompleted = 1;
        }

        mtctx->@params = @params;
        mtctx->frameContentSize = pledgedSrcSize;
        ZSTD_freeCDict(mtctx->cdictLocal);
        if (dict != null)
        {
            mtctx->cdictLocal = ZSTD_createCDict_advanced(
                dict,
                dictSize,
                ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy,
                dictContentType,
                @params.cParams,
                mtctx->cMem
            );
            mtctx->cdict = mtctx->cdictLocal;
            if (mtctx->cdictLocal == null)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }
        else
        {
            mtctx->cdictLocal = null;
            mtctx->cdict = cdict;
        }

        mtctx->targetPrefixSize = ZSTDMT_computeOverlapSize(&@params);
        mtctx->targetSectionSize = @params.jobSize;
        if (mtctx->targetSectionSize == 0)
        {
            mtctx->targetSectionSize = (nuint)(1UL << (int)ZSTDMT_computeTargetJobLog(&@params));
        }

        assert(
            mtctx->targetSectionSize <= (nuint)(MEM_32bits ? 512 * (1 << 20) : 1024 * (1 << 20))
        );
        if (@params.rsyncable != 0)
        {
            /* Aim for the targetsectionSize as the average job size. */
            uint jobSizeKB = (uint)(mtctx->targetSectionSize >> 10);
            assert(jobSizeKB >= 1);
            uint rsyncBits = ZSTD_highbit32(jobSizeKB) + 10;
            assert(rsyncBits >= 17 + 2);
            mtctx->rsync.hash = 0;
            mtctx->rsync.hitMask = (1UL << (int)rsyncBits) - 1;
            mtctx->rsync.primePower = ZSTD_rollingHash_primePower(32);
        }

        if (mtctx->targetSectionSize < mtctx->targetPrefixSize)
            mtctx->targetSectionSize = mtctx->targetPrefixSize;
        ZSTDMT_setBufferSize(mtctx->bufPool, ZSTD_compressBound(mtctx->targetSectionSize));
        {
            /* If ldm is enabled we need windowSize space. */
            nuint windowSize =
                mtctx->@params.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable
                    ? 1U << (int)mtctx->@params.cParams.windowLog
                    : 0;
            /* Two buffers of slack, plus extra space for the overlap
             * This is the minimum slack that LDM works with. One extra because
             * flush might waste up to targetSectionSize-1 bytes. Another extra
             * for the overlap (if > 0), then one to fill which doesn't overlap
             * with the LDM window.
             */
            nuint nbSlackBuffers = (nuint)(2 + (mtctx->targetPrefixSize > 0 ? 1 : 0));
            nuint slackSize = mtctx->targetSectionSize * nbSlackBuffers;
            /* Compute the total size, and always have enough slack */
            nuint nbWorkers = (nuint)(mtctx->@params.nbWorkers > 1 ? mtctx->@params.nbWorkers : 1);
            nuint sectionsSize = mtctx->targetSectionSize * nbWorkers;
            nuint capacity = (windowSize > sectionsSize ? windowSize : sectionsSize) + slackSize;
            if (mtctx->roundBuff.capacity < capacity)
            {
                if (mtctx->roundBuff.buffer != null)
                    ZSTD_customFree(mtctx->roundBuff.buffer, mtctx->cMem);
                mtctx->roundBuff.buffer = (byte*)ZSTD_customMalloc(capacity, mtctx->cMem);
                if (mtctx->roundBuff.buffer == null)
                {
                    mtctx->roundBuff.capacity = 0;
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
                }

                mtctx->roundBuff.capacity = capacity;
            }
        }

        mtctx->roundBuff.pos = 0;
        mtctx->inBuff.buffer = g_nullBuffer;
        mtctx->inBuff.filled = 0;
        mtctx->inBuff.prefix = kNullRange;
        mtctx->doneJobID = 0;
        mtctx->nextJobID = 0;
        mtctx->frameEnded = 0;
        mtctx->allJobsCompleted = 0;
        mtctx->consumed = 0;
        mtctx->produced = 0;
        ZSTD_freeCDict(mtctx->cdictLocal);
        mtctx->cdictLocal = null;
        mtctx->cdict = null;
        if (dict != null)
        {
            if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_rawContent)
            {
                mtctx->inBuff.prefix.start = (byte*)dict;
                mtctx->inBuff.prefix.size = dictSize;
            }
            else
            {
                mtctx->cdictLocal = ZSTD_createCDict_advanced(
                    dict,
                    dictSize,
                    ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
                    dictContentType,
                    @params.cParams,
                    mtctx->cMem
                );
                mtctx->cdict = mtctx->cdictLocal;
                if (mtctx->cdictLocal == null)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            }
        }
        else
        {
            mtctx->cdict = cdict;
        }

        if (
            ZSTDMT_serialState_reset(
                &mtctx->serial,
                mtctx->seqPool,
                @params,
                mtctx->targetSectionSize,
                dict,
                dictSize,
                dictContentType
            ) != 0
        )
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        return 0;
    }

    /* ZSTDMT_writeLastEmptyBlock()
     * Write a single empty block with an end-of-frame to finish a frame.
     * Job must be created from streaming variant.
     * This function is always successful if expected conditions are fulfilled.
     */
    private static void ZSTDMT_writeLastEmptyBlock(ZSTDMT_jobDescription* job)
    {
        assert(job->lastJob == 1);
        assert(job->src.size == 0);
        assert(job->firstJob == 0);
        assert(job->dstBuff.start == null);
        job->dstBuff = ZSTDMT_getBuffer(job->bufPool);
        if (job->dstBuff.start == null)
        {
            job->cSize = unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            return;
        }

        assert(job->dstBuff.capacity >= ZSTD_blockHeaderSize);
        job->src = kNullRange;
        job->cSize = ZSTD_writeLastEmptyBlock(job->dstBuff.start, job->dstBuff.capacity);
        assert(!ERR_isError(job->cSize));
        assert(job->consumed == 0);
    }

    private static nuint ZSTDMT_createCompressionJob(
        ZSTDMT_CCtx_s* mtctx,
        nuint srcSize,
        ZSTD_EndDirective endOp
    )
    {
        uint jobID = mtctx->nextJobID & mtctx->jobIDMask;
        int endFrame = endOp == ZSTD_EndDirective.ZSTD_e_end ? 1 : 0;
        if (mtctx->nextJobID > mtctx->doneJobID + mtctx->jobIDMask)
        {
            assert((mtctx->nextJobID & mtctx->jobIDMask) == (mtctx->doneJobID & mtctx->jobIDMask));
            return 0;
        }

        if (mtctx->jobReady == 0)
        {
            byte* src = (byte*)mtctx->inBuff.buffer.start;
            mtctx->jobs[jobID].src.start = src;
            mtctx->jobs[jobID].src.size = srcSize;
            assert(mtctx->inBuff.filled >= srcSize);
            mtctx->jobs[jobID].prefix = mtctx->inBuff.prefix;
            mtctx->jobs[jobID].consumed = 0;
            mtctx->jobs[jobID].cSize = 0;
            mtctx->jobs[jobID].@params = mtctx->@params;
            mtctx->jobs[jobID].cdict = mtctx->nextJobID == 0 ? mtctx->cdict : null;
            mtctx->jobs[jobID].fullFrameSize = mtctx->frameContentSize;
            mtctx->jobs[jobID].dstBuff = g_nullBuffer;
            mtctx->jobs[jobID].cctxPool = mtctx->cctxPool;
            mtctx->jobs[jobID].bufPool = mtctx->bufPool;
            mtctx->jobs[jobID].seqPool = mtctx->seqPool;
            mtctx->jobs[jobID].serial = &mtctx->serial;
            mtctx->jobs[jobID].jobID = mtctx->nextJobID;
            mtctx->jobs[jobID].firstJob = mtctx->nextJobID == 0 ? 1U : 0U;
            mtctx->jobs[jobID].lastJob = (uint)endFrame;
            mtctx->jobs[jobID].frameChecksumNeeded =
                mtctx->@params.fParams.checksumFlag != 0 && endFrame != 0 && mtctx->nextJobID > 0
                    ? 1U
                    : 0U;
            mtctx->jobs[jobID].dstFlushed = 0;
            mtctx->roundBuff.pos += srcSize;
            mtctx->inBuff.buffer = g_nullBuffer;
            mtctx->inBuff.filled = 0;
            if (endFrame == 0)
            {
                nuint newPrefixSize =
                    srcSize < mtctx->targetPrefixSize ? srcSize : mtctx->targetPrefixSize;
                mtctx->inBuff.prefix.start = src + srcSize - newPrefixSize;
                mtctx->inBuff.prefix.size = newPrefixSize;
            }
            else
            {
                mtctx->inBuff.prefix = kNullRange;
                mtctx->frameEnded = (uint)endFrame;
                if (mtctx->nextJobID == 0)
                {
                    mtctx->@params.fParams.checksumFlag = 0;
                }
            }

            if (srcSize == 0 && mtctx->nextJobID > 0)
            {
                assert(endOp == ZSTD_EndDirective.ZSTD_e_end);
                ZSTDMT_writeLastEmptyBlock(mtctx->jobs + jobID);
                mtctx->nextJobID++;
                return 0;
            }
        }

        if (
            POOL_tryAdd(
                mtctx->factory,
                (delegate* managed<void*, void>)(&ZSTDMT_compressionJob),
                &mtctx->jobs[jobID]
            ) != 0
        )
        {
            mtctx->nextJobID++;
            mtctx->jobReady = 0;
        }
        else
        {
            mtctx->jobReady = 1;
        }

        return 0;
    }

    /*! ZSTDMT_flushProduced() :
     *  flush whatever data has been produced but not yet flushed in current job.
     *  move to next job if current one is fully flushed.
     * `output` : `pos` will be updated with amount of data flushed .
     * `blockToFlush` : if >0, the function will block and wait if there is no data available to flush .
     * @return : amount of data remaining within internal buffer, 0 if no more, 1 if unknown but > 0, or an error code */
    private static nuint ZSTDMT_flushProduced(
        ZSTDMT_CCtx_s* mtctx,
        ZSTD_outBuffer_s* output,
        uint blockToFlush,
        ZSTD_EndDirective end
    )
    {
        uint wJobID = mtctx->doneJobID & mtctx->jobIDMask;
        assert(output->size >= output->pos);
        SynchronizationWrapper.Enter(&mtctx->jobs[wJobID].job_mutex);
        if (blockToFlush != 0 && mtctx->doneJobID < mtctx->nextJobID)
        {
            assert(mtctx->jobs[wJobID].dstFlushed <= mtctx->jobs[wJobID].cSize);
            while (mtctx->jobs[wJobID].dstFlushed == mtctx->jobs[wJobID].cSize)
            {
                if (mtctx->jobs[wJobID].consumed == mtctx->jobs[wJobID].src.size)
                {
                    break;
                }

                SynchronizationWrapper.Wait(&mtctx->jobs[wJobID].job_mutex);
            }
        }

        {
            /* shared */
            nuint cSize = mtctx->jobs[wJobID].cSize;
            /* shared */
            nuint srcConsumed = mtctx->jobs[wJobID].consumed;
            /* read-only, could be done after mutex lock, but no-declaration-after-statement */
            nuint srcSize = mtctx->jobs[wJobID].src.size;
            SynchronizationWrapper.Exit(&mtctx->jobs[wJobID].job_mutex);
            if (ERR_isError(cSize))
            {
                ZSTDMT_waitForAllJobsCompleted(mtctx);
                ZSTDMT_releaseAllJobResources(mtctx);
                return cSize;
            }

            assert(srcConsumed <= srcSize);
            if (srcConsumed == srcSize && mtctx->jobs[wJobID].frameChecksumNeeded != 0)
            {
                uint checksum = (uint)ZSTD_XXH64_digest(&mtctx->serial.xxhState);
                MEM_writeLE32(
                    (sbyte*)mtctx->jobs[wJobID].dstBuff.start + mtctx->jobs[wJobID].cSize,
                    checksum
                );
                cSize += 4;
                mtctx->jobs[wJobID].cSize += 4;
                mtctx->jobs[wJobID].frameChecksumNeeded = 0;
            }

            if (cSize > 0)
            {
                nuint toFlush =
                    cSize - mtctx->jobs[wJobID].dstFlushed < output->size - output->pos
                        ? cSize - mtctx->jobs[wJobID].dstFlushed
                        : output->size - output->pos;
                assert(mtctx->doneJobID < mtctx->nextJobID);
                assert(cSize >= mtctx->jobs[wJobID].dstFlushed);
                assert(mtctx->jobs[wJobID].dstBuff.start != null);
                if (toFlush > 0)
                {
                    memcpy(
                        (sbyte*)output->dst + output->pos,
                        (sbyte*)mtctx->jobs[wJobID].dstBuff.start + mtctx->jobs[wJobID].dstFlushed,
                        (uint)toFlush
                    );
                }

                output->pos += toFlush;
                mtctx->jobs[wJobID].dstFlushed += toFlush;
                if (srcConsumed == srcSize && mtctx->jobs[wJobID].dstFlushed == cSize)
                {
                    ZSTDMT_releaseBuffer(mtctx->bufPool, mtctx->jobs[wJobID].dstBuff);
                    mtctx->jobs[wJobID].dstBuff = g_nullBuffer;
                    mtctx->jobs[wJobID].cSize = 0;
                    mtctx->consumed += srcSize;
                    mtctx->produced += cSize;
                    mtctx->doneJobID++;
                }
            }

            if (cSize > mtctx->jobs[wJobID].dstFlushed)
                return cSize - mtctx->jobs[wJobID].dstFlushed;
            if (srcSize > srcConsumed)
                return 1;
        }

        if (mtctx->doneJobID < mtctx->nextJobID)
            return 1;
        if (mtctx->jobReady != 0)
            return 1;
        if (mtctx->inBuff.filled > 0)
            return 1;
        mtctx->allJobsCompleted = mtctx->frameEnded;
        if (end == ZSTD_EndDirective.ZSTD_e_end)
            return mtctx->frameEnded == 0 ? 1U : 0U;
        return 0;
    }

    /**
     * Returns the range of data used by the earliest job that is not yet complete.
     * If the data of the first job is broken up into two segments, we cover both
     * sections.
     */
    private static Range ZSTDMT_getInputDataInUse(ZSTDMT_CCtx_s* mtctx)
    {
        uint firstJobID = mtctx->doneJobID;
        uint lastJobID = mtctx->nextJobID;
        uint jobID;
        /* no need to check during first round */
        nuint roundBuffCapacity = mtctx->roundBuff.capacity;
        nuint nbJobs1stRoundMin = roundBuffCapacity / mtctx->targetSectionSize;
        if (lastJobID < nbJobs1stRoundMin)
            return kNullRange;
        for (jobID = firstJobID; jobID < lastJobID; ++jobID)
        {
            uint wJobID = jobID & mtctx->jobIDMask;
            nuint consumed;
            SynchronizationWrapper.Enter(&mtctx->jobs[wJobID].job_mutex);
            consumed = mtctx->jobs[wJobID].consumed;
            SynchronizationWrapper.Exit(&mtctx->jobs[wJobID].job_mutex);
            if (consumed < mtctx->jobs[wJobID].src.size)
            {
                Range range = mtctx->jobs[wJobID].prefix;
                if (range.size == 0)
                {
                    range = mtctx->jobs[wJobID].src;
                }

                assert(range.start <= mtctx->jobs[wJobID].src.start);
                return range;
            }
        }

        return kNullRange;
    }

    /**
     * Returns non-zero iff buffer and range overlap.
     */
    private static int ZSTDMT_isOverlapped(buffer_s buffer, Range range)
    {
        byte* bufferStart = (byte*)buffer.start;
        byte* rangeStart = (byte*)range.start;
        if (rangeStart == null || bufferStart == null)
            return 0;
        {
            byte* bufferEnd = bufferStart + buffer.capacity;
            byte* rangeEnd = rangeStart + range.size;
            if (bufferStart == bufferEnd || rangeStart == rangeEnd)
                return 0;
            return bufferStart < rangeEnd && rangeStart < bufferEnd ? 1 : 0;
        }
    }

    private static int ZSTDMT_doesOverlapWindow(buffer_s buffer, ZSTD_window_t window)
    {
        Range extDict;
        Range prefix;
        extDict.start = window.dictBase + window.lowLimit;
        extDict.size = window.dictLimit - window.lowLimit;
        prefix.start = window.@base + window.dictLimit;
        prefix.size = (nuint)(window.nextSrc - (window.@base + window.dictLimit));
        return ZSTDMT_isOverlapped(buffer, extDict) != 0 || ZSTDMT_isOverlapped(buffer, prefix) != 0
            ? 1
            : 0;
    }

    private static void ZSTDMT_waitForLdmComplete(ZSTDMT_CCtx_s* mtctx, buffer_s buffer)
    {
        if (mtctx->@params.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            void** mutex = &mtctx->serial.ldmWindowMutex;
            SynchronizationWrapper.Enter(mutex);
            while (ZSTDMT_doesOverlapWindow(buffer, mtctx->serial.ldmWindow) != 0)
            {
                SynchronizationWrapper.Wait(mutex);
            }

            SynchronizationWrapper.Exit(mutex);
        }
    }

    /**
     * Attempts to set the inBuff to the next section to fill.
     * If any part of the new section is still in use we give up.
     * Returns non-zero if the buffer is filled.
     */
    private static int ZSTDMT_tryGetInputRange(ZSTDMT_CCtx_s* mtctx)
    {
        Range inUse = ZSTDMT_getInputDataInUse(mtctx);
        nuint spaceLeft = mtctx->roundBuff.capacity - mtctx->roundBuff.pos;
        nuint spaceNeeded = mtctx->targetSectionSize;
        buffer_s buffer;
        assert(mtctx->inBuff.buffer.start == null);
        assert(mtctx->roundBuff.capacity >= spaceNeeded);
        if (spaceLeft < spaceNeeded)
        {
            /* ZSTD_invalidateRepCodes() doesn't work for extDict variants.
             * Simply copy the prefix to the beginning in that case.
             */
            byte* start = mtctx->roundBuff.buffer;
            nuint prefixSize = mtctx->inBuff.prefix.size;
            buffer.start = start;
            buffer.capacity = prefixSize;
            if (ZSTDMT_isOverlapped(buffer, inUse) != 0)
            {
                return 0;
            }

            ZSTDMT_waitForLdmComplete(mtctx, buffer);
            memmove(start, mtctx->inBuff.prefix.start, prefixSize);
            mtctx->inBuff.prefix.start = start;
            mtctx->roundBuff.pos = prefixSize;
        }

        buffer.start = mtctx->roundBuff.buffer + mtctx->roundBuff.pos;
        buffer.capacity = spaceNeeded;
        if (ZSTDMT_isOverlapped(buffer, inUse) != 0)
        {
            return 0;
        }

        assert(ZSTDMT_isOverlapped(buffer, mtctx->inBuff.prefix) == 0);
        ZSTDMT_waitForLdmComplete(mtctx, buffer);
        mtctx->inBuff.buffer = buffer;
        mtctx->inBuff.filled = 0;
        assert(mtctx->roundBuff.pos + buffer.capacity <= mtctx->roundBuff.capacity);
        return 1;
    }

    /**
     * Searches through the input for a synchronization point. If one is found, we
     * will instruct the caller to flush, and return the number of bytes to load.
     * Otherwise, we will load as many bytes as possible and instruct the caller
     * to continue as normal.
     */
    private static SyncPoint findSynchronizationPoint(ZSTDMT_CCtx_s* mtctx, ZSTD_inBuffer_s input)
    {
        byte* istart = (byte*)input.src + input.pos;
        ulong primePower = mtctx->rsync.primePower;
        ulong hitMask = mtctx->rsync.hitMask;
        SyncPoint syncPoint;
        ulong hash;
        byte* prev;
        nuint pos;
        syncPoint.toLoad =
            input.size - input.pos < mtctx->targetSectionSize - mtctx->inBuff.filled
                ? input.size - input.pos
                : mtctx->targetSectionSize - mtctx->inBuff.filled;
        syncPoint.flush = 0;
        if (mtctx->@params.rsyncable == 0)
            return syncPoint;
        if (mtctx->inBuff.filled + input.size - input.pos < 1 << 17)
            return syncPoint;
        if (mtctx->inBuff.filled + syncPoint.toLoad < 32)
            return syncPoint;
        if (mtctx->inBuff.filled < 1 << 17)
        {
            pos = (1 << 17) - mtctx->inBuff.filled;
            if (pos >= 32)
            {
                prev = istart + pos - 32;
                hash = ZSTD_rollingHash_compute(prev, 32);
            }
            else
            {
                assert(mtctx->inBuff.filled >= 32);
                prev = (byte*)mtctx->inBuff.buffer.start + mtctx->inBuff.filled - 32;
                hash = ZSTD_rollingHash_compute(prev + pos, 32 - pos);
                hash = ZSTD_rollingHash_append(hash, istart, pos);
            }
        }
        else
        {
            assert(mtctx->inBuff.filled >= 1 << 17);
            assert(1 << 17 >= 32);
            pos = 0;
            prev = (byte*)mtctx->inBuff.buffer.start + mtctx->inBuff.filled - 32;
            hash = ZSTD_rollingHash_compute(prev, 32);
            if ((hash & hitMask) == hitMask)
            {
                syncPoint.toLoad = 0;
                syncPoint.flush = 1;
                return syncPoint;
            }
        }

        assert(pos < 32 || ZSTD_rollingHash_compute(istart + pos - 32, 32) == hash);
        for (; pos < syncPoint.toLoad; ++pos)
        {
            byte toRemove = pos < 32 ? prev[pos] : istart[pos - 32];
            hash = ZSTD_rollingHash_rotate(hash, toRemove, istart[pos], primePower);
            assert(mtctx->inBuff.filled + pos >= 1 << 17);
            if ((hash & hitMask) == hitMask)
            {
                syncPoint.toLoad = pos + 1;
                syncPoint.flush = 1;
                ++pos;
                break;
            }
        }

        assert(pos < 32 || ZSTD_rollingHash_compute(istart + pos - 32, 32) == hash);
        return syncPoint;
    }

    /* ===   Streaming functions   === */
    private static nuint ZSTDMT_nextInputSizeHint(ZSTDMT_CCtx_s* mtctx)
    {
        nuint hintInSize = mtctx->targetSectionSize - mtctx->inBuff.filled;
        if (hintInSize == 0)
            hintInSize = mtctx->targetSectionSize;
        return hintInSize;
    }

    /** ZSTDMT_compressStream_generic() :
     *  internal use only - exposed to be invoked from zstd_compress.c
     *  assumption : output and input are valid (pos <= size)
     * @return : minimum amount of data remaining to flush, 0 if none */
    private static nuint ZSTDMT_compressStream_generic(
        ZSTDMT_CCtx_s* mtctx,
        ZSTD_outBuffer_s* output,
        ZSTD_inBuffer_s* input,
        ZSTD_EndDirective endOp
    )
    {
        uint forwardInputProgress = 0;
        assert(output->pos <= output->size);
        assert(input->pos <= input->size);
        if (mtctx->frameEnded != 0 && endOp == ZSTD_EndDirective.ZSTD_e_continue)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        if (mtctx->jobReady == 0 && input->size > input->pos)
        {
            if (mtctx->inBuff.buffer.start == null)
            {
                assert(mtctx->inBuff.filled == 0);
                if (ZSTDMT_tryGetInputRange(mtctx) == 0)
                {
                    assert(mtctx->doneJobID != mtctx->nextJobID);
                }
            }

            if (mtctx->inBuff.buffer.start != null)
            {
                SyncPoint syncPoint = findSynchronizationPoint(mtctx, *input);
                if (syncPoint.flush != 0 && endOp == ZSTD_EndDirective.ZSTD_e_continue)
                {
                    endOp = ZSTD_EndDirective.ZSTD_e_flush;
                }

                assert(mtctx->inBuff.buffer.capacity >= mtctx->targetSectionSize);
                memcpy(
                    (sbyte*)mtctx->inBuff.buffer.start + mtctx->inBuff.filled,
                    (sbyte*)input->src + input->pos,
                    (uint)syncPoint.toLoad
                );
                input->pos += syncPoint.toLoad;
                mtctx->inBuff.filled += syncPoint.toLoad;
                forwardInputProgress = syncPoint.toLoad > 0 ? 1U : 0U;
            }
        }

        if (input->pos < input->size && endOp == ZSTD_EndDirective.ZSTD_e_end)
        {
            assert(
                mtctx->inBuff.filled == 0
                    || mtctx->inBuff.filled == mtctx->targetSectionSize
                    || mtctx->@params.rsyncable != 0
            );
            endOp = ZSTD_EndDirective.ZSTD_e_flush;
        }

        if (
            mtctx->jobReady != 0
            || mtctx->inBuff.filled >= mtctx->targetSectionSize
            || endOp != ZSTD_EndDirective.ZSTD_e_continue && mtctx->inBuff.filled > 0
            || endOp == ZSTD_EndDirective.ZSTD_e_end && mtctx->frameEnded == 0
        )
        {
            nuint jobSize = mtctx->inBuff.filled;
            assert(mtctx->inBuff.filled <= mtctx->targetSectionSize);
            {
                nuint err_code = ZSTDMT_createCompressionJob(mtctx, jobSize, endOp);
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }
        }

        {
            /* block if there was no forward input progress */
            nuint remainingToFlush = ZSTDMT_flushProduced(
                mtctx,
                output,
                forwardInputProgress == 0 ? 1U : 0U,
                endOp
            );
            if (input->pos < input->size)
                return remainingToFlush > 1 ? remainingToFlush : 1;
            return remainingToFlush;
        }
    }
}
