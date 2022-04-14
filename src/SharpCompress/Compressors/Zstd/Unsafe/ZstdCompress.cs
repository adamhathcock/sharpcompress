using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /*-*************************************
        *  Helper functions
        ***************************************/
        /* ZSTD_compressBound()
         * Note that the result from this function is only compatible with the "normal"
         * full-block strategy.
         * When there are a lot of small blocks due to frequent flush in streaming mode
         * the overhead of headers can make the compressed data to be larger than the
         * return value of ZSTD_compressBound().
         */
        public static nuint ZSTD_compressBound(nuint srcSize)
        {
            return ((srcSize) + ((srcSize) >> 8) + (((srcSize) < (uint)((128 << 10))) ? (((uint)((128 << 10)) - (srcSize)) >> 11) : 0));
        }

        public static ZSTD_CCtx_s* ZSTD_createCCtx()
        {
            return ZSTD_createCCtx_advanced(ZSTD_defaultCMem);
        }

        private static void ZSTD_initCCtx(ZSTD_CCtx_s* cctx, ZSTD_customMem memManager)
        {
            assert(cctx != null);
            memset((void*)(cctx), (0), ((nuint)(sizeof(ZSTD_CCtx_s))));
            cctx->customMem = memManager;
            cctx->bmi2 = ((IsBmi2Supported) ? 1 : 0);

            {
                nuint err = ZSTD_CCtx_reset(cctx, ZSTD_ResetDirective.ZSTD_reset_parameters);

                assert((ERR_isError(err)) == 0);
            }
        }

        public static ZSTD_CCtx_s* ZSTD_createCCtx_advanced(ZSTD_customMem customMem)
        {
            if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            {
                return (ZSTD_CCtx_s*)null;
            }


            {
                ZSTD_CCtx_s* cctx = (ZSTD_CCtx_s*)(ZSTD_customMalloc((nuint)(sizeof(ZSTD_CCtx_s)), customMem));

                if (cctx == null)
                {
                    return (ZSTD_CCtx_s*)null;
                }

                ZSTD_initCCtx(cctx, customMem);
                return cctx;
            }
        }

        /*! ZSTD_initStatic*() :
         *  Initialize an object using a pre-allocated fixed-size buffer.
         *  workspace: The memory area to emplace the object into.
         *             Provided pointer *must be 8-bytes aligned*.
         *             Buffer must outlive object.
         *  workspaceSize: Use ZSTD_estimate*Size() to determine
         *                 how large workspace must be to support target scenario.
         * @return : pointer to object (same address as workspace, just different type),
         *           or NULL if error (size too small, incorrect alignment, etc.)
         *  Note : zstd will never resize nor malloc() when using a static buffer.
         *         If the object requires more memory than available,
         *         zstd will just error out (typically ZSTD_error_memory_allocation).
         *  Note 2 : there is no corresponding "free" function.
         *           Since workspace is allocated externally, it must be freed externally too.
         *  Note 3 : cParams : use ZSTD_getCParams() to convert a compression level
         *           into its associated cParams.
         *  Limitation 1 : currently not compatible with internal dictionary creation, triggered by
         *                 ZSTD_CCtx_loadDictionary(), ZSTD_initCStream_usingDict() or ZSTD_initDStream_usingDict().
         *  Limitation 2 : static cctx currently not compatible with multi-threading.
         *  Limitation 3 : static dctx is incompatible with legacy support.
         */
        public static ZSTD_CCtx_s* ZSTD_initStaticCCtx(void* workspace, nuint workspaceSize)
        {
            ZSTD_cwksp ws;
            ZSTD_CCtx_s* cctx;

            if (workspaceSize <= (nuint)(sizeof(ZSTD_CCtx_s)))
            {
                return (ZSTD_CCtx_s*)null;
            }

            if (((nuint)(workspace) & 7) != 0)
            {
                return (ZSTD_CCtx_s*)null;
            }

            ZSTD_cwksp_init(&ws, workspace, workspaceSize, ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_static_alloc);
            cctx = (ZSTD_CCtx_s*)(ZSTD_cwksp_reserve_object(&ws, (nuint)(sizeof(ZSTD_CCtx_s))));
            if (cctx == null)
            {
                return (ZSTD_CCtx_s*)null;
            }

            memset((void*)(cctx), (0), ((nuint)(sizeof(ZSTD_CCtx_s))));
            ZSTD_cwksp_move(&cctx->workspace, &ws);
            cctx->staticSize = workspaceSize;
            if ((ZSTD_cwksp_check_available(&cctx->workspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2)))) + 2 * (nuint)(sizeof(ZSTD_compressedBlockState_t)))) == 0)
            {
                return (ZSTD_CCtx_s*)null;
            }

            cctx->blockState.prevCBlock = (ZSTD_compressedBlockState_t*)(ZSTD_cwksp_reserve_object(&cctx->workspace, (nuint)(sizeof(ZSTD_compressedBlockState_t))));
            cctx->blockState.nextCBlock = (ZSTD_compressedBlockState_t*)(ZSTD_cwksp_reserve_object(&cctx->workspace, (nuint)(sizeof(ZSTD_compressedBlockState_t))));
            cctx->entropyWorkspace = (uint*)(ZSTD_cwksp_reserve_object(&cctx->workspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2))))));
            cctx->bmi2 = ((IsBmi2Supported) ? 1 : 0);
            return cctx;
        }

        /**
         * Clears and frees all of the dictionaries in the CCtx.
         */
        private static void ZSTD_clearAllDicts(ZSTD_CCtx_s* cctx)
        {
            ZSTD_customFree(cctx->localDict.dictBuffer, cctx->customMem);
            ZSTD_freeCDict(cctx->localDict.cdict);
            memset((void*)(&cctx->localDict), (0), ((nuint)(sizeof(ZSTD_localDict))));
            memset((void*)(&cctx->prefixDict), (0), ((nuint)(sizeof(ZSTD_prefixDict_s))));
            cctx->cdict = null;
        }

        private static nuint ZSTD_sizeof_localDict(ZSTD_localDict dict)
        {
            nuint bufferSize = dict.dictBuffer != null ? dict.dictSize : 0;
            nuint cdictSize = ZSTD_sizeof_CDict(dict.cdict);

            return bufferSize + cdictSize;
        }

        private static void ZSTD_freeCCtxContent(ZSTD_CCtx_s* cctx)
        {
            assert(cctx != null);
            assert(cctx->staticSize == 0);
            ZSTD_clearAllDicts(cctx);
            ZSTD_cwksp_free(&cctx->workspace, cctx->customMem);
        }

        public static nuint ZSTD_freeCCtx(ZSTD_CCtx_s* cctx)
        {
            if (cctx == null)
            {
                return 0;
            }

            if (cctx->staticSize != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }


            {
                int cctxInWorkspace = ZSTD_cwksp_owns_buffer(&cctx->workspace, (void*)cctx);

                ZSTD_freeCCtxContent(cctx);
                if (cctxInWorkspace == 0)
                {
                    ZSTD_customFree((void*)cctx, cctx->customMem);
                }
            }

            return 0;
        }

        private static nuint ZSTD_sizeof_mtctx(ZSTD_CCtx_s* cctx)
        {
            return 0;
        }

        /*! ZSTD_sizeof_*() : Requires v1.4.0+
         *  These functions give the _current_ memory usage of selected object.
         *  Note that object memory usage can evolve (increase or decrease) over time. */
        public static nuint ZSTD_sizeof_CCtx(ZSTD_CCtx_s* cctx)
        {
            if (cctx == null)
            {
                return 0;
            }

            return (cctx->workspace.workspace == cctx ? 0 : (nuint)(sizeof(ZSTD_CCtx_s))) + ZSTD_cwksp_sizeof(&cctx->workspace) + ZSTD_sizeof_localDict(cctx->localDict) + ZSTD_sizeof_mtctx(cctx);
        }

        public static nuint ZSTD_sizeof_CStream(ZSTD_CCtx_s* zcs)
        {
            return ZSTD_sizeof_CCtx(zcs);
        }

        /* private API call, for dictBuilder only */
        public static seqStore_t* ZSTD_getSeqStore(ZSTD_CCtx_s* ctx)
        {
            return &(ctx->seqStore);
        }

        /* Returns true if the strategy supports using a row based matchfinder */
        private static int ZSTD_rowMatchFinderSupported(ZSTD_strategy strategy)
        {
            return ((strategy >= ZSTD_strategy.ZSTD_greedy && strategy <= ZSTD_strategy.ZSTD_lazy2) ? 1 : 0);
        }

        /* Returns true if the strategy and useRowMatchFinder mode indicate that we will use the row based matchfinder
         * for this compression.
         */
        private static int ZSTD_rowMatchFinderUsed(ZSTD_strategy strategy, ZSTD_useRowMatchFinderMode_e mode)
        {
            assert(mode != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
            return (((ZSTD_rowMatchFinderSupported(strategy)) != 0 && (mode == ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder)) ? 1 : 0);
        }

        /* Returns row matchfinder usage enum given an initial mode and cParams */
        private static ZSTD_useRowMatchFinderMode_e ZSTD_resolveRowMatchFinderMode(ZSTD_useRowMatchFinderMode_e mode, ZSTD_compressionParameters* cParams)
        {
            int kHasSIMD128 = 1;

            if (mode != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto)
            {
                return mode;
            }

            mode = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_disableRowMatchFinder;
            if ((ZSTD_rowMatchFinderSupported(cParams->strategy)) == 0)
            {
                return mode;
            }

            if (kHasSIMD128 != 0)
            {
                if (cParams->windowLog > 14)
                {
                    mode = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder;
                }
            }
            else
            {
                if (cParams->windowLog > 17)
                {
                    mode = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder;
                }
            }

            return mode;
        }

        /* Returns 1 if the arguments indicate that we should allocate a chainTable, 0 otherwise */
        private static int ZSTD_allocateChainTable(ZSTD_strategy strategy, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, uint forDDSDict)
        {
            assert(useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
            return ((forDDSDict != 0 || ((strategy != ZSTD_strategy.ZSTD_fast) && (ZSTD_rowMatchFinderUsed(strategy, useRowMatchFinder)) == 0)) ? 1 : 0);
        }

        /* Returns 1 if compression parameters are such that we should
         * enable long distance matching (wlog >= 27, strategy >= btopt).
         * Returns 0 otherwise.
         */
        private static uint ZSTD_CParams_shouldEnableLdm(ZSTD_compressionParameters* cParams)
        {
            return ((cParams->strategy >= ZSTD_strategy.ZSTD_btopt && cParams->windowLog >= 27) ? 1U : 0U);
        }

        /* Returns 1 if compression parameters are such that we should
         * enable blockSplitter (wlog >= 17, strategy >= btopt).
         * Returns 0 otherwise.
         */
        private static uint ZSTD_CParams_useBlockSplitter(ZSTD_compressionParameters* cParams)
        {
            return ((cParams->strategy >= ZSTD_strategy.ZSTD_btopt && cParams->windowLog >= 17) ? 1U : 0U);
        }

        private static ZSTD_CCtx_params_s ZSTD_makeCCtxParamsFromCParams(ZSTD_compressionParameters cParams)
        {
            ZSTD_CCtx_params_s cctxParams;

            ZSTD_CCtxParams_init(&cctxParams, 3);
            cctxParams.cParams = cParams;
            if ((ZSTD_CParams_shouldEnableLdm(&cParams)) != 0)
            {
                cctxParams.ldmParams.enableLdm = 1;
                ZSTD_ldm_adjustParameters(&cctxParams.ldmParams, &cParams);
                assert(cctxParams.ldmParams.hashLog >= cctxParams.ldmParams.bucketSizeLog);
                assert(cctxParams.ldmParams.hashRateLog < 32);
            }

            if ((ZSTD_CParams_useBlockSplitter(&cParams)) != 0)
            {
                cctxParams.splitBlocks = 1;
            }

            cctxParams.useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(cctxParams.useRowMatchFinder, &cParams);
            assert((ZSTD_checkCParams(cParams)) == 0);
            return cctxParams;
        }

        private static ZSTD_CCtx_params_s* ZSTD_createCCtxParams_advanced(ZSTD_customMem customMem)
        {
            ZSTD_CCtx_params_s* @params;

            if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            {
                return (ZSTD_CCtx_params_s*)null;
            }

            @params = (ZSTD_CCtx_params_s*)(ZSTD_customCalloc((nuint)(sizeof(ZSTD_CCtx_params_s)), customMem));
            if (@params == null)
            {
                return (ZSTD_CCtx_params_s*)null;
            }

            ZSTD_CCtxParams_init(@params, 3);
            @params->customMem = customMem;
            return @params;
        }

        /*! ZSTD_CCtx_params :
         *  Quick howto :
         *  - ZSTD_createCCtxParams() : Create a ZSTD_CCtx_params structure
         *  - ZSTD_CCtxParams_setParameter() : Push parameters one by one into
         *                                     an existing ZSTD_CCtx_params structure.
         *                                     This is similar to
         *                                     ZSTD_CCtx_setParameter().
         *  - ZSTD_CCtx_setParametersUsingCCtxParams() : Apply parameters to
         *                                    an existing CCtx.
         *                                    These parameters will be applied to
         *                                    all subsequent frames.
         *  - ZSTD_compressStream2() : Do compression using the CCtx.
         *  - ZSTD_freeCCtxParams() : Free the memory, accept NULL pointer.
         *
         *  This can be used with ZSTD_estimateCCtxSize_advanced_usingCCtxParams()
         *  for static allocation of CCtx for single-threaded compression.
         */
        public static ZSTD_CCtx_params_s* ZSTD_createCCtxParams()
        {
            return ZSTD_createCCtxParams_advanced(ZSTD_defaultCMem);
        }

        public static nuint ZSTD_freeCCtxParams(ZSTD_CCtx_params_s* @params)
        {
            if (@params == null)
            {
                return 0;
            }

            ZSTD_customFree((void*)@params, @params->customMem);
            return 0;
        }

        /*! ZSTD_CCtxParams_reset() :
         *  Reset params to default values.
         */
        public static nuint ZSTD_CCtxParams_reset(ZSTD_CCtx_params_s* @params)
        {
            return ZSTD_CCtxParams_init(@params, 3);
        }

        /*! ZSTD_CCtxParams_init() :
         *  Initializes the compression parameters of cctxParams according to
         *  compression level. All other parameters are reset to their default values.
         */
        public static nuint ZSTD_CCtxParams_init(ZSTD_CCtx_params_s* cctxParams, int compressionLevel)
        {
            if (cctxParams == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            memset((void*)(cctxParams), (0), ((nuint)(sizeof(ZSTD_CCtx_params_s))));
            cctxParams->compressionLevel = compressionLevel;
            cctxParams->fParams.contentSizeFlag = 1;
            return 0;
        }

        /**
         * Initializes the cctxParams from params and compressionLevel.
         * @param compressionLevel If params are derived from a compression level then that compression level, otherwise ZSTD_NO_CLEVEL.
         */
        private static void ZSTD_CCtxParams_init_internal(ZSTD_CCtx_params_s* cctxParams, ZSTD_parameters* @params, int compressionLevel)
        {
            assert((ZSTD_checkCParams(@params->cParams)) == 0);
            memset((void*)(cctxParams), (0), ((nuint)(sizeof(ZSTD_CCtx_params_s))));
            cctxParams->cParams = @params->cParams;
            cctxParams->fParams = @params->fParams;
            cctxParams->compressionLevel = compressionLevel;
            cctxParams->useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(cctxParams->useRowMatchFinder, &@params->cParams);
        }

        /*! ZSTD_CCtxParams_init_advanced() :
         *  Initializes the compression and frame parameters of cctxParams according to
         *  params. All other parameters are reset to their default values.
         */
        public static nuint ZSTD_CCtxParams_init_advanced(ZSTD_CCtx_params_s* cctxParams, ZSTD_parameters @params)
        {
            if (cctxParams == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }


            {
                nuint err_code = (ZSTD_checkCParams(@params.cParams));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            ZSTD_CCtxParams_init_internal(cctxParams, &@params, 0);
            return 0;
        }

        /**
         * Sets cctxParams' cParams and fParams from params, but otherwise leaves them alone.
         * @param param Validated zstd parameters.
         */
        private static void ZSTD_CCtxParams_setZstdParams(ZSTD_CCtx_params_s* cctxParams, ZSTD_parameters* @params)
        {
            assert((ZSTD_checkCParams(@params->cParams)) == 0);
            cctxParams->cParams = @params->cParams;
            cctxParams->fParams = @params->fParams;
            cctxParams->compressionLevel = 0;
        }

        /*! ZSTD_cParam_getBounds() :
         *  All parameters must belong to an interval with lower and upper bounds,
         *  otherwise they will either trigger an error or be automatically clamped.
         * @return : a structure, ZSTD_bounds, which contains
         *         - an error status field, which must be tested using ZSTD_isError()
         *         - lower and upper bounds, both inclusive
         */
        public static ZSTD_bounds ZSTD_cParam_getBounds(ZSTD_cParameter param)
        {
            ZSTD_bounds bounds = new ZSTD_bounds
            {
                error = 0,
                lowerBound = 0,
                upperBound = 0,
            };

            switch (param)
            {
                case ZSTD_cParameter.ZSTD_c_compressionLevel:
                {
                    bounds.lowerBound = ZSTD_minCLevel();
                }

                bounds.upperBound = ZSTD_maxCLevel();
                return bounds;
                case ZSTD_cParameter.ZSTD_c_windowLog:
                {
                    bounds.lowerBound = 10;
                }

                bounds.upperBound = ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31));
                return bounds;
                case ZSTD_cParameter.ZSTD_c_hashLog:
                {
                    bounds.lowerBound = 6;
                }

                bounds.upperBound = ((((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) < 30) ? ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) : 30);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_chainLog:
                {
                    bounds.lowerBound = 6;
                }

                bounds.upperBound = ((int)((nuint)(sizeof(nuint)) == 4 ? 29 : 30));
                return bounds;
                case ZSTD_cParameter.ZSTD_c_searchLog:
                {
                    bounds.lowerBound = 1;
                }

                bounds.upperBound = (((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) - 1);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_minMatch:
                {
                    bounds.lowerBound = 3;
                }

                bounds.upperBound = 7;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_targetLength:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = (1 << 17);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_strategy:
                {
                    bounds.lowerBound = (int)ZSTD_strategy.ZSTD_fast;
                }

                bounds.upperBound = (int)ZSTD_strategy.ZSTD_btultra2;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_checksumFlag:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_nbWorkers:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 0;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_jobSize:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 0;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_overlapLog:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 0;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                {
                    bounds.lowerBound = 6;
                }

                bounds.upperBound = ((((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) < 30) ? ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) : 30);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                {
                    bounds.lowerBound = 4;
                }

                bounds.upperBound = 4096;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                {
                    bounds.lowerBound = 1;
                }

                bounds.upperBound = 8;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = (((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) - 6);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                {

                }

                bounds.lowerBound = (int)ZSTD_format_e.ZSTD_f_zstd1;
                bounds.upperBound = (int)ZSTD_format_e.ZSTD_f_zstd1_magicless;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                {

                }

                bounds.lowerBound = (int)ZSTD_dictAttachPref_e.ZSTD_dictDefaultAttach;
                bounds.upperBound = (int)ZSTD_dictAttachPref_e.ZSTD_dictForceLoad;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                {

                }

                bounds.lowerBound = (int)ZSTD_literalCompressionMode_e.ZSTD_lcm_auto;
                bounds.upperBound = (int)ZSTD_literalCompressionMode_e.ZSTD_lcm_uncompressed;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam6:
                {
                    bounds.lowerBound = 64;
                }

                bounds.upperBound = (1 << 17);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 2147483647;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                {
                    bounds.lowerBound = (int)(ZSTD_bufferMode_e.ZSTD_bm_buffered);
                }

                bounds.upperBound = (int)(ZSTD_bufferMode_e.ZSTD_bm_stable);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                {
                    bounds.lowerBound = (int)(ZSTD_sequenceFormat_e.ZSTD_sf_noBlockDelimiters);
                }

                bounds.upperBound = (int)(ZSTD_sequenceFormat_e.ZSTD_sf_explicitBlockDelimiters);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                {
                    bounds.lowerBound = (int)(ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
                }

                bounds.upperBound = (int)(ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder);
                return bounds;
                case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                {
                    bounds.lowerBound = 0;
                }

                bounds.upperBound = 1;
                return bounds;
                default:
                {
                    bounds.error = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                }

                return bounds;
            }
        }

        /* ZSTD_cParam_clampBounds:
         * Clamps the value into the bounded range.
         */
        private static nuint ZSTD_cParam_clampBounds(ZSTD_cParameter cParam, int* value)
        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(cParam);

            if ((ERR_isError(bounds.error)) != 0)
            {
                return bounds.error;
            }

            if (*value < bounds.lowerBound)
            {
                *value = bounds.lowerBound;
            }

            if (*value > bounds.upperBound)
            {
                *value = bounds.upperBound;
            }

            return 0;
        }

        private static int ZSTD_isUpdateAuthorized(ZSTD_cParameter param)
        {
            switch (param)
            {
                case ZSTD_cParameter.ZSTD_c_compressionLevel:
                case ZSTD_cParameter.ZSTD_c_hashLog:
                case ZSTD_cParameter.ZSTD_c_chainLog:
                case ZSTD_cParameter.ZSTD_c_searchLog:
                case ZSTD_cParameter.ZSTD_c_minMatch:
                case ZSTD_cParameter.ZSTD_c_targetLength:
                case ZSTD_cParameter.ZSTD_c_strategy:
                {
                    return 1;
                }

                case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                case ZSTD_cParameter.ZSTD_c_windowLog:
                case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                case ZSTD_cParameter.ZSTD_c_checksumFlag:
                case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                case ZSTD_cParameter.ZSTD_c_nbWorkers:
                case ZSTD_cParameter.ZSTD_c_jobSize:
                case ZSTD_cParameter.ZSTD_c_overlapLog:
                case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                case ZSTD_cParameter.ZSTD_c_experimentalParam6:
                case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                default:
                {
                    return 0;
                }
            }
        }

        /*! ZSTD_CCtx_setParameter() :
         *  Set one compression parameter, selected by enum ZSTD_cParameter.
         *  All parameters have valid bounds. Bounds can be queried using ZSTD_cParam_getBounds().
         *  Providing a value beyond bound will either clamp it, or trigger an error (depending on parameter).
         *  Setting a parameter is generally only possible during frame initialization (before starting compression).
         *  Exception : when using multi-threading mode (nbWorkers >= 1),
         *              the following parameters can be updated _during_ compression (within same frame):
         *              => compressionLevel, hashLog, chainLog, searchLog, minMatch, targetLength and strategy.
         *              new parameters will be active for next job only (after a flush()).
         * @return : an error code (which can be tested using ZSTD_isError()).
         */
        public static nuint ZSTD_CCtx_setParameter(ZSTD_CCtx_s* cctx, ZSTD_cParameter param, int value)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                if ((ZSTD_isUpdateAuthorized(param)) != 0)
                {
                    cctx->cParamsChanged = 1;
                }
                else
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
                    }

                }
            }

            switch (param)
            {
                case ZSTD_cParameter.ZSTD_c_nbWorkers:
                {
                    if ((value != 0) && cctx->staticSize != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                break;
                case ZSTD_cParameter.ZSTD_c_compressionLevel:
                case ZSTD_cParameter.ZSTD_c_windowLog:
                case ZSTD_cParameter.ZSTD_c_hashLog:
                case ZSTD_cParameter.ZSTD_c_chainLog:
                case ZSTD_cParameter.ZSTD_c_searchLog:
                case ZSTD_cParameter.ZSTD_c_minMatch:
                case ZSTD_cParameter.ZSTD_c_targetLength:
                case ZSTD_cParameter.ZSTD_c_strategy:
                case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                case ZSTD_cParameter.ZSTD_c_checksumFlag:
                case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                case ZSTD_cParameter.ZSTD_c_jobSize:
                case ZSTD_cParameter.ZSTD_c_overlapLog:
                case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                case ZSTD_cParameter.ZSTD_c_experimentalParam6:
                case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                {
                    break;
                }

                default:
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

            }

            return ZSTD_CCtxParams_setParameter(&cctx->requestedParams, param, value);
        }

        /*! ZSTD_CCtxParams_setParameter() : Requires v1.4.0+
         *  Similar to ZSTD_CCtx_setParameter.
         *  Set one compression parameter, selected by enum ZSTD_cParameter.
         *  Parameters must be applied to a ZSTD_CCtx using
         *  ZSTD_CCtx_setParametersUsingCCtxParams().
         * @result : a code representing success or failure (which can be tested with
         *           ZSTD_isError()).
         */
        public static nuint ZSTD_CCtxParams_setParameter(ZSTD_CCtx_params_s* CCtxParams, ZSTD_cParameter param, int value)
        {
            switch (param)
            {
                case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam2, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->format = (ZSTD_format_e)(value);
                return (nuint)(CCtxParams->format);
                case ZSTD_cParameter.ZSTD_c_compressionLevel:
                {

                    {
                        nuint err_code = (ZSTD_cParam_clampBounds(param, &value));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    if (value == 0)
                    {
                        CCtxParams->compressionLevel = 3;
                    }
                    else
                    {
                        CCtxParams->compressionLevel = value;
                    }

                    if (CCtxParams->compressionLevel >= 0)
                    {
                        return (nuint)(CCtxParams->compressionLevel);
                    }

                    return 0;
                }

                case ZSTD_cParameter.ZSTD_c_windowLog:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_windowLog, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->cParams.windowLog = (uint)(value);
                return CCtxParams->cParams.windowLog;
                case ZSTD_cParameter.ZSTD_c_hashLog:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_hashLog, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->cParams.hashLog = (uint)(value);
                return CCtxParams->cParams.hashLog;
                case ZSTD_cParameter.ZSTD_c_chainLog:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_chainLog, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->cParams.chainLog = (uint)(value);
                return CCtxParams->cParams.chainLog;
                case ZSTD_cParameter.ZSTD_c_searchLog:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_searchLog, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->cParams.searchLog = (uint)(value);
                return (nuint)(value);
                case ZSTD_cParameter.ZSTD_c_minMatch:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_minMatch, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->cParams.minMatch = (uint)value;
                return CCtxParams->cParams.minMatch;
                case ZSTD_cParameter.ZSTD_c_targetLength:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_targetLength, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->cParams.targetLength = (uint)value;
                return CCtxParams->cParams.targetLength;
                case ZSTD_cParameter.ZSTD_c_strategy:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->cParams.strategy = (ZSTD_strategy)(value);
                return (nuint)(CCtxParams->cParams.strategy);
                case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
        ;
                CCtxParams->fParams.contentSizeFlag = ((value != 0) ? 1 : 0);
                return (nuint)CCtxParams->fParams.contentSizeFlag;
                case ZSTD_cParameter.ZSTD_c_checksumFlag:
                {
                    CCtxParams->fParams.checksumFlag = ((value != 0) ? 1 : 0);
                }

                return (nuint)CCtxParams->fParams.checksumFlag;
                case ZSTD_cParameter.ZSTD_c_dictIDFlag:
        ;
                CCtxParams->fParams.noDictIDFlag = (value == 0 ? 1 : 0);
                return (CCtxParams->fParams.noDictIDFlag == 0 ? 1U : 0U);
                case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                {
                    CCtxParams->forceWindow = ((value != 0) ? 1 : 0);
                }

                return (nuint)CCtxParams->forceWindow;
                case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                {
                    ZSTD_dictAttachPref_e pref = (ZSTD_dictAttachPref_e)(value);


                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam4, (int)pref)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }

                    CCtxParams->attachDictPref = pref;
                    return (nuint)CCtxParams->attachDictPref;
                }

                case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                {
                    ZSTD_literalCompressionMode_e lcm = (ZSTD_literalCompressionMode_e)(value);


                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam5, (int)lcm)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }

                    CCtxParams->literalCompressionMode = lcm;
                    return (nuint)CCtxParams->literalCompressionMode;
                }

                case ZSTD_cParameter.ZSTD_c_nbWorkers:
                {
                    if (value != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                return 0;
                case ZSTD_cParameter.ZSTD_c_jobSize:
                {
                    if (value != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                return 0;
                case ZSTD_cParameter.ZSTD_c_overlapLog:
                {
                    if (value != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                return 0;
                case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                {
                    if (value != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                return 0;
                case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                {
                    CCtxParams->enableDedicatedDictSearch = ((value != 0) ? 1 : 0);
                }

                return (nuint)CCtxParams->enableDedicatedDictSearch;
                case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                {
                    CCtxParams->ldmParams.enableLdm = (((value != 0)) ? 1U : 0U);
                }

                return CCtxParams->ldmParams.enableLdm;
                case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmHashLog, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->ldmParams.hashLog = (uint)value;
                return CCtxParams->ldmParams.hashLog;
                case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmMinMatch, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->ldmParams.minMatchLength = (uint)value;
                return CCtxParams->ldmParams.minMatchLength;
                case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->ldmParams.bucketSizeLog = (uint)value;
                return CCtxParams->ldmParams.bucketSizeLog;
                case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                {
                    if (value > ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) - 6)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }
                }

                CCtxParams->ldmParams.hashRateLog = (uint)value;
                return CCtxParams->ldmParams.hashRateLog;
                case ZSTD_cParameter.ZSTD_c_experimentalParam6:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam6, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->targetCBlockSize = (nuint)value;
                return CCtxParams->targetCBlockSize;
                case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                {
                    if (value != 0)
                    {
                        if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam7, value)) == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                        }

                    }
                }

                CCtxParams->srcSizeHint = value;
                return (nuint)CCtxParams->srcSizeHint;
                case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam9, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->inBufferMode = (ZSTD_bufferMode_e)(value);
                return (nuint)CCtxParams->inBufferMode;
                case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam10, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->outBufferMode = (ZSTD_bufferMode_e)(value);
                return (nuint)CCtxParams->outBufferMode;
                case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam11, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->blockDelimiters = (ZSTD_sequenceFormat_e)(value);
                return (nuint)CCtxParams->blockDelimiters;
                case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam12, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->validateSequences = value;
                return (nuint)CCtxParams->validateSequences;
                case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam13, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->splitBlocks = value;
                return (nuint)CCtxParams->splitBlocks;
                case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam14, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->useRowMatchFinder = (ZSTD_useRowMatchFinderMode_e)(value);
                return (nuint)CCtxParams->useRowMatchFinder;
                case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                {
                    if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam15, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                CCtxParams->deterministicRefPrefix = (!(value == 0) ? 1 : 0);
                return (nuint)CCtxParams->deterministicRefPrefix;
                default:
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

            }
        }

        /*! ZSTD_CCtx_getParameter() :
         *  Get the requested compression parameter value, selected by enum ZSTD_cParameter,
         *  and store it into int* value.
         * @return : 0, or an error code (which can be tested with ZSTD_isError()).
         */
        public static nuint ZSTD_CCtx_getParameter(ZSTD_CCtx_s* cctx, ZSTD_cParameter param, int* value)
        {
            return ZSTD_CCtxParams_getParameter(&cctx->requestedParams, param, value);
        }

        /*! ZSTD_CCtxParams_getParameter() :
         * Similar to ZSTD_CCtx_getParameter.
         * Get the requested value of one compression parameter, selected by enum ZSTD_cParameter.
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         */
        public static nuint ZSTD_CCtxParams_getParameter(ZSTD_CCtx_params_s* CCtxParams, ZSTD_cParameter param, int* value)
        {
            switch (param)
            {
                case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                {
                    *value = (int)CCtxParams->format;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_compressionLevel:
                {
                    *value = CCtxParams->compressionLevel;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_windowLog:
                {
                    *value = (int)(CCtxParams->cParams.windowLog);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_hashLog:
                {
                    *value = (int)(CCtxParams->cParams.hashLog);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_chainLog:
                {
                    *value = (int)(CCtxParams->cParams.chainLog);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_searchLog:
                {
                    *value = (int)CCtxParams->cParams.searchLog;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_minMatch:
                {
                    *value = (int)CCtxParams->cParams.minMatch;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_targetLength:
                {
                    *value = (int)CCtxParams->cParams.targetLength;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_strategy:
                {
                    *value = (int)(uint)(CCtxParams->cParams.strategy);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                {
                    *value = CCtxParams->fParams.contentSizeFlag;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_checksumFlag:
                {
                    *value = CCtxParams->fParams.checksumFlag;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                {
                    *value = (CCtxParams->fParams.noDictIDFlag == 0 ? 1 : 0);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                {
                    *value = CCtxParams->forceWindow;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                {
                    *value = (int)CCtxParams->attachDictPref;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                {
                    *value = (int)CCtxParams->literalCompressionMode;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_nbWorkers:
                {
                    assert(CCtxParams->nbWorkers == 0);
                }

                *value = CCtxParams->nbWorkers;
                break;
                case ZSTD_cParameter.ZSTD_c_jobSize:
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                case ZSTD_cParameter.ZSTD_c_overlapLog:
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

                case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                {
                    *value = CCtxParams->enableDedicatedDictSearch;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                {
                    *value = (int)CCtxParams->ldmParams.enableLdm;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                {
                    *value = (int)CCtxParams->ldmParams.hashLog;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                {
                    *value = (int)CCtxParams->ldmParams.minMatchLength;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                {
                    *value = (int)CCtxParams->ldmParams.bucketSizeLog;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                {
                    *value = (int)CCtxParams->ldmParams.hashRateLog;
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam6:
                {
                    *value = (int)(CCtxParams->targetCBlockSize);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                {
                    *value = (int)(CCtxParams->srcSizeHint);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                {
                    *value = (int)(CCtxParams->inBufferMode);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                {
                    *value = (int)(CCtxParams->outBufferMode);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                {
                    *value = (int)(CCtxParams->blockDelimiters);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                {
                    *value = (int)(CCtxParams->validateSequences);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                {
                    *value = (int)(CCtxParams->splitBlocks);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                {
                    *value = (int)(CCtxParams->useRowMatchFinder);
                }

                break;
                case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                {
                    *value = (int)(CCtxParams->deterministicRefPrefix);
                }

                break;
                default:
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }
                }

            }

            return 0;
        }

        /** ZSTD_CCtx_setParametersUsingCCtxParams() :
         *  just applies `params` into `cctx`
         *  no action is performed, parameters are merely stored.
         *  If ZSTDMT is enabled, parameters are pushed to cctx->mtctx.
         *    This is possible even if a compression is ongoing.
         *    In which case, new parameters will be applied on the fly, starting with next compression job.
         */
        public static nuint ZSTD_CCtx_setParametersUsingCCtxParams(ZSTD_CCtx_s* cctx, ZSTD_CCtx_params_s* @params)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            if (cctx->cdict != null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            cctx->requestedParams = *@params;
            return 0;
        }

        /*! ZSTD_CCtx_setPledgedSrcSize() :
         *  Total input data size to be compressed as a single frame.
         *  Value will be written in frame header, unless if explicitly forbidden using ZSTD_c_contentSizeFlag.
         *  This value will also be controlled at end of frame, and trigger an error if not respected.
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Note 1 : pledgedSrcSize==0 actually means zero, aka an empty frame.
         *           In order to mean "unknown content size", pass constant ZSTD_CONTENTSIZE_UNKNOWN.
         *           ZSTD_CONTENTSIZE_UNKNOWN is default value for any new frame.
         *  Note 2 : pledgedSrcSize is only valid once, for the next frame.
         *           It's discarded at the end of the frame, and replaced by ZSTD_CONTENTSIZE_UNKNOWN.
         *  Note 3 : Whenever all input data is provided and consumed in a single round,
         *           for example with ZSTD_compress2(),
         *           or invoking immediately ZSTD_compressStream2(,,,ZSTD_e_end),
         *           this value is automatically overridden by srcSize instead.
         */
        public static nuint ZSTD_CCtx_setPledgedSrcSize(ZSTD_CCtx_s* cctx, ulong pledgedSrcSize)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            cctx->pledgedSrcSizePlusOne = pledgedSrcSize + 1;
            return 0;
        }

        /**
         * Initializes the local dict using the requested parameters.
         * NOTE: This does not use the pledged src size, because it may be used for more
         * than one compression.
         */
        private static nuint ZSTD_initLocalDict(ZSTD_CCtx_s* cctx)
        {
            ZSTD_localDict* dl = &cctx->localDict;

            if (dl->dict == null)
            {
                assert(dl->dictBuffer == null);
                assert(dl->cdict == null);
                assert(dl->dictSize == 0);
                return 0;
            }

            if (dl->cdict != null)
            {
                assert(cctx->cdict == dl->cdict);
                return 0;
            }

            assert(dl->dictSize > 0);
            assert(cctx->cdict == null);
            assert(cctx->prefixDict.dict == null);
            dl->cdict = ZSTD_createCDict_advanced2(dl->dict, dl->dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef, dl->dictContentType, &cctx->requestedParams, cctx->customMem);
            if (dl->cdict == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }

            cctx->cdict = dl->cdict;
            return 0;
        }

        /*! ZSTD_CCtx_loadDictionary_advanced() :
         *  Same as ZSTD_CCtx_loadDictionary(), but gives finer control over
         *  how to load the dictionary (by copy ? by reference ?)
         *  and how to interpret it (automatic ? force raw mode ? full mode only ?) */
        public static nuint ZSTD_CCtx_loadDictionary_advanced(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_dictContentType_e dictContentType)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            ZSTD_clearAllDicts(cctx);
            if (dict == null || dictSize == 0)
            {
                return 0;
            }

            if (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef)
            {
                cctx->localDict.dict = dict;
            }
            else
            {
                void* dictBuffer;

                if (cctx->staticSize != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                }

                dictBuffer = ZSTD_customMalloc(dictSize, cctx->customMem);
                if (dictBuffer == null)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                }

                memcpy((dictBuffer), (dict), (dictSize));
                cctx->localDict.dictBuffer = dictBuffer;
                cctx->localDict.dict = dictBuffer;
            }

            cctx->localDict.dictSize = dictSize;
            cctx->localDict.dictContentType = dictContentType;
            return 0;
        }

        /*! ZSTD_CCtx_loadDictionary_byReference() :
         *  Same as ZSTD_CCtx_loadDictionary(), but dictionary content is referenced, instead of being copied into CCtx.
         *  It saves some memory, but also requires that `dict` outlives its usage within `cctx` */
        public static nuint ZSTD_CCtx_loadDictionary_byReference(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize)
        {
            return ZSTD_CCtx_loadDictionary_advanced(cctx, dict, dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef, ZSTD_dictContentType_e.ZSTD_dct_auto);
        }

        /*! ZSTD_CCtx_loadDictionary() : Requires v1.4.0+
         *  Create an internal CDict from `dict` buffer.
         *  Decompression will have to use same dictionary.
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Special: Loading a NULL (or 0-size) dictionary invalidates previous dictionary,
         *           meaning "return to no-dictionary mode".
         *  Note 1 : Dictionary is sticky, it will be used for all future compressed frames.
         *           To return to "no-dictionary" situation, load a NULL dictionary (or reset parameters).
         *  Note 2 : Loading a dictionary involves building tables.
         *           It's also a CPU consuming operation, with non-negligible impact on latency.
         *           Tables are dependent on compression parameters, and for this reason,
         *           compression parameters can no longer be changed after loading a dictionary.
         *  Note 3 :`dict` content will be copied internally.
         *           Use experimental ZSTD_CCtx_loadDictionary_byReference() to reference content instead.
         *           In such a case, dictionary buffer must outlive its users.
         *  Note 4 : Use ZSTD_CCtx_loadDictionary_advanced()
         *           to precisely select how dictionary content must be interpreted. */
        public static nuint ZSTD_CCtx_loadDictionary(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize)
        {
            return ZSTD_CCtx_loadDictionary_advanced(cctx, dict, dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy, ZSTD_dictContentType_e.ZSTD_dct_auto);
        }

        /*! ZSTD_CCtx_refCDict() : Requires v1.4.0+
         *  Reference a prepared dictionary, to be used for all next compressed frames.
         *  Note that compression parameters are enforced from within CDict,
         *  and supersede any compression parameter previously set within CCtx.
         *  The parameters ignored are labelled as "superseded-by-cdict" in the ZSTD_cParameter enum docs.
         *  The ignored parameters will be used again if the CCtx is returned to no-dictionary mode.
         *  The dictionary will remain valid for future compressed frames using same CCtx.
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Special : Referencing a NULL CDict means "return to no-dictionary mode".
         *  Note 1 : Currently, only one dictionary can be managed.
         *           Referencing a new dictionary effectively "discards" any previous one.
         *  Note 2 : CDict is just referenced, its lifetime must outlive its usage within CCtx. */
        public static nuint ZSTD_CCtx_refCDict(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            ZSTD_clearAllDicts(cctx);
            cctx->cdict = cdict;
            return 0;
        }

        public static nuint ZSTD_CCtx_refThreadPool(ZSTD_CCtx_s* cctx, void* pool)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            cctx->pool = pool;
            return 0;
        }

        /*! ZSTD_CCtx_refPrefix() : Requires v1.4.0+
         *  Reference a prefix (single-usage dictionary) for next compressed frame.
         *  A prefix is **only used once**. Tables are discarded at end of frame (ZSTD_e_end).
         *  Decompression will need same prefix to properly regenerate data.
         *  Compressing with a prefix is similar in outcome as performing a diff and compressing it,
         *  but performs much faster, especially during decompression (compression speed is tunable with compression level).
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Special: Adding any prefix (including NULL) invalidates any previous prefix or dictionary
         *  Note 1 : Prefix buffer is referenced. It **must** outlive compression.
         *           Its content must remain unmodified during compression.
         *  Note 2 : If the intention is to diff some large src data blob with some prior version of itself,
         *           ensure that the window size is large enough to contain the entire source.
         *           See ZSTD_c_windowLog.
         *  Note 3 : Referencing a prefix involves building tables, which are dependent on compression parameters.
         *           It's a CPU consuming operation, with non-negligible impact on latency.
         *           If there is a need to use the same prefix multiple times, consider loadDictionary instead.
         *  Note 4 : By default, the prefix is interpreted as raw content (ZSTD_dct_rawContent).
         *           Use experimental ZSTD_CCtx_refPrefix_advanced() to alter dictionary interpretation. */
        public static nuint ZSTD_CCtx_refPrefix(ZSTD_CCtx_s* cctx, void* prefix, nuint prefixSize)
        {
            return ZSTD_CCtx_refPrefix_advanced(cctx, prefix, prefixSize, ZSTD_dictContentType_e.ZSTD_dct_rawContent);
        }

        /*! ZSTD_CCtx_refPrefix_advanced() :
         *  Same as ZSTD_CCtx_refPrefix(), but gives finer control over
         *  how to interpret prefix content (automatic ? force raw mode (default) ? full mode only ?) */
        public static nuint ZSTD_CCtx_refPrefix_advanced(ZSTD_CCtx_s* cctx, void* prefix, nuint prefixSize, ZSTD_dictContentType_e dictContentType)
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            ZSTD_clearAllDicts(cctx);
            if (prefix != null && prefixSize > 0)
            {
                cctx->prefixDict.dict = prefix;
                cctx->prefixDict.dictSize = prefixSize;
                cctx->prefixDict.dictContentType = dictContentType;
            }

            return 0;
        }

        /*! ZSTD_CCtx_reset() :
         *  Also dumps dictionary */
        public static nuint ZSTD_CCtx_reset(ZSTD_CCtx_s* cctx, ZSTD_ResetDirective reset)
        {
            if ((reset == ZSTD_ResetDirective.ZSTD_reset_session_only) || (reset == ZSTD_ResetDirective.ZSTD_reset_session_and_parameters))
            {
                cctx->streamStage = ZSTD_cStreamStage.zcss_init;
                cctx->pledgedSrcSizePlusOne = 0;
            }

            if ((reset == ZSTD_ResetDirective.ZSTD_reset_parameters) || (reset == ZSTD_ResetDirective.ZSTD_reset_session_and_parameters))
            {
                if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
                }

                ZSTD_clearAllDicts(cctx);
                return ZSTD_CCtxParams_reset(&cctx->requestedParams);
            }

            return 0;
        }

        /** ZSTD_checkCParams() :
            control CParam values remain within authorized range.
            @return : 0, or an error code if one value is beyond authorized range */
        public static nuint ZSTD_checkCParams(ZSTD_compressionParameters cParams)
        {

            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_windowLog, (int)(cParams.windowLog))) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }


            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_chainLog, (int)(cParams.chainLog))) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }


            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_hashLog, (int)(cParams.hashLog))) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }


            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_searchLog, (int)(cParams.searchLog))) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }


            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_minMatch, (int)(cParams.minMatch))) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }


            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_targetLength, (int)(cParams.targetLength))) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }


            {
                if ((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, (int)cParams.strategy)) == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                }

            }

            return 0;
        }

        /** ZSTD_clampCParams() :
         *  make CParam values within valid range.
         *  @return : valid CParams */
        private static ZSTD_compressionParameters ZSTD_clampCParams(ZSTD_compressionParameters cParams)
        {

            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_windowLog);

                if ((int)(cParams.windowLog) < bounds.lowerBound)
                {
                    cParams.windowLog = (uint)(bounds.lowerBound);
                }
                else if ((int)(cParams.windowLog) > bounds.upperBound)
                {
                    cParams.windowLog = (uint)(bounds.upperBound);
                }
            }


            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_chainLog);

                if ((int)(cParams.chainLog) < bounds.lowerBound)
                {
                    cParams.chainLog = (uint)(bounds.lowerBound);
                }
                else if ((int)(cParams.chainLog) > bounds.upperBound)
                {
                    cParams.chainLog = (uint)(bounds.upperBound);
                }
            }


            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_hashLog);

                if ((int)(cParams.hashLog) < bounds.lowerBound)
                {
                    cParams.hashLog = (uint)(bounds.lowerBound);
                }
                else if ((int)(cParams.hashLog) > bounds.upperBound)
                {
                    cParams.hashLog = (uint)(bounds.upperBound);
                }
            }


            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_searchLog);

                if ((int)(cParams.searchLog) < bounds.lowerBound)
                {
                    cParams.searchLog = (uint)(bounds.lowerBound);
                }
                else if ((int)(cParams.searchLog) > bounds.upperBound)
                {
                    cParams.searchLog = (uint)(bounds.upperBound);
                }
            }


            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_minMatch);

                if ((int)(cParams.minMatch) < bounds.lowerBound)
                {
                    cParams.minMatch = (uint)(bounds.lowerBound);
                }
                else if ((int)(cParams.minMatch) > bounds.upperBound)
                {
                    cParams.minMatch = (uint)(bounds.upperBound);
                }
            }


            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_targetLength);

                if ((int)(cParams.targetLength) < bounds.lowerBound)
                {
                    cParams.targetLength = (uint)(bounds.lowerBound);
                }
                else if ((int)(cParams.targetLength) > bounds.upperBound)
                {
                    cParams.targetLength = (uint)(bounds.upperBound);
                }
            }


            {
                ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_strategy);

                if ((int)(cParams.strategy) < bounds.lowerBound)
                {
                    cParams.strategy = (ZSTD_strategy)(bounds.lowerBound);
                }
                else if ((int)(cParams.strategy) > bounds.upperBound)
                {
                    cParams.strategy = (ZSTD_strategy)(bounds.upperBound);
                }
            }

            return cParams;
        }

        /** ZSTD_cycleLog() :
         *  condition for correct operation : hashLog > 1 */
        public static uint ZSTD_cycleLog(uint hashLog, ZSTD_strategy strat)
        {
            uint btScale = ((((uint)(strat) >= (uint)(ZSTD_strategy.ZSTD_btlazy2))) ? 1U : 0U);

            return hashLog - btScale;
        }

        /** ZSTD_dictAndWindowLog() :
         * Returns an adjusted window log that is large enough to fit the source and the dictionary.
         * The zstd format says that the entire dictionary is valid if one byte of the dictionary
         * is within the window. So the hashLog and chainLog should be large enough to reference both
         * the dictionary and the window. So we must use this adjusted dictAndWindowLog when downsizing
         * the hashLog and windowLog.
         * NOTE: srcSize must not be ZSTD_CONTENTSIZE_UNKNOWN.
         */
        private static uint ZSTD_dictAndWindowLog(uint windowLog, ulong srcSize, ulong dictSize)
        {
            ulong maxWindowSize = 1UL << (unchecked((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)));

            if (dictSize == 0)
            {
                return windowLog;
            }

            assert(windowLog <= (uint)(((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))));
            assert(srcSize != (unchecked(0UL - 1)));

            {
                ulong windowSize = 1UL << (int)windowLog;
                ulong dictAndWindowSize = dictSize + windowSize;

                if (windowSize >= dictSize + srcSize)
                {
                    return windowLog;
                }
                else if (dictAndWindowSize >= maxWindowSize)
                {
                    return (uint)(((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)));
                }
                else
                {
                    return ZSTD_highbit32((uint)(dictAndWindowSize) - 1) + 1;
                }
            }
        }

        /** ZSTD_adjustCParams_internal() :
         *  optimize `cPar` for a specified input (`srcSize` and `dictSize`).
         *  mostly downsize to reduce memory consumption and initialization latency.
         * `srcSize` can be ZSTD_CONTENTSIZE_UNKNOWN when not known.
         * `mode` is the mode for parameter adjustment. See docs for `ZSTD_cParamMode_e`.
         *  note : `srcSize==0` means 0!
         *  condition : cPar is presumed validated (can be checked using ZSTD_checkCParams()). */
        private static ZSTD_compressionParameters ZSTD_adjustCParams_internal(ZSTD_compressionParameters cPar, ulong srcSize, nuint dictSize, ZSTD_cParamMode_e mode)
        {
            ulong minSrcSize = 513;
            ulong maxWindowResize = 1UL << (((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)) - 1);

            assert(ZSTD_checkCParams(cPar) == 0);
            switch (mode)
            {
                case ZSTD_cParamMode_e.ZSTD_cpm_unknown:
                case ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict:
                {
                    break;
                }

                case ZSTD_cParamMode_e.ZSTD_cpm_createCDict:
                {
                    if (dictSize != 0 && srcSize == (unchecked(0UL - 1)))
                    {
                        srcSize = minSrcSize;
                    }
                }

                break;
                case ZSTD_cParamMode_e.ZSTD_cpm_attachDict:
                {
                    dictSize = 0;
                }

                break;
                default:
                {
                    assert(0 != 0);
                }

                break;
            }

            if ((srcSize < maxWindowResize) && (dictSize < maxWindowResize))
            {
                uint tSize = (uint)(srcSize + dictSize);
                uint hashSizeMin = (uint)(1 << 6);
                uint srcLog = (uint)((tSize < hashSizeMin) ? 6 : ZSTD_highbit32(tSize - 1) + 1);

                if (cPar.windowLog > srcLog)
                {
                    cPar.windowLog = srcLog;
                }
            }

            if (srcSize != (unchecked(0UL - 1)))
            {
                uint dictAndWindowLog = ZSTD_dictAndWindowLog(cPar.windowLog, (ulong)(srcSize), (ulong)(dictSize));
                uint cycleLog = ZSTD_cycleLog(cPar.chainLog, cPar.strategy);

                if (cPar.hashLog > dictAndWindowLog + 1)
                {
                    cPar.hashLog = dictAndWindowLog + 1;
                }

                if (cycleLog > dictAndWindowLog)
                {
                    cPar.chainLog -= (cycleLog - dictAndWindowLog);
                }
            }

            if (cPar.windowLog < 10)
            {
                cPar.windowLog = 10;
            }

            return cPar;
        }

        /*! ZSTD_adjustCParams() :
         *  optimize params for a given `srcSize` and `dictSize`.
         * `srcSize` can be unknown, in which case use ZSTD_CONTENTSIZE_UNKNOWN.
         * `dictSize` must be `0` when there is no dictionary.
         *  cPar can be invalid : all parameters will be clamped within valid range in the @return struct.
         *  This function never fails (wide contract) */
        public static ZSTD_compressionParameters ZSTD_adjustCParams(ZSTD_compressionParameters cPar, ulong srcSize, nuint dictSize)
        {
            cPar = ZSTD_clampCParams(cPar);
            if (srcSize == 0)
            {
                srcSize = (unchecked(0UL - 1));
            }

            return ZSTD_adjustCParams_internal(cPar, srcSize, dictSize, ZSTD_cParamMode_e.ZSTD_cpm_unknown);
        }

        private static void ZSTD_overrideCParams(ZSTD_compressionParameters* cParams, ZSTD_compressionParameters* overrides)
        {
            if (overrides->windowLog != 0)
            {
                cParams->windowLog = overrides->windowLog;
            }

            if (overrides->hashLog != 0)
            {
                cParams->hashLog = overrides->hashLog;
            }

            if (overrides->chainLog != 0)
            {
                cParams->chainLog = overrides->chainLog;
            }

            if (overrides->searchLog != 0)
            {
                cParams->searchLog = overrides->searchLog;
            }

            if (overrides->minMatch != 0)
            {
                cParams->minMatch = overrides->minMatch;
            }

            if (overrides->targetLength != 0)
            {
                cParams->targetLength = overrides->targetLength;
            }

            if (overrides->strategy != default)
            {
                cParams->strategy = overrides->strategy;
            }
        }

        /* ZSTD_getCParamsFromCCtxParams() :
         * cParams are built depending on compressionLevel, src size hints,
         * LDM and manually set compression parameters.
         * Note: srcSizeHint == 0 means 0!
         */
        public static ZSTD_compressionParameters ZSTD_getCParamsFromCCtxParams(ZSTD_CCtx_params_s* CCtxParams, ulong srcSizeHint, nuint dictSize, ZSTD_cParamMode_e mode)
        {
            ZSTD_compressionParameters cParams;

            if (srcSizeHint == (unchecked(0UL - 1)) && CCtxParams->srcSizeHint > 0)
            {
                srcSizeHint = (ulong)CCtxParams->srcSizeHint;
            }

            cParams = ZSTD_getCParams_internal(CCtxParams->compressionLevel, srcSizeHint, dictSize, mode);
            if (CCtxParams->ldmParams.enableLdm != 0)
            {
                cParams.windowLog = 27;
            }

            ZSTD_overrideCParams(&cParams, &CCtxParams->cParams);
            assert((ZSTD_checkCParams(cParams)) == 0);
            return ZSTD_adjustCParams_internal(cParams, srcSizeHint, dictSize, mode);
        }

        private static nuint ZSTD_sizeof_matchState(ZSTD_compressionParameters* cParams, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, uint enableDedicatedDictSearch, uint forCCtx)
        {
            nuint chainSize = (ZSTD_allocateChainTable(cParams->strategy, useRowMatchFinder, ((enableDedicatedDictSearch != 0 && forCCtx == 0) ? 1U : 0U))) != 0 ? ((nuint)(1) << (int)cParams->chainLog) : 0;
            nuint hSize = ((nuint)(1)) << (int)cParams->hashLog;
            uint hashLog3 = (uint)((forCCtx != 0 && cParams->minMatch == 3) ? ((17) < (cParams->windowLog) ? (17) : (cParams->windowLog)) : 0);
            nuint h3Size = hashLog3 != 0 ? ((nuint)(1)) << (int)hashLog3 : 0;
            nuint tableSpace = chainSize * (nuint)(4) + hSize * (nuint)(4) + h3Size * (nuint)(4);
            nuint optPotentialSpace = ZSTD_cwksp_aligned_alloc_size((uint)((52 + 1)) * (nuint)(4)) + ZSTD_cwksp_aligned_alloc_size((uint)((35 + 1)) * (nuint)(4)) + ZSTD_cwksp_aligned_alloc_size((uint)((31 + 1)) * (nuint)(4)) + ZSTD_cwksp_aligned_alloc_size((uint)((1 << 8)) * (nuint)(4)) + ZSTD_cwksp_aligned_alloc_size((uint)(((1 << 12) + 1)) * (nuint)(8)) + ZSTD_cwksp_aligned_alloc_size((uint)(((1 << 12) + 1)) * (nuint)(28));
            nuint lazyAdditionalSpace = (ZSTD_rowMatchFinderUsed(cParams->strategy, useRowMatchFinder)) != 0 ? ZSTD_cwksp_aligned_alloc_size(hSize * (nuint)(2)) : 0;
            nuint optSpace = (forCCtx != 0 && (cParams->strategy >= ZSTD_strategy.ZSTD_btopt)) ? optPotentialSpace : 0;
            nuint slackSpace = ZSTD_cwksp_slack_space_required();

            assert(useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
            return tableSpace + optSpace + slackSpace + lazyAdditionalSpace;
        }

        private static nuint ZSTD_estimateCCtxSize_usingCCtxParams_internal(ZSTD_compressionParameters* cParams, ldmParams_t* ldmParams, int isStatic, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, nuint buffInSize, nuint buffOutSize, ulong pledgedSrcSize)
        {
            nuint windowSize = ((1) > ((nuint)((((ulong)(1) << (int)cParams->windowLog)) < (pledgedSrcSize) ? (((ulong)(1) << (int)cParams->windowLog)) : (pledgedSrcSize))) ? (1) : ((nuint)((((ulong)(1) << (int)cParams->windowLog)) < (pledgedSrcSize) ? (((ulong)(1) << (int)cParams->windowLog)) : (pledgedSrcSize))));
            nuint blockSize = (nuint)((uint)(((1 << 17))) < (windowSize) ? ((1 << 17)) : (windowSize));
            uint divider = (uint)((cParams->minMatch == 3) ? 3 : 4);
            nuint maxNbSeq = blockSize / divider;
            nuint tokenSpace = ZSTD_cwksp_alloc_size(32 + blockSize) + ZSTD_cwksp_aligned_alloc_size(maxNbSeq * (nuint)(8)) + 3 * ZSTD_cwksp_alloc_size(maxNbSeq * (nuint)(1));
            nuint entropySpace = ZSTD_cwksp_alloc_size(((uint)(((6 << 10) + 256)) + ((nuint)(4) * (uint)((((35) > (52) ? (35) : (52)) + 2)))));
            nuint blockStateSpace = 2 * ZSTD_cwksp_alloc_size((nuint)(4592));
            nuint matchStateSize = ZSTD_sizeof_matchState(cParams, useRowMatchFinder, 0, 1);
            nuint ldmSpace = ZSTD_ldm_getTableSize(*ldmParams);
            nuint maxNbLdmSeq = ZSTD_ldm_getMaxNbSeq(*ldmParams, blockSize);
            nuint ldmSeqSpace = ldmParams->enableLdm != 0 ? ZSTD_cwksp_aligned_alloc_size(maxNbLdmSeq * (nuint)(12)) : 0;
            nuint bufferSpace = ZSTD_cwksp_alloc_size(buffInSize) + ZSTD_cwksp_alloc_size(buffOutSize);
            nuint cctxSpace = isStatic != 0 ? ZSTD_cwksp_alloc_size((nuint)(sizeof(ZSTD_CCtx_s))) : 0;
            nuint neededSpace = cctxSpace + entropySpace + blockStateSpace + ldmSpace + ldmSeqSpace + matchStateSize + tokenSpace + bufferSpace;

            return neededSpace;
        }

        public static nuint ZSTD_estimateCCtxSize_usingCCtxParams(ZSTD_CCtx_params_s* @params)
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParamsFromCCtxParams(@params, (unchecked(0UL - 1)), 0, ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict);
            ZSTD_useRowMatchFinderMode_e useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(@params->useRowMatchFinder, &cParams);

            if (@params->nbWorkers > 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            return ZSTD_estimateCCtxSize_usingCCtxParams_internal(&cParams, &@params->ldmParams, 1, useRowMatchFinder, 0, 0, (unchecked(0UL - 1)));
        }

        public static nuint ZSTD_estimateCCtxSize_usingCParams(ZSTD_compressionParameters cParams)
        {
            ZSTD_CCtx_params_s initialParams = ZSTD_makeCCtxParamsFromCParams(cParams);

            if ((ZSTD_rowMatchFinderSupported(cParams.strategy)) != 0)
            {
                nuint noRowCCtxSize;
                nuint rowCCtxSize;

                initialParams.useRowMatchFinder = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_disableRowMatchFinder;
                noRowCCtxSize = ZSTD_estimateCCtxSize_usingCCtxParams(&initialParams);
                initialParams.useRowMatchFinder = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder;
                rowCCtxSize = ZSTD_estimateCCtxSize_usingCCtxParams(&initialParams);
                return ((noRowCCtxSize) > (rowCCtxSize) ? (noRowCCtxSize) : (rowCCtxSize));
            }
            else
            {
                return ZSTD_estimateCCtxSize_usingCCtxParams(&initialParams);
            }
        }

        private static nuint ZSTD_estimateCCtxSize_internal(int compressionLevel)
        {
            int tier = 0;
            nuint largestSize = 0;


            for (; tier < 4; ++tier)
            {
                ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, srcSizeTiers[tier], 0, ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict);

                largestSize = ((ZSTD_estimateCCtxSize_usingCParams(cParams)) > (largestSize) ? (ZSTD_estimateCCtxSize_usingCParams(cParams)) : (largestSize));
            }

            return largestSize;
        }

        /*! ZSTD_estimate*() :
         *  These functions make it possible to estimate memory usage
         *  of a future {D,C}Ctx, before its creation.
         *
         *  ZSTD_estimateCCtxSize() will provide a memory budget large enough
         *  for any compression level up to selected one.
         *  Note : Unlike ZSTD_estimateCStreamSize*(), this estimate
         *         does not include space for a window buffer.
         *         Therefore, the estimation is only guaranteed for single-shot compressions, not streaming.
         *  The estimate will assume the input may be arbitrarily large,
         *  which is the worst case.
         *
         *  When srcSize can be bound by a known and rather "small" value,
         *  this fact can be used to provide a tighter estimation
         *  because the CCtx compression context will need less memory.
         *  This tighter estimation can be provided by more advanced functions
         *  ZSTD_estimateCCtxSize_usingCParams(), which can be used in tandem with ZSTD_getCParams(),
         *  and ZSTD_estimateCCtxSize_usingCCtxParams(), which can be used in tandem with ZSTD_CCtxParams_setParameter().
         *  Both can be used to estimate memory using custom compression parameters and arbitrary srcSize limits.
         *
         *  Note 2 : only single-threaded compression is supported.
         *  ZSTD_estimateCCtxSize_usingCCtxParams() will return an error code if ZSTD_c_nbWorkers is >= 1.
         */
        public static nuint ZSTD_estimateCCtxSize(int compressionLevel)
        {
            int level;
            nuint memBudget = 0;

            for (level = ((compressionLevel) < (1) ? (compressionLevel) : (1)); level <= compressionLevel; level++)
            {
                nuint newMB = ZSTD_estimateCCtxSize_internal(level);

                if (newMB > memBudget)
                {
                    memBudget = newMB;
                }
            }

            return memBudget;
        }

        public static nuint ZSTD_estimateCStreamSize_usingCCtxParams(ZSTD_CCtx_params_s* @params)
        {
            if (@params->nbWorkers > 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }


            {
                ZSTD_compressionParameters cParams = ZSTD_getCParamsFromCCtxParams(@params, (unchecked(0UL - 1)), 0, ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict);
                nuint blockSize = (nuint)((uint)(((1 << 17))) < ((nuint)(1) << (int)cParams.windowLog) ? ((1 << 17)) : ((nuint)(1) << (int)cParams.windowLog));
                nuint inBuffSize = (@params->inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered) ? ((nuint)(1) << (int)cParams.windowLog) + blockSize : 0;
                nuint outBuffSize = (@params->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered) ? ZSTD_compressBound(blockSize) + 1 : 0;
                ZSTD_useRowMatchFinderMode_e useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(@params->useRowMatchFinder, &@params->cParams);

                return ZSTD_estimateCCtxSize_usingCCtxParams_internal(&cParams, &@params->ldmParams, 1, useRowMatchFinder, inBuffSize, outBuffSize, (unchecked(0UL - 1)));
            }
        }

        public static nuint ZSTD_estimateCStreamSize_usingCParams(ZSTD_compressionParameters cParams)
        {
            ZSTD_CCtx_params_s initialParams = ZSTD_makeCCtxParamsFromCParams(cParams);

            if ((ZSTD_rowMatchFinderSupported(cParams.strategy)) != 0)
            {
                nuint noRowCCtxSize;
                nuint rowCCtxSize;

                initialParams.useRowMatchFinder = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_disableRowMatchFinder;
                noRowCCtxSize = ZSTD_estimateCStreamSize_usingCCtxParams(&initialParams);
                initialParams.useRowMatchFinder = ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder;
                rowCCtxSize = ZSTD_estimateCStreamSize_usingCCtxParams(&initialParams);
                return ((noRowCCtxSize) > (rowCCtxSize) ? (noRowCCtxSize) : (rowCCtxSize));
            }
            else
            {
                return ZSTD_estimateCStreamSize_usingCCtxParams(&initialParams);
            }
        }

        private static nuint ZSTD_estimateCStreamSize_internal(int compressionLevel)
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, (unchecked(0UL - 1)), 0, ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict);

            return ZSTD_estimateCStreamSize_usingCParams(cParams);
        }

        /*! ZSTD_estimateCStreamSize() :
         *  ZSTD_estimateCStreamSize() will provide a budget large enough for any compression level up to selected one.
         *  It will also consider src size to be arbitrarily "large", which is worst case.
         *  If srcSize is known to always be small, ZSTD_estimateCStreamSize_usingCParams() can provide a tighter estimation.
         *  ZSTD_estimateCStreamSize_usingCParams() can be used in tandem with ZSTD_getCParams() to create cParams from compressionLevel.
         *  ZSTD_estimateCStreamSize_usingCCtxParams() can be used in tandem with ZSTD_CCtxParams_setParameter(). Only single-threaded compression is supported. This function will return an error code if ZSTD_c_nbWorkers is >= 1.
         *  Note : CStream size estimation is only correct for single-threaded compression.
         *  ZSTD_DStream memory budget depends on window Size.
         *  This information can be passed manually, using ZSTD_estimateDStreamSize,
         *  or deducted from a valid frame Header, using ZSTD_estimateDStreamSize_fromFrame();
         *  Note : if streaming is init with function ZSTD_init?Stream_usingDict(),
         *         an internal ?Dict will be created, which additional size is not estimated here.
         *         In this case, get total size by adding ZSTD_estimate?DictSize */
        public static nuint ZSTD_estimateCStreamSize(int compressionLevel)
        {
            int level;
            nuint memBudget = 0;

            for (level = ((compressionLevel) < (1) ? (compressionLevel) : (1)); level <= compressionLevel; level++)
            {
                nuint newMB = ZSTD_estimateCStreamSize_internal(level);

                if (newMB > memBudget)
                {
                    memBudget = newMB;
                }
            }

            return memBudget;
        }

        /* ZSTD_getFrameProgression():
         * tells how much data has been consumed (input) and produced (output) for current frame.
         * able to count progression inside worker threads (non-blocking mode).
         */
        public static ZSTD_frameProgression ZSTD_getFrameProgression(ZSTD_CCtx_s* cctx)
        {

            {
                ZSTD_frameProgression fp;
                nuint buffered = (cctx->inBuff == null) ? 0 : cctx->inBuffPos - cctx->inToCompress;

                if (buffered != 0)
                {
                    assert(cctx->inBuffPos >= cctx->inToCompress);
                }

                assert(buffered <= (uint)((1 << 17)));
                fp.ingested = cctx->consumedSrcSize + buffered;
                fp.consumed = cctx->consumedSrcSize;
                fp.produced = cctx->producedCSize;
                fp.flushed = cctx->producedCSize;
                fp.currentJobID = 0;
                fp.nbActiveWorkers = 0;
                return fp;
            }
        }

        /*! ZSTD_toFlushNow()
         *  Only useful for multithreading scenarios currently (nbWorkers >= 1).
         */
        public static nuint ZSTD_toFlushNow(ZSTD_CCtx_s* cctx)
        {
            return 0;
        }

        private static void ZSTD_assertEqualCParams(ZSTD_compressionParameters cParams1, ZSTD_compressionParameters cParams2)
        {
            assert(cParams1.windowLog == cParams2.windowLog);
            assert(cParams1.chainLog == cParams2.chainLog);
            assert(cParams1.hashLog == cParams2.hashLog);
            assert(cParams1.searchLog == cParams2.searchLog);
            assert(cParams1.minMatch == cParams2.minMatch);
            assert(cParams1.targetLength == cParams2.targetLength);
            assert(cParams1.strategy == cParams2.strategy);
        }

        public static void ZSTD_reset_compressedBlockState(ZSTD_compressedBlockState_t* bs)
        {
            int i;

            for (i = 0; i < 3; ++i)
            {
                bs->rep[i] = repStartValue[i];
            }

            bs->entropy.huf.repeatMode = HUF_repeat.HUF_repeat_none;
            bs->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_none;
            bs->entropy.fse.matchlength_repeatMode = FSE_repeat.FSE_repeat_none;
            bs->entropy.fse.litlength_repeatMode = FSE_repeat.FSE_repeat_none;
        }

        /*! ZSTD_invalidateMatchState()
         *  Invalidate all the matches in the match finder tables.
         *  Requires nextSrc and base to be set (can be NULL).
         */
        private static void ZSTD_invalidateMatchState(ZSTD_matchState_t* ms)
        {
            ZSTD_window_clear(&ms->window);
            ms->nextToUpdate = ms->window.dictLimit;
            ms->loadedDictEnd = 0;
            ms->opt.litLengthSum = 0;
            ms->dictMatchState = null;
        }

        private static nuint ZSTD_reset_matchState(ZSTD_matchState_t* ms, ZSTD_cwksp* ws, ZSTD_compressionParameters* cParams, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, ZSTD_compResetPolicy_e crp, ZSTD_indexResetPolicy_e forceResetIndex, ZSTD_resetTarget_e forWho)
        {
            nuint chainSize = (ZSTD_allocateChainTable(cParams->strategy, useRowMatchFinder, ((ms->dedicatedDictSearch != 0 && (forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CDict)) ? 1U : 0U))) != 0 ? ((nuint)(1) << (int)cParams->chainLog) : 0;
            nuint hSize = ((nuint)(1)) << (int)cParams->hashLog;
            uint hashLog3 = (uint)(((forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx) && cParams->minMatch == 3) ? ((17) < (cParams->windowLog) ? (17) : (cParams->windowLog)) : 0);
            nuint h3Size = hashLog3 != 0 ? ((nuint)(1)) << (int)hashLog3 : 0;

            assert(useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
            if (forceResetIndex == ZSTD_indexResetPolicy_e.ZSTDirp_reset)
            {
                ZSTD_window_init(&ms->window);
                ZSTD_cwksp_mark_tables_dirty(ws);
            }

            ms->hashLog3 = hashLog3;
            ZSTD_invalidateMatchState(ms);
            assert((ZSTD_cwksp_reserve_failed(ws)) == 0);
            ZSTD_cwksp_clear_tables(ws);
            ms->hashTable = (uint*)(ZSTD_cwksp_reserve_table(ws, hSize * (nuint)(sizeof(uint))));
            ms->chainTable = (uint*)(ZSTD_cwksp_reserve_table(ws, chainSize * (nuint)(sizeof(uint))));
            ms->hashTable3 = (uint*)(ZSTD_cwksp_reserve_table(ws, h3Size * (nuint)(sizeof(uint))));
            if ((ZSTD_cwksp_reserve_failed(ws)) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }

            if (crp != ZSTD_compResetPolicy_e.ZSTDcrp_leaveDirty)
            {
                ZSTD_cwksp_clean_tables(ws);
            }

            if ((forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx) && (cParams->strategy >= ZSTD_strategy.ZSTD_btopt))
            {
                ms->opt.litFreq = (uint*)(ZSTD_cwksp_reserve_aligned(ws, (uint)((1 << 8)) * (nuint)(sizeof(uint))));
                ms->opt.litLengthFreq = (uint*)(ZSTD_cwksp_reserve_aligned(ws, (uint)((35 + 1)) * (nuint)(sizeof(uint))));
                ms->opt.matchLengthFreq = (uint*)(ZSTD_cwksp_reserve_aligned(ws, (uint)((52 + 1)) * (nuint)(sizeof(uint))));
                ms->opt.offCodeFreq = (uint*)(ZSTD_cwksp_reserve_aligned(ws, (uint)((31 + 1)) * (nuint)(sizeof(uint))));
                ms->opt.matchTable = (ZSTD_match_t*)(ZSTD_cwksp_reserve_aligned(ws, (uint)(((1 << 12) + 1)) * (nuint)(sizeof(ZSTD_match_t))));
                ms->opt.priceTable = (ZSTD_optimal_t*)(ZSTD_cwksp_reserve_aligned(ws, (uint)(((1 << 12) + 1)) * (nuint)(sizeof(ZSTD_optimal_t))));
            }

            if ((ZSTD_rowMatchFinderUsed(cParams->strategy, useRowMatchFinder)) != 0)
            {

                {
                    nuint tagTableSize = hSize * (nuint)(2);

                    ms->tagTable = (ushort*)(ZSTD_cwksp_reserve_aligned(ws, tagTableSize));
                    if (ms->tagTable != null)
                    {
                        memset((void*)(ms->tagTable), (0), (tagTableSize));
                    }
                }


                {
                    uint rowLog = (uint)(cParams->searchLog < 5 ? 4 : 5);

                    assert(cParams->hashLog > rowLog);
                    ms->rowHashLog = cParams->hashLog - rowLog;
                }
            }

            ms->cParams = *cParams;
            if ((ZSTD_cwksp_reserve_failed(ws)) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }

            return 0;
        }

        private static int ZSTD_indexTooCloseToMax(ZSTD_window_t w)
        {
            return (((nuint)(w.nextSrc - w.@base) > (((3U << 29) + (1U << ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)))) - (uint)((16 * (1 << 20))))) ? 1 : 0);
        }

        /** ZSTD_dictTooBig():
         * When dictionaries are larger than ZSTD_CHUNKSIZE_MAX they can't be loaded in
         * one go generically. So we ensure that in that case we reset the tables to zero,
         * so that we can load as much of the dictionary as possible.
         */
        private static int ZSTD_dictTooBig(nuint loadedDictSize)
        {
            return ((loadedDictSize > ((unchecked((uint)(-1))) - ((3U << 29) + (1U << ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)))))) ? 1 : 0);
        }

        /*! ZSTD_resetCCtx_internal() :
         * @param loadedDictSize The size of the dictionary to be loaded
         * into the context, if any. If no dictionary is used, or the
         * dictionary is being attached / copied, then pass 0.
         * note : `params` are assumed fully validated at this stage.
         */
        private static nuint ZSTD_resetCCtx_internal(ZSTD_CCtx_s* zc, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize, nuint loadedDictSize, ZSTD_compResetPolicy_e crp, ZSTD_buffered_policy_e zbuff)
        {
            ZSTD_cwksp* ws = &zc->workspace;

            assert((ERR_isError(ZSTD_checkCParams(@params->cParams))) == 0);
            zc->isFirstBlock = 1;
            zc->appliedParams = *@params;
            @params = &zc->appliedParams;
            assert(@params->useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
            if (@params->ldmParams.enableLdm != 0)
            {
                ZSTD_ldm_adjustParameters(&zc->appliedParams.ldmParams, &@params->cParams);
                assert(@params->ldmParams.hashLog >= @params->ldmParams.bucketSizeLog);
                assert(@params->ldmParams.hashRateLog < 32);
            }


            {
                nuint windowSize = ((1) > ((nuint)((((ulong)(1) << (int)@params->cParams.windowLog)) < (pledgedSrcSize) ? (((ulong)(1) << (int)@params->cParams.windowLog)) : (pledgedSrcSize))) ? (1) : ((nuint)((((ulong)(1) << (int)@params->cParams.windowLog)) < (pledgedSrcSize) ? (((ulong)(1) << (int)@params->cParams.windowLog)) : (pledgedSrcSize))));
                nuint blockSize = (nuint)((uint)(((1 << 17))) < (windowSize) ? ((1 << 17)) : (windowSize));
                uint divider = (uint)((@params->cParams.minMatch == 3) ? 3 : 4);
                nuint maxNbSeq = blockSize / divider;
                nuint buffOutSize = (zbuff == ZSTD_buffered_policy_e.ZSTDb_buffered && @params->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered) ? ZSTD_compressBound(blockSize) + 1 : 0;
                nuint buffInSize = (zbuff == ZSTD_buffered_policy_e.ZSTDb_buffered && @params->inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered) ? windowSize + blockSize : 0;
                nuint maxNbLdmSeq = ZSTD_ldm_getMaxNbSeq(@params->ldmParams, blockSize);
                int indexTooClose = ZSTD_indexTooCloseToMax(zc->blockState.matchState.window);
                int dictTooBig = ZSTD_dictTooBig(loadedDictSize);
                ZSTD_indexResetPolicy_e needsIndexReset = (indexTooClose != 0 || dictTooBig != 0 || zc->initialized == 0) ? ZSTD_indexResetPolicy_e.ZSTDirp_reset : ZSTD_indexResetPolicy_e.ZSTDirp_continue;
                nuint neededSpace = ZSTD_estimateCCtxSize_usingCCtxParams_internal(&@params->cParams, &@params->ldmParams, ((zc->staticSize != 0) ? 1 : 0), @params->useRowMatchFinder, buffInSize, buffOutSize, pledgedSrcSize);
                int resizeWorkspace;


                {
                    nuint err_code = (neededSpace);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (zc->staticSize == 0)
                {
                    ZSTD_cwksp_bump_oversized_duration(ws, 0);
                }


                {
                    int workspaceTooSmall = ((ZSTD_cwksp_sizeof(ws) < neededSpace) ? 1 : 0);
                    int workspaceWasteful = ZSTD_cwksp_check_wasteful(ws, neededSpace);

                    resizeWorkspace = ((workspaceTooSmall != 0 || workspaceWasteful != 0) ? 1 : 0);
                    if (resizeWorkspace != 0)
                    {
                        if (zc->staticSize != 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                        }

                        needsIndexReset = ZSTD_indexResetPolicy_e.ZSTDirp_reset;
                        ZSTD_cwksp_free(ws, zc->customMem);

                        {
                            nuint err_code = (ZSTD_cwksp_create(ws, neededSpace, zc->customMem));

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        assert((ZSTD_cwksp_check_available(ws, 2 * (nuint)(sizeof(ZSTD_compressedBlockState_t)))) != 0);
                        zc->blockState.prevCBlock = (ZSTD_compressedBlockState_t*)(ZSTD_cwksp_reserve_object(ws, (nuint)(sizeof(ZSTD_compressedBlockState_t))));
                        if (zc->blockState.prevCBlock == null)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                        }

                        zc->blockState.nextCBlock = (ZSTD_compressedBlockState_t*)(ZSTD_cwksp_reserve_object(ws, (nuint)(sizeof(ZSTD_compressedBlockState_t))));
                        if (zc->blockState.nextCBlock == null)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                        }

                        zc->entropyWorkspace = (uint*)(ZSTD_cwksp_reserve_object(ws, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2))))));
                        if (zc->blockState.nextCBlock == null)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                        }

                    }
                }

                ZSTD_cwksp_clear(ws);
                zc->blockState.matchState.cParams = @params->cParams;
                zc->pledgedSrcSizePlusOne = pledgedSrcSize + 1;
                zc->consumedSrcSize = 0;
                zc->producedCSize = 0;
                if (pledgedSrcSize == (unchecked(0UL - 1)))
                {
                    zc->appliedParams.fParams.contentSizeFlag = 0;
                }

                zc->blockSize = blockSize;
                XXH64_reset(&zc->xxhState, 0);
                zc->stage = ZSTD_compressionStage_e.ZSTDcs_init;
                zc->dictID = 0;
                zc->dictContentSize = 0;
                ZSTD_reset_compressedBlockState(zc->blockState.prevCBlock);
                zc->seqStore.litStart = ZSTD_cwksp_reserve_buffer(ws, blockSize + 32);
                zc->seqStore.maxNbLit = blockSize;
                zc->bufferedPolicy = zbuff;
                zc->inBuffSize = buffInSize;
                zc->inBuff = (sbyte*)(ZSTD_cwksp_reserve_buffer(ws, buffInSize));
                zc->outBuffSize = buffOutSize;
                zc->outBuff = (sbyte*)(ZSTD_cwksp_reserve_buffer(ws, buffOutSize));
                if (@params->ldmParams.enableLdm != 0)
                {
                    nuint numBuckets = ((nuint)(1)) << (int)(@params->ldmParams.hashLog - @params->ldmParams.bucketSizeLog);

                    zc->ldmState.bucketOffsets = ZSTD_cwksp_reserve_buffer(ws, numBuckets);
                    memset((void*)(zc->ldmState.bucketOffsets), (0), (numBuckets));
                }

                ZSTD_referenceExternalSequences(zc, (rawSeq*)null, 0);
                zc->seqStore.maxNbSeq = maxNbSeq;
                zc->seqStore.llCode = ZSTD_cwksp_reserve_buffer(ws, maxNbSeq * (nuint)(sizeof(byte)));
                zc->seqStore.mlCode = ZSTD_cwksp_reserve_buffer(ws, maxNbSeq * (nuint)(sizeof(byte)));
                zc->seqStore.ofCode = ZSTD_cwksp_reserve_buffer(ws, maxNbSeq * (nuint)(sizeof(byte)));
                zc->seqStore.sequencesStart = (seqDef_s*)(ZSTD_cwksp_reserve_aligned(ws, maxNbSeq * (nuint)(sizeof(seqDef_s))));

                {
                    nuint err_code = (ZSTD_reset_matchState(&zc->blockState.matchState, ws, &@params->cParams, @params->useRowMatchFinder, crp, needsIndexReset, ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (@params->ldmParams.enableLdm != 0)
                {
                    nuint ldmHSize = ((nuint)(1)) << (int)@params->ldmParams.hashLog;

                    zc->ldmState.hashTable = (ldmEntry_t*)(ZSTD_cwksp_reserve_aligned(ws, ldmHSize * (nuint)(sizeof(ldmEntry_t))));
                    memset((void*)(zc->ldmState.hashTable), (0), (ldmHSize * (nuint)(sizeof(ldmEntry_t))));
                    zc->ldmSequences = (rawSeq*)(ZSTD_cwksp_reserve_aligned(ws, maxNbLdmSeq * (nuint)(sizeof(rawSeq))));
                    zc->maxNbLdmSequences = maxNbLdmSeq;
                    ZSTD_window_init(&zc->ldmState.window);
                    zc->ldmState.loadedDictEnd = 0;
                }

                assert((ZSTD_cwksp_estimated_space_within_bounds(ws, neededSpace, resizeWorkspace)) != 0);
                zc->initialized = 1;
                return 0;
            }
        }

        /* ZSTD_invalidateRepCodes() :
         * ensures next compression will not use repcodes from previous block.
         * Note : only works with regular variant;
         *        do not use with extDict variant ! */
        public static void ZSTD_invalidateRepCodes(ZSTD_CCtx_s* cctx)
        {
            int i;

            for (i = 0; i < 3; i++)
            {
                cctx->blockState.prevCBlock->rep[i] = 0;
            }

            assert((ZSTD_window_hasExtDict(cctx->blockState.matchState.window)) == 0);
        }

        public static nuint* attachDictSizeCutoffs = GetArrayPointer(new nuint[10]
        {
            8 * (1 << 10),
            8 * (1 << 10),
            16 * (1 << 10),
            32 * (1 << 10),
            32 * (1 << 10),
            32 * (1 << 10),
            32 * (1 << 10),
            32 * (1 << 10),
            8 * (1 << 10),
            8 * (1 << 10),
        });

        private static int ZSTD_shouldAttachDict(ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize)
        {
            nuint cutoff = attachDictSizeCutoffs[(int)cdict->matchState.cParams.strategy];
            int dedicatedDictSearch = cdict->matchState.dedicatedDictSearch;

            return ((dedicatedDictSearch != 0 || ((pledgedSrcSize <= cutoff || pledgedSrcSize == (unchecked(0UL - 1)) || @params->attachDictPref == ZSTD_dictAttachPref_e.ZSTD_dictForceAttach) && @params->attachDictPref != ZSTD_dictAttachPref_e.ZSTD_dictForceCopy && @params->forceWindow == 0)) ? 1 : 0);
        }

        private static nuint ZSTD_resetCCtx_byAttachingCDict(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s @params, ulong pledgedSrcSize, ZSTD_buffered_policy_e zbuff)
        {

            {
                ZSTD_compressionParameters adjusted_cdict_cParams = cdict->matchState.cParams;
                uint windowLog = @params.cParams.windowLog;

                assert(windowLog != 0);
                if (cdict->matchState.dedicatedDictSearch != 0)
                {
                    ZSTD_dedicatedDictSearch_revertCParams(&adjusted_cdict_cParams);
                }

                @params.cParams = ZSTD_adjustCParams_internal(adjusted_cdict_cParams, pledgedSrcSize, cdict->dictContentSize, ZSTD_cParamMode_e.ZSTD_cpm_attachDict);
                @params.cParams.windowLog = windowLog;
                @params.useRowMatchFinder = cdict->useRowMatchFinder;

                {
                    nuint err_code = (ZSTD_resetCCtx_internal(cctx, &@params, pledgedSrcSize, 0, ZSTD_compResetPolicy_e.ZSTDcrp_makeClean, zbuff));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(cctx->appliedParams.cParams.strategy == adjusted_cdict_cParams.strategy);
            }


            {
                uint cdictEnd = (uint)(cdict->matchState.window.nextSrc - cdict->matchState.window.@base);
                uint cdictLen = cdictEnd - cdict->matchState.window.dictLimit;

                if (cdictLen == 0)
                {
        ;
                }
                else
                {
                    cctx->blockState.matchState.dictMatchState = &cdict->matchState;
                    if (cctx->blockState.matchState.window.dictLimit < cdictEnd)
                    {
                        cctx->blockState.matchState.window.nextSrc = cctx->blockState.matchState.window.@base + cdictEnd;
                        ZSTD_window_clear(&cctx->blockState.matchState.window);
                    }

                    cctx->blockState.matchState.loadedDictEnd = cctx->blockState.matchState.window.dictLimit;
                }
            }

            cctx->dictID = cdict->dictID;
            cctx->dictContentSize = cdict->dictContentSize;
            memcpy((void*)(cctx->blockState.prevCBlock), (void*)(&cdict->cBlockState), ((nuint)(sizeof(ZSTD_compressedBlockState_t))));
            return 0;
        }

        private static nuint ZSTD_resetCCtx_byCopyingCDict(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s @params, ulong pledgedSrcSize, ZSTD_buffered_policy_e zbuff)
        {
            ZSTD_compressionParameters* cdict_cParams = &cdict->matchState.cParams;

            assert(cdict->matchState.dedicatedDictSearch == 0);

            {
                uint windowLog = @params.cParams.windowLog;

                assert(windowLog != 0);
                @params.cParams = *cdict_cParams;
                @params.cParams.windowLog = windowLog;
                @params.useRowMatchFinder = cdict->useRowMatchFinder;

                {
                    nuint err_code = (ZSTD_resetCCtx_internal(cctx, &@params, pledgedSrcSize, 0, ZSTD_compResetPolicy_e.ZSTDcrp_leaveDirty, zbuff));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(cctx->appliedParams.cParams.strategy == cdict_cParams->strategy);
                assert(cctx->appliedParams.cParams.hashLog == cdict_cParams->hashLog);
                assert(cctx->appliedParams.cParams.chainLog == cdict_cParams->chainLog);
            }

            ZSTD_cwksp_mark_tables_dirty(&cctx->workspace);
            assert(@params.useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);

            {
                nuint chainSize = (ZSTD_allocateChainTable(cdict_cParams->strategy, cdict->useRowMatchFinder, 0)) != 0 ? ((nuint)(1) << (int)cdict_cParams->chainLog) : 0;
                nuint hSize = (nuint)(1) << (int)cdict_cParams->hashLog;

                memcpy((void*)(cctx->blockState.matchState.hashTable), (void*)(cdict->matchState.hashTable), (hSize * (nuint)(sizeof(uint))));
                if ((ZSTD_allocateChainTable(cctx->appliedParams.cParams.strategy, cctx->appliedParams.useRowMatchFinder, 0)) != 0)
                {
                    memcpy((void*)(cctx->blockState.matchState.chainTable), (void*)(cdict->matchState.chainTable), (chainSize * (nuint)(sizeof(uint))));
                }

                if ((ZSTD_rowMatchFinderUsed(cdict_cParams->strategy, cdict->useRowMatchFinder)) != 0)
                {
                    nuint tagTableSize = hSize * (nuint)(2);

                    memcpy((void*)(cctx->blockState.matchState.tagTable), (void*)(cdict->matchState.tagTable), (tagTableSize));
                }
            }


            {
                int h3log = (int)cctx->blockState.matchState.hashLog3;
                nuint h3Size = h3log != 0 ? ((nuint)(1) << h3log) : 0;

                assert(cdict->matchState.hashLog3 == 0);
                memset((void*)(cctx->blockState.matchState.hashTable3), (0), (h3Size * (nuint)(sizeof(uint))));
            }

            ZSTD_cwksp_mark_tables_clean(&cctx->workspace);

            {
                ZSTD_matchState_t* srcMatchState = &cdict->matchState;
                ZSTD_matchState_t* dstMatchState = &cctx->blockState.matchState;

                dstMatchState->window = srcMatchState->window;
                dstMatchState->nextToUpdate = srcMatchState->nextToUpdate;
                dstMatchState->loadedDictEnd = srcMatchState->loadedDictEnd;
            }

            cctx->dictID = cdict->dictID;
            cctx->dictContentSize = cdict->dictContentSize;
            memcpy((void*)(cctx->blockState.prevCBlock), (void*)(&cdict->cBlockState), ((nuint)(sizeof(ZSTD_compressedBlockState_t))));
            return 0;
        }

        /* We have a choice between copying the dictionary context into the working
         * context, or referencing the dictionary context from the working context
         * in-place. We decide here which strategy to use. */
        private static nuint ZSTD_resetCCtx_usingCDict(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize, ZSTD_buffered_policy_e zbuff)
        {
            if ((ZSTD_shouldAttachDict(cdict, @params, pledgedSrcSize)) != 0)
            {
                return ZSTD_resetCCtx_byAttachingCDict(cctx, cdict, *@params, pledgedSrcSize, zbuff);
            }
            else
            {
                return ZSTD_resetCCtx_byCopyingCDict(cctx, cdict, *@params, pledgedSrcSize, zbuff);
            }
        }

        /*! ZSTD_copyCCtx_internal() :
         *  Duplicate an existing context `srcCCtx` into another one `dstCCtx`.
         *  Only works during stage ZSTDcs_init (i.e. after creation, but before first call to ZSTD_compressContinue()).
         *  The "context", in this case, refers to the hash and chain tables,
         *  entropy tables, and dictionary references.
         * `windowLog` value is enforced if != 0, otherwise value is copied from srcCCtx.
         * @return : 0, or an error code */
        private static nuint ZSTD_copyCCtx_internal(ZSTD_CCtx_s* dstCCtx, ZSTD_CCtx_s* srcCCtx, ZSTD_frameParameters fParams, ulong pledgedSrcSize, ZSTD_buffered_policy_e zbuff)
        {
            if (srcCCtx->stage != ZSTD_compressionStage_e.ZSTDcs_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            memcpy((void*)(&dstCCtx->customMem), (void*)(&srcCCtx->customMem), ((nuint)(sizeof(ZSTD_customMem))));

            {
                ZSTD_CCtx_params_s @params = dstCCtx->requestedParams;

                @params.cParams = srcCCtx->appliedParams.cParams;
                assert(srcCCtx->appliedParams.useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
                @params.useRowMatchFinder = srcCCtx->appliedParams.useRowMatchFinder;
                @params.fParams = fParams;
                ZSTD_resetCCtx_internal(dstCCtx, &@params, pledgedSrcSize, 0, ZSTD_compResetPolicy_e.ZSTDcrp_leaveDirty, zbuff);
                assert(dstCCtx->appliedParams.cParams.windowLog == srcCCtx->appliedParams.cParams.windowLog);
                assert(dstCCtx->appliedParams.cParams.strategy == srcCCtx->appliedParams.cParams.strategy);
                assert(dstCCtx->appliedParams.cParams.hashLog == srcCCtx->appliedParams.cParams.hashLog);
                assert(dstCCtx->appliedParams.cParams.chainLog == srcCCtx->appliedParams.cParams.chainLog);
                assert(dstCCtx->blockState.matchState.hashLog3 == srcCCtx->blockState.matchState.hashLog3);
            }

            ZSTD_cwksp_mark_tables_dirty(&dstCCtx->workspace);

            {
                nuint chainSize = (ZSTD_allocateChainTable(srcCCtx->appliedParams.cParams.strategy, srcCCtx->appliedParams.useRowMatchFinder, 0)) != 0 ? ((nuint)(1) << (int)srcCCtx->appliedParams.cParams.chainLog) : 0;
                nuint hSize = (nuint)(1) << (int)srcCCtx->appliedParams.cParams.hashLog;
                int h3log = (int)srcCCtx->blockState.matchState.hashLog3;
                nuint h3Size = h3log != 0 ? ((nuint)(1) << h3log) : 0;

                memcpy((void*)(dstCCtx->blockState.matchState.hashTable), (void*)(srcCCtx->blockState.matchState.hashTable), (hSize * (nuint)(sizeof(uint))));
                memcpy((void*)(dstCCtx->blockState.matchState.chainTable), (void*)(srcCCtx->blockState.matchState.chainTable), (chainSize * (nuint)(sizeof(uint))));
                memcpy((void*)(dstCCtx->blockState.matchState.hashTable3), (void*)(srcCCtx->blockState.matchState.hashTable3), (h3Size * (nuint)(sizeof(uint))));
            }

            ZSTD_cwksp_mark_tables_clean(&dstCCtx->workspace);

            {
                ZSTD_matchState_t* srcMatchState = &srcCCtx->blockState.matchState;
                ZSTD_matchState_t* dstMatchState = &dstCCtx->blockState.matchState;

                dstMatchState->window = srcMatchState->window;
                dstMatchState->nextToUpdate = srcMatchState->nextToUpdate;
                dstMatchState->loadedDictEnd = srcMatchState->loadedDictEnd;
            }

            dstCCtx->dictID = srcCCtx->dictID;
            dstCCtx->dictContentSize = srcCCtx->dictContentSize;
            memcpy((void*)(dstCCtx->blockState.prevCBlock), (void*)(srcCCtx->blockState.prevCBlock), ((nuint)(sizeof(ZSTD_compressedBlockState_t))));
            return 0;
        }

        /*! ZSTD_copyCCtx() :
         *  Duplicate an existing context `srcCCtx` into another one `dstCCtx`.
         *  Only works during stage ZSTDcs_init (i.e. after creation, but before first call to ZSTD_compressContinue()).
         *  pledgedSrcSize==0 means "unknown".
        *   @return : 0, or an error code */
        public static nuint ZSTD_copyCCtx(ZSTD_CCtx_s* dstCCtx, ZSTD_CCtx_s* srcCCtx, ulong pledgedSrcSize)
        {
            ZSTD_frameParameters fParams = new ZSTD_frameParameters
            {
                contentSizeFlag = 1,
                checksumFlag = 0,
                noDictIDFlag = 0,
            };
            ZSTD_buffered_policy_e zbuff = srcCCtx->bufferedPolicy;

            if (pledgedSrcSize == 0)
            {
                pledgedSrcSize = (unchecked(0UL - 1));
            }

            fParams.contentSizeFlag = ((pledgedSrcSize != (unchecked(0UL - 1))) ? 1 : 0);
            return ZSTD_copyCCtx_internal(dstCCtx, srcCCtx, fParams, pledgedSrcSize, zbuff);
        }

        /*! ZSTD_reduceTable() :
         *  reduce table indexes by `reducerValue`, or squash to zero.
         *  PreserveMark preserves "unsorted mark" for btlazy2 strategy.
         *  It must be set to a clear 0/1 value, to remove branch during inlining.
         *  Presume table size is a multiple of ZSTD_ROWSIZE
         *  to help auto-vectorization */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_reduceTable_internal(uint* table, uint size, uint reducerValue, int preserveMark)
        {
            int nbRows = (int)(size) / 16;
            int cellNb = 0;
            int rowNb;

            assert((size & (uint)((16 - 1))) == 0);
            assert(size < (1U << 31));
            for (rowNb = 0; rowNb < nbRows; rowNb++)
            {
                int column;

                for (column = 0; column < 16; column++)
                {
                    if (preserveMark != 0)
                    {
                        uint adder = (table[cellNb] == 1) ? reducerValue : 0;

                        table[cellNb] += adder;
                    }

                    if (table[cellNb] < reducerValue)
                    {
                        table[cellNb] = 0;
                    }
                    else
                    {
                        table[cellNb] -= reducerValue;
                    }

                    cellNb++;
                }
            }
        }

        private static void ZSTD_reduceTable(uint* table, uint size, uint reducerValue)
        {
            ZSTD_reduceTable_internal(table, size, reducerValue, 0);
        }

        private static void ZSTD_reduceTable_btlazy2(uint* table, uint size, uint reducerValue)
        {
            ZSTD_reduceTable_internal(table, size, reducerValue, 1);
        }

        /*! ZSTD_reduceIndex() :
        *   rescale all indexes to avoid future overflow (indexes are U32) */
        private static void ZSTD_reduceIndex(ZSTD_matchState_t* ms, ZSTD_CCtx_params_s* @params, uint reducerValue)
        {

            {
                uint hSize = (uint)(1) << (int)@params->cParams.hashLog;

                ZSTD_reduceTable(ms->hashTable, hSize, reducerValue);
            }

            if ((ZSTD_allocateChainTable(@params->cParams.strategy, @params->useRowMatchFinder, (uint)(ms->dedicatedDictSearch))) != 0)
            {
                uint chainSize = (uint)(1) << (int)@params->cParams.chainLog;

                if (@params->cParams.strategy == ZSTD_strategy.ZSTD_btlazy2)
                {
                    ZSTD_reduceTable_btlazy2(ms->chainTable, chainSize, reducerValue);
                }
                else
                {
                    ZSTD_reduceTable(ms->chainTable, chainSize, reducerValue);
                }
            }

            if (ms->hashLog3 != 0)
            {
                uint h3Size = (uint)(1) << (int)ms->hashLog3;

                ZSTD_reduceTable(ms->hashTable3, h3Size, reducerValue);
            }
        }

        /* See doc/zstd_compression_format.md for detailed format description */
        public static void ZSTD_seqToCodes(seqStore_t* seqStorePtr)
        {
            seqDef_s* sequences = seqStorePtr->sequencesStart;
            byte* llCodeTable = seqStorePtr->llCode;
            byte* ofCodeTable = seqStorePtr->ofCode;
            byte* mlCodeTable = seqStorePtr->mlCode;
            uint nbSeq = (uint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
            uint u;

            assert(nbSeq <= seqStorePtr->maxNbSeq);
            for (u = 0; u < nbSeq; u++)
            {
                uint llv = sequences[u].litLength;
                uint mlv = sequences[u].matchLength;

                llCodeTable[u] = (byte)(ZSTD_LLcode(llv));
                ofCodeTable[u] = (byte)(ZSTD_highbit32(sequences[u].offset));
                mlCodeTable[u] = (byte)(ZSTD_MLcode(mlv));
            }

            if (seqStorePtr->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_literalLength)
            {
                llCodeTable[seqStorePtr->longLengthPos] = 35;
            }

            if (seqStorePtr->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_matchLength)
            {
                mlCodeTable[seqStorePtr->longLengthPos] = 52;
            }
        }

        /* ZSTD_useTargetCBlockSize():
         * Returns if target compressed block size param is being used.
         * If used, compression will do best effort to make a compressed block size to be around targetCBlockSize.
         * Returns 1 if true, 0 otherwise. */
        private static int ZSTD_useTargetCBlockSize(ZSTD_CCtx_params_s* cctxParams)
        {
            return ((cctxParams->targetCBlockSize != 0) ? 1 : 0);
        }

        /* ZSTD_blockSplitterEnabled():
         * Returns if block splitting param is being used
         * If used, compression will do best effort to split a block in order to improve compression ratio.
         * Returns 1 if true, 0 otherwise. */
        private static int ZSTD_blockSplitterEnabled(ZSTD_CCtx_params_s* cctxParams)
        {
            return ((cctxParams->splitBlocks != 0) ? 1 : 0);
        }

        /* ZSTD_buildSequencesStatistics():
         * Returns a ZSTD_symbolEncodingTypeStats_t, or a zstd error code in the `size` field.
         * Modifies `nextEntropy` to have the appropriate values as a side effect.
         * nbSeq must be greater than 0.
         *
         * entropyWkspSize must be of size at least ENTROPY_WORKSPACE_SIZE - (MaxSeq + 1)*sizeof(U32)
         */
        private static ZSTD_symbolEncodingTypeStats_t ZSTD_buildSequencesStatistics(seqStore_t* seqStorePtr, nuint nbSeq, ZSTD_fseCTables_t* prevEntropy, ZSTD_fseCTables_t* nextEntropy, byte* dst, byte* dstEnd, ZSTD_strategy strategy, uint* countWorkspace, void* entropyWorkspace, nuint entropyWkspSize)
        {
            byte* ostart = dst;
            byte* oend = dstEnd;
            byte* op = ostart;
            uint* CTable_LitLength = (uint*)nextEntropy->litlengthCTable;
            uint* CTable_OffsetBits = (uint*)nextEntropy->offcodeCTable;
            uint* CTable_MatchLength = (uint*)nextEntropy->matchlengthCTable;
            byte* ofCodeTable = seqStorePtr->ofCode;
            byte* llCodeTable = seqStorePtr->llCode;
            byte* mlCodeTable = seqStorePtr->mlCode;
            ZSTD_symbolEncodingTypeStats_t stats;
            var _ = &stats;

            stats.lastCountSize = 0;
            ZSTD_seqToCodes(seqStorePtr);
            assert(op <= oend);
            assert(nbSeq != 0);

            {
                uint max = 35;
                nuint mostFrequent = HIST_countFast_wksp(countWorkspace, &max, (void*)llCodeTable, nbSeq, entropyWorkspace, entropyWkspSize);

                nextEntropy->litlength_repeatMode = prevEntropy->litlength_repeatMode;
                stats.LLtype = (uint)(ZSTD_selectEncodingType(&nextEntropy->litlength_repeatMode, countWorkspace, max, mostFrequent, nbSeq, 9, (uint*)prevEntropy->litlengthCTable, (short*)LL_defaultNorm, LL_defaultNormLog, ZSTD_defaultPolicy_e.ZSTD_defaultAllowed, strategy));
                assert(symbolEncodingType_e.set_basic < symbolEncodingType_e.set_compressed && symbolEncodingType_e.set_rle < symbolEncodingType_e.set_compressed);
                assert(!(stats.LLtype < (uint)symbolEncodingType_e.set_compressed && nextEntropy->litlength_repeatMode != FSE_repeat.FSE_repeat_none));

                {
                    nuint countSize = ZSTD_buildCTable((void*)op, (nuint)(oend - op), CTable_LitLength, 9, (symbolEncodingType_e)(stats.LLtype), countWorkspace, max, llCodeTable, nbSeq, (short*)LL_defaultNorm, LL_defaultNormLog, 35, (uint*)prevEntropy->litlengthCTable, (nuint)(1316), entropyWorkspace, entropyWkspSize);

                    if ((ERR_isError(countSize)) != 0)
                    {
                        stats.size = countSize;
                        return stats;
                    }

                    if (stats.LLtype == (uint)symbolEncodingType_e.set_compressed)
                    {
                        stats.lastCountSize = countSize;
                    }

                    op += countSize;
                    assert(op <= oend);
                }
            }


            {
                uint max = 31;
                nuint mostFrequent = HIST_countFast_wksp(countWorkspace, &max, (void*)ofCodeTable, nbSeq, entropyWorkspace, entropyWkspSize);
                ZSTD_defaultPolicy_e defaultPolicy = (max <= 28) ? ZSTD_defaultPolicy_e.ZSTD_defaultAllowed : ZSTD_defaultPolicy_e.ZSTD_defaultDisallowed;

                nextEntropy->offcode_repeatMode = prevEntropy->offcode_repeatMode;
                stats.Offtype = (uint)(ZSTD_selectEncodingType(&nextEntropy->offcode_repeatMode, countWorkspace, max, mostFrequent, nbSeq, 8, (uint*)prevEntropy->offcodeCTable, (short*)OF_defaultNorm, OF_defaultNormLog, defaultPolicy, strategy));
                assert(!(stats.Offtype < (uint)symbolEncodingType_e.set_compressed && nextEntropy->offcode_repeatMode != FSE_repeat.FSE_repeat_none));

                {
                    nuint countSize = ZSTD_buildCTable((void*)op, (nuint)(oend - op), CTable_OffsetBits, 8, (symbolEncodingType_e)(stats.Offtype), countWorkspace, max, ofCodeTable, nbSeq, (short*)OF_defaultNorm, OF_defaultNormLog, 28, (uint*)prevEntropy->offcodeCTable, (nuint)(772), entropyWorkspace, entropyWkspSize);

                    if ((ERR_isError(countSize)) != 0)
                    {
                        stats.size = countSize;
                        return stats;
                    }

                    if (stats.Offtype == (uint)symbolEncodingType_e.set_compressed)
                    {
                        stats.lastCountSize = countSize;
                    }

                    op += countSize;
                    assert(op <= oend);
                }
            }


            {
                uint max = 52;
                nuint mostFrequent = HIST_countFast_wksp(countWorkspace, &max, (void*)mlCodeTable, nbSeq, entropyWorkspace, entropyWkspSize);

                nextEntropy->matchlength_repeatMode = prevEntropy->matchlength_repeatMode;
                stats.MLtype = (uint)(ZSTD_selectEncodingType(&nextEntropy->matchlength_repeatMode, countWorkspace, max, mostFrequent, nbSeq, 9, (uint*)prevEntropy->matchlengthCTable, (short*)ML_defaultNorm, ML_defaultNormLog, ZSTD_defaultPolicy_e.ZSTD_defaultAllowed, strategy));
                assert(!(stats.MLtype < (uint)symbolEncodingType_e.set_compressed && nextEntropy->matchlength_repeatMode != FSE_repeat.FSE_repeat_none));

                {
                    nuint countSize = ZSTD_buildCTable((void*)op, (nuint)(oend - op), CTable_MatchLength, 9, (symbolEncodingType_e)(stats.MLtype), countWorkspace, max, mlCodeTable, nbSeq, (short*)ML_defaultNorm, ML_defaultNormLog, 52, (uint*)prevEntropy->matchlengthCTable, (nuint)(1452), entropyWorkspace, entropyWkspSize);

                    if ((ERR_isError(countSize)) != 0)
                    {
                        stats.size = countSize;
                        return stats;
                    }

                    if (stats.MLtype == (uint)symbolEncodingType_e.set_compressed)
                    {
                        stats.lastCountSize = countSize;
                    }

                    op += countSize;
                    assert(op <= oend);
                }
            }

            stats.size = (nuint)(op - ostart);
            return stats;
        }

        /* ZSTD_entropyCompressSeqStore_internal():
         * compresses both literals and sequences
         * Returns compressed size of block, or a zstd error.
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_entropyCompressSeqStore_internal(seqStore_t* seqStorePtr, ZSTD_entropyCTables_t* prevEntropy, ZSTD_entropyCTables_t* nextEntropy, ZSTD_CCtx_params_s* cctxParams, void* dst, nuint dstCapacity, void* entropyWorkspace, nuint entropyWkspSize, int bmi2)
        {
            int longOffsets = ((cctxParams->cParams.windowLog > ((uint)(MEM_32bits ? 25 : 57))) ? 1 : 0);
            ZSTD_strategy strategy = cctxParams->cParams.strategy;
            uint* count = (uint*)(entropyWorkspace);
            uint* CTable_LitLength = (uint*)nextEntropy->fse.litlengthCTable;
            uint* CTable_OffsetBits = (uint*)nextEntropy->fse.offcodeCTable;
            uint* CTable_MatchLength = (uint*)nextEntropy->fse.matchlengthCTable;
            seqDef_s* sequences = seqStorePtr->sequencesStart;
            nuint nbSeq = (nuint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
            byte* ofCodeTable = seqStorePtr->ofCode;
            byte* llCodeTable = seqStorePtr->llCode;
            byte* mlCodeTable = seqStorePtr->mlCode;
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstCapacity;
            byte* op = ostart;
            nuint lastCountSize;

            entropyWorkspace = count + (((35) > (52) ? (35) : (52)) + 1);
            entropyWkspSize -= (uint)((((35) > (52) ? (35) : (52)) + 1)) * (nuint)(sizeof(uint));
            assert(entropyWkspSize >= (uint)(((6 << 10) + 256)));

            {
                byte* literals = seqStorePtr->litStart;
                nuint litSize = (nuint)(seqStorePtr->lit - literals);
                nuint cSize = ZSTD_compressLiterals(&prevEntropy->huf, &nextEntropy->huf, cctxParams->cParams.strategy, ZSTD_disableLiteralsCompression(cctxParams), (void*)op, dstCapacity, (void*)literals, litSize, entropyWorkspace, entropyWkspSize, bmi2);


                {
                    nuint err_code = (cSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(cSize <= dstCapacity);
                op += cSize;
            }

            if ((oend - op) < 3 + 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (nbSeq < 128)
            {
                *op++ = (byte)(nbSeq);
            }
            else if (nbSeq < 0x7F00)
            {
                op[0] = (byte)((nbSeq >> 8) + 0x80);
                op[1] = (byte)(nbSeq);
                op += 2;
            }
            else
            {
                op[0] = 0xFF;
                MEM_writeLE16((void*)(op + 1), (ushort)(nbSeq - 0x7F00));
                op += 3;
            }

            assert(op <= oend);
            if (nbSeq == 0)
            {
                memcpy((void*)(&nextEntropy->fse), (void*)(&prevEntropy->fse), ((nuint)(sizeof(ZSTD_fseCTables_t))));
                return (nuint)(op - ostart);
            }


            {
                ZSTD_symbolEncodingTypeStats_t stats;
                byte* seqHead = op++;

                stats = ZSTD_buildSequencesStatistics(seqStorePtr, nbSeq, &prevEntropy->fse, &nextEntropy->fse, op, oend, strategy, count, entropyWorkspace, entropyWkspSize);

                {
                    nuint err_code = (stats.size);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                *seqHead = (byte)((stats.LLtype << 6) + (stats.Offtype << 4) + (stats.MLtype << 2));
                lastCountSize = stats.lastCountSize;
                op += stats.size;
            }


            {
                nuint bitstreamSize = ZSTD_encodeSequences((void*)op, (nuint)(oend - op), CTable_MatchLength, mlCodeTable, CTable_OffsetBits, ofCodeTable, CTable_LitLength, llCodeTable, sequences, nbSeq, longOffsets, bmi2);


                {
                    nuint err_code = (bitstreamSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                op += bitstreamSize;
                assert(op <= oend);
                if (lastCountSize != 0 && (lastCountSize + bitstreamSize) < 4)
                {
                    assert(lastCountSize + bitstreamSize == 3);
                    return 0;
                }
            }

            return (nuint)(op - ostart);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_entropyCompressSeqStore(seqStore_t* seqStorePtr, ZSTD_entropyCTables_t* prevEntropy, ZSTD_entropyCTables_t* nextEntropy, ZSTD_CCtx_params_s* cctxParams, void* dst, nuint dstCapacity, nuint srcSize, void* entropyWorkspace, nuint entropyWkspSize, int bmi2)
        {
            nuint cSize = ZSTD_entropyCompressSeqStore_internal(seqStorePtr, prevEntropy, nextEntropy, cctxParams, dst, dstCapacity, entropyWorkspace, entropyWkspSize, bmi2);

            if (cSize == 0)
            {
                return 0;
            }

            if (((cSize == (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)))) && (srcSize <= dstCapacity)))
            {
                return 0;
            }


            {
                nuint err_code = (cSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint maxCSize = srcSize - ZSTD_minGain(srcSize, cctxParams->cParams.strategy);

                if (cSize >= maxCSize)
                {
                    return 0;
                }
            }

            return cSize;
        }

        /* ZSTD_selectBlockCompressor() :
         * Not static, but internal use only (used by long distance matcher)
         * assumption : strat is a valid strategy */
        public static ZSTD_blockCompressor? ZSTD_selectBlockCompressor(ZSTD_strategy strat, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, ZSTD_dictMode_e dictMode)
        {

            ZSTD_blockCompressor? selectedCompressor;

            assert((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, (int)strat)) != 0);
            if ((ZSTD_rowMatchFinderUsed(strat, useRowMatchFinder)) != 0)
            {


                assert(useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
                selectedCompressor = rowBasedBlockCompressors[(int)(dictMode)][(int)(strat) - (int)(ZSTD_strategy.ZSTD_greedy)];
            }
            else
            {
                selectedCompressor = blockCompressor[(int)(dictMode)][(int)(strat)];
            }

            assert(selectedCompressor != null);
            return selectedCompressor;
        }

        private static void ZSTD_storeLastLiterals(seqStore_t* seqStorePtr, byte* anchor, nuint lastLLSize)
        {
            memcpy((void*)(seqStorePtr->lit), (void*)(anchor), (lastLLSize));
            seqStorePtr->lit += lastLLSize;
        }

        public static void ZSTD_resetSeqStore(seqStore_t* ssPtr)
        {
            ssPtr->lit = ssPtr->litStart;
            ssPtr->sequences = ssPtr->sequencesStart;
            ssPtr->longLengthType = ZSTD_longLengthType_e.ZSTD_llt_none;
        }

        private static nuint ZSTD_buildSeqStore(ZSTD_CCtx_s* zc, void* src, nuint srcSize)
        {
            ZSTD_matchState_t* ms = &zc->blockState.matchState;

            assert(srcSize <= (uint)((1 << 17)));
            ZSTD_assertEqualCParams(zc->appliedParams.cParams, ms->cParams);
            if (srcSize < (uint)((1 + 1 + 1)) + ZSTD_blockHeaderSize + 1)
            {
                if (zc->appliedParams.cParams.strategy >= ZSTD_strategy.ZSTD_btopt)
                {
                    ZSTD_ldm_skipRawSeqStoreBytes(&zc->externSeqStore, srcSize);
                }
                else
                {
                    ZSTD_ldm_skipSequences(&zc->externSeqStore, srcSize, zc->appliedParams.cParams.minMatch);
                }

                return (nuint)ZSTD_buildSeqStore_e.ZSTDbss_noCompress;
            }

            ZSTD_resetSeqStore(&(zc->seqStore));
            ms->opt.symbolCosts = &zc->blockState.prevCBlock->entropy;
            ms->opt.literalCompressionMode = zc->appliedParams.literalCompressionMode;
            assert(ms->dictMatchState == null || ms->loadedDictEnd == ms->window.dictLimit);

            {
                byte* @base = ms->window.@base;
                byte* istart = (byte*)(src);
                uint curr = (uint)(istart - @base);

                if ((nuint)(sizeof(nint)) == 8)
                {
                    assert(istart - @base < unchecked((nint)(unchecked((uint)(-1)))));
                }

                if (curr > ms->nextToUpdate + 384)
                {
                    ms->nextToUpdate = curr - (uint)((192) < ((uint)(curr - ms->nextToUpdate - 384)) ? (192) : ((uint)(curr - ms->nextToUpdate - 384)));
                }
            }


            {
                ZSTD_dictMode_e dictMode = ZSTD_matchState_dictMode(ms);
                nuint lastLLSize;


                {
                    int i;

                    for (i = 0; i < 3; ++i)
                    {
                        zc->blockState.nextCBlock->rep[i] = zc->blockState.prevCBlock->rep[i];
                    }
                }

                if (zc->externSeqStore.pos < zc->externSeqStore.size)
                {
                    assert(zc->appliedParams.ldmParams.enableLdm == 0);
                    lastLLSize = ZSTD_ldm_blockCompress(&zc->externSeqStore, ms, &zc->seqStore, zc->blockState.nextCBlock->rep, zc->appliedParams.useRowMatchFinder, src, srcSize);
                    assert(zc->externSeqStore.pos <= zc->externSeqStore.size);
                }
                else if (zc->appliedParams.ldmParams.enableLdm != 0)
                {
                    rawSeqStore_t ldmSeqStore = kNullRawSeqStore;

                    ldmSeqStore.seq = zc->ldmSequences;
                    ldmSeqStore.capacity = zc->maxNbLdmSequences;

                    {
                        nuint err_code = (ZSTD_ldm_generateSequences(&zc->ldmState, &ldmSeqStore, &zc->appliedParams.ldmParams, src, srcSize));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    lastLLSize = ZSTD_ldm_blockCompress(&ldmSeqStore, ms, &zc->seqStore, zc->blockState.nextCBlock->rep, zc->appliedParams.useRowMatchFinder, src, srcSize);
                    assert(ldmSeqStore.pos == ldmSeqStore.size);
                }
                else
                {
                    var blockCompressor = ZSTD_selectBlockCompressor(zc -> appliedParams.cParams.strategy, zc -> appliedParams.useRowMatchFinder, dictMode) ?? throw new InvalidOperationException();

                    ms->ldmSeqStore = null;
                    lastLLSize = blockCompressor(ms, &zc->seqStore, zc->blockState.nextCBlock->rep, src, srcSize);
                }


                {
                    byte* lastLiterals = (byte*)(src) + srcSize - lastLLSize;

                    ZSTD_storeLastLiterals(&zc->seqStore, lastLiterals, lastLLSize);
                }
            }

            return (nuint)ZSTD_buildSeqStore_e.ZSTDbss_compress;
        }

        private static void ZSTD_copyBlockSequences(ZSTD_CCtx_s* zc)
        {
            seqStore_t* seqStore = ZSTD_getSeqStore(zc);
            seqDef_s* seqStoreSeqs = seqStore->sequencesStart;
            nuint seqStoreSeqSize = (nuint)(seqStore->sequences - seqStoreSeqs);
            nuint seqStoreLiteralsSize = (nuint)(seqStore->lit - seqStore->litStart);
            nuint literalsRead = 0;
            nuint lastLLSize;
            ZSTD_Sequence* outSeqs = &zc->seqCollector.seqStart[zc->seqCollector.seqIndex];
            nuint i;
            repcodes_s updatedRepcodes;

            assert(zc->seqCollector.seqIndex + 1 < zc->seqCollector.maxSequences);
            assert(zc->seqCollector.maxSequences >= seqStoreSeqSize + 1);
            memcpy((void*)(updatedRepcodes.rep), (void*)(zc->blockState.prevCBlock->rep), ((nuint)(sizeof(repcodes_s))));
            for (i = 0; i < seqStoreSeqSize; ++i)
            {
                uint rawOffset = seqStoreSeqs[i].offset - 3;

                outSeqs[i].litLength = seqStoreSeqs[i].litLength;
                outSeqs[i].matchLength = (uint)(seqStoreSeqs[i].matchLength + 3);
                outSeqs[i].rep = 0;
                if (i == seqStore->longLengthPos)
                {
                    if (seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_literalLength)
                    {
                        outSeqs[i].litLength += 0x10000;
                    }
                    else if (seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_matchLength)
                    {
                        outSeqs[i].matchLength += 0x10000;
                    }
                }

                if (seqStoreSeqs[i].offset <= 3)
                {
                    outSeqs[i].rep = seqStoreSeqs[i].offset;
                    if (outSeqs[i].litLength != 0)
                    {
                        rawOffset = updatedRepcodes.rep[outSeqs[i].rep - 1];
                    }
                    else
                    {
                        if (outSeqs[i].rep == 3)
                        {
                            rawOffset = updatedRepcodes.rep[0] - 1;
                        }
                        else
                        {
                            rawOffset = updatedRepcodes.rep[outSeqs[i].rep];
                        }
                    }
                }

                outSeqs[i].offset = rawOffset;
                updatedRepcodes = ZSTD_updateRep(updatedRepcodes.rep, seqStoreSeqs[i].offset - 1, ((seqStoreSeqs[i].litLength == 0) ? 1U : 0U));
                literalsRead += outSeqs[i].litLength;
            }

            assert(seqStoreLiteralsSize >= literalsRead);
            lastLLSize = seqStoreLiteralsSize - literalsRead;
            outSeqs[i].litLength = (uint)(lastLLSize);
            outSeqs[i].matchLength = outSeqs[i].offset = outSeqs[i].rep = 0;
            seqStoreSeqSize++;
            zc->seqCollector.seqIndex += seqStoreSeqSize;
        }

        /*! ZSTD_generateSequences() :
         * Generate sequences using ZSTD_compress2, given a source buffer.
         *
         * Each block will end with a dummy sequence
         * with offset == 0, matchLength == 0, and litLength == length of last literals.
         * litLength may be == 0, and if so, then the sequence of (of: 0 ml: 0 ll: 0)
         * simply acts as a block delimiter.
         *
         * zc can be used to insert custom compression params.
         * This function invokes ZSTD_compress2
         *
         * The output of this function can be fed into ZSTD_compressSequences() with CCtx
         * setting of ZSTD_c_blockDelimiters as ZSTD_sf_explicitBlockDelimiters
         * @return : number of sequences generated
         */
        public static nuint ZSTD_generateSequences(ZSTD_CCtx_s* zc, ZSTD_Sequence* outSeqs, nuint outSeqsSize, void* src, nuint srcSize)
        {
            nuint dstCapacity = ZSTD_compressBound(srcSize);
            void* dst = ZSTD_customMalloc(dstCapacity, ZSTD_defaultCMem);
            SeqCollector seqCollector;

            if (dst == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }

            seqCollector.collectSequences = 1;
            seqCollector.seqStart = outSeqs;
            seqCollector.seqIndex = 0;
            seqCollector.maxSequences = outSeqsSize;
            zc->seqCollector = seqCollector;
            ZSTD_compress2(zc, dst, dstCapacity, src, srcSize);
            ZSTD_customFree(dst, ZSTD_defaultCMem);
            return zc->seqCollector.seqIndex;
        }

        /*! ZSTD_mergeBlockDelimiters() :
         * Given an array of ZSTD_Sequence, remove all sequences that represent block delimiters/last literals
         * by merging them into into the literals of the next sequence.
         *
         * As such, the final generated result has no explicit representation of block boundaries,
         * and the final last literals segment is not represented in the sequences.
         *
         * The output of this function can be fed into ZSTD_compressSequences() with CCtx
         * setting of ZSTD_c_blockDelimiters as ZSTD_sf_noBlockDelimiters
         * @return : number of sequences left after merging
         */
        public static nuint ZSTD_mergeBlockDelimiters(ZSTD_Sequence* sequences, nuint seqsSize)
        {
            nuint @in = 0;
            nuint @out = 0;

            for (; @in < seqsSize; ++@in)
            {
                if (sequences[@in].offset == 0 && sequences[@in].matchLength == 0)
                {
                    if (@in != seqsSize - 1)
                    {
                        sequences[@in + 1].litLength += sequences[@in].litLength;
                    }
                }
                else
                {
                    sequences[@out] = sequences[@in];
                    ++@out;
                }
            }

            return @out;
        }

        /* Unrolled loop to read four size_ts of input at a time. Returns 1 if is RLE, 0 if not. */
        private static int ZSTD_isRLE(byte* src, nuint length)
        {
            byte* ip = src;
            byte value = ip[0];
            nuint valueST = (nuint)((ulong)(value) * 0x0101010101010101UL);
            nuint unrollSize = (nuint)(sizeof(nuint)) * 4;
            nuint unrollMask = unrollSize - 1;
            nuint prefixLength = length & unrollMask;
            nuint i;
            nuint u;

            if (length == 1)
            {
                return 1;
            }

            if (prefixLength != 0 && ZSTD_count(ip + 1, ip, ip + prefixLength) != prefixLength - 1)
            {
                return 0;
            }

            for (i = prefixLength; i != length; i += unrollSize)
            {
                for (u = 0; u < unrollSize; u += (nuint)(sizeof(nuint)))
                {
                    if (MEM_readST((void*)(ip + i + u)) != valueST)
                    {
                        return 0;
                    }
                }
            }

            return 1;
        }

        /* Returns true if the given block may be RLE.
         * This is just a heuristic based on the compressibility.
         * It may return both false positives and false negatives.
         */
        private static int ZSTD_maybeRLE(seqStore_t* seqStore)
        {
            nuint nbSeqs = (nuint)(seqStore->sequences - seqStore->sequencesStart);
            nuint nbLits = (nuint)(seqStore->lit - seqStore->litStart);

            return ((nbSeqs < 4 && nbLits < 10) ? 1 : 0);
        }

        private static void ZSTD_blockState_confirmRepcodesAndEntropyTables(ZSTD_blockState_t* bs)
        {
            ZSTD_compressedBlockState_t* tmp = bs->prevCBlock;

            bs->prevCBlock = bs->nextCBlock;
            bs->nextCBlock = tmp;
        }

        /* Writes the block header */
        private static void writeBlockHeader(void* op, nuint cSize, nuint blockSize, uint lastBlock)
        {
            uint cBlockHeader = cSize == 1 ? lastBlock + (((uint)(blockType_e.bt_rle)) << 1) + (uint)(blockSize << 3) : lastBlock + (((uint)(blockType_e.bt_compressed)) << 1) + (uint)(cSize << 3);

            MEM_writeLE24(op, cBlockHeader);
        }

        /** ZSTD_buildBlockEntropyStats_literals() :
         *  Builds entropy for the literals.
         *  Stores literals block type (raw, rle, compressed, repeat) and
         *  huffman description table to hufMetadata.
         *  Requires ENTROPY_WORKSPACE_SIZE workspace
         *  @return : size of huffman description table or error code */
        private static nuint ZSTD_buildBlockEntropyStats_literals(void* src, nuint srcSize, ZSTD_hufCTables_t* prevHuf, ZSTD_hufCTables_t* nextHuf, ZSTD_hufCTablesMetadata_t* hufMetadata, int disableLiteralsCompression, void* workspace, nuint wkspSize)
        {
            byte* wkspStart = (byte*)(workspace);
            byte* wkspEnd = wkspStart + wkspSize;
            byte* countWkspStart = wkspStart;
            uint* countWksp = (uint*)(workspace);
            nuint countWkspSize = (uint)((255 + 1)) * (nuint)(4);
            byte* nodeWksp = countWkspStart + countWkspSize;
            nuint nodeWkspSize = (nuint)(wkspEnd - nodeWksp);
            uint maxSymbolValue = 255;
            uint huffLog = 11;
            HUF_repeat repeat = prevHuf->repeatMode;

            memcpy((void*)(nextHuf), (void*)(prevHuf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
            if (disableLiteralsCompression != 0)
            {
                hufMetadata->hType = symbolEncodingType_e.set_basic;
                return 0;
            }


            {
                nuint minLitSize = (nuint)((prevHuf->repeatMode == HUF_repeat.HUF_repeat_valid) ? 6 : 63);

                if (srcSize <= minLitSize)
                {
                    hufMetadata->hType = symbolEncodingType_e.set_basic;
                    return 0;
                }
            }


            {
                nuint largest = HIST_count_wksp(countWksp, &maxSymbolValue, (void*)(byte*)(src), srcSize, workspace, wkspSize);


                {
                    nuint err_code = (largest);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (largest == srcSize)
                {
                    hufMetadata->hType = symbolEncodingType_e.set_rle;
                    return 0;
                }

                if (largest <= (srcSize >> 7) + 4)
                {
                    hufMetadata->hType = symbolEncodingType_e.set_basic;
                    return 0;
                }
            }

            if (repeat == HUF_repeat.HUF_repeat_check && (HUF_validateCTable((HUF_CElt_s*)(prevHuf->CTable), countWksp, maxSymbolValue)) == 0)
            {
                repeat = HUF_repeat.HUF_repeat_none;
            }

            memset((void*)(nextHuf->CTable), (0), ((nuint)(sizeof(HUF_CElt_s) * 256)));
            huffLog = HUF_optimalTableLog(huffLog, srcSize, maxSymbolValue);

            {
                nuint maxBits = HUF_buildCTable_wksp((HUF_CElt_s*)(nextHuf->CTable), countWksp, maxSymbolValue, huffLog, (void*)nodeWksp, nodeWkspSize);


                {
                    nuint err_code = (maxBits);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                huffLog = (uint)(maxBits);

                {
                    nuint newCSize = HUF_estimateCompressedSize((HUF_CElt_s*)(nextHuf->CTable), countWksp, maxSymbolValue);
                    nuint hSize = HUF_writeCTable_wksp((void*)hufMetadata->hufDesBuffer, (nuint)(128), (HUF_CElt_s*)(nextHuf->CTable), maxSymbolValue, huffLog, (void*)nodeWksp, nodeWkspSize);

                    if (repeat != HUF_repeat.HUF_repeat_none)
                    {
                        nuint oldCSize = HUF_estimateCompressedSize((HUF_CElt_s*)(prevHuf->CTable), countWksp, maxSymbolValue);

                        if (oldCSize < srcSize && (oldCSize <= hSize + newCSize || hSize + 12 >= srcSize))
                        {
                            memcpy((void*)(nextHuf), (void*)(prevHuf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
                            hufMetadata->hType = symbolEncodingType_e.set_repeat;
                            return 0;
                        }
                    }

                    if (newCSize + hSize >= srcSize)
                    {
                        memcpy((void*)(nextHuf), (void*)(prevHuf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
                        hufMetadata->hType = symbolEncodingType_e.set_basic;
                        return 0;
                    }

                    hufMetadata->hType = symbolEncodingType_e.set_compressed;
                    nextHuf->repeatMode = HUF_repeat.HUF_repeat_check;
                    return hSize;
                }
            }
        }

        /* ZSTD_buildDummySequencesStatistics():
         * Returns a ZSTD_symbolEncodingTypeStats_t with all encoding types as set_basic,
         * and updates nextEntropy to the appropriate repeatMode.
         */
        private static ZSTD_symbolEncodingTypeStats_t ZSTD_buildDummySequencesStatistics(ZSTD_fseCTables_t* nextEntropy)
        {
            ZSTD_symbolEncodingTypeStats_t stats = new ZSTD_symbolEncodingTypeStats_t
            {
                LLtype = (uint)symbolEncodingType_e.set_basic,
                Offtype = (uint)symbolEncodingType_e.set_basic,
                MLtype = (uint)symbolEncodingType_e.set_basic,
                size = 0,
                lastCountSize = 0,
            };

            nextEntropy->litlength_repeatMode = FSE_repeat.FSE_repeat_none;
            nextEntropy->offcode_repeatMode = FSE_repeat.FSE_repeat_none;
            nextEntropy->matchlength_repeatMode = FSE_repeat.FSE_repeat_none;
            return stats;
        }

        /** ZSTD_buildBlockEntropyStats_sequences() :
         *  Builds entropy for the sequences.
         *  Stores symbol compression modes and fse table to fseMetadata.
         *  Requires ENTROPY_WORKSPACE_SIZE wksp.
         *  @return : size of fse tables or error code */
        private static nuint ZSTD_buildBlockEntropyStats_sequences(seqStore_t* seqStorePtr, ZSTD_fseCTables_t* prevEntropy, ZSTD_fseCTables_t* nextEntropy, ZSTD_CCtx_params_s* cctxParams, ZSTD_fseCTablesMetadata_t* fseMetadata, void* workspace, nuint wkspSize)
        {
            ZSTD_strategy strategy = cctxParams->cParams.strategy;
            nuint nbSeq = (nuint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
            byte* ostart = (byte*)fseMetadata->fseTablesBuffer;
            byte* oend = ostart + (nuint)(133);
            byte* op = ostart;
            uint* countWorkspace = (uint*)(workspace);
            uint* entropyWorkspace = countWorkspace + (((35) > (52) ? (35) : (52)) + 1);
            nuint entropyWorkspaceSize = wkspSize - (uint)((((35) > (52) ? (35) : (52)) + 1)) * (nuint)(4);
            ZSTD_symbolEncodingTypeStats_t stats;

            stats = nbSeq != 0 ? ZSTD_buildSequencesStatistics(seqStorePtr, nbSeq, prevEntropy, nextEntropy, op, oend, strategy, countWorkspace, (void*)entropyWorkspace, entropyWorkspaceSize) : ZSTD_buildDummySequencesStatistics(nextEntropy);

            {
                nuint err_code = (stats.size);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            fseMetadata->llType = (symbolEncodingType_e)(stats.LLtype);
            fseMetadata->ofType = (symbolEncodingType_e)(stats.Offtype);
            fseMetadata->mlType = (symbolEncodingType_e)(stats.MLtype);
            fseMetadata->lastCountSize = stats.lastCountSize;
            return stats.size;
        }

        /** ZSTD_buildBlockEntropyStats() :
         *  Builds entropy for the block.
         *  Requires workspace size ENTROPY_WORKSPACE_SIZE
         *
         *  @return : 0 on success or error code
         */
        public static nuint ZSTD_buildBlockEntropyStats(seqStore_t* seqStorePtr, ZSTD_entropyCTables_t* prevEntropy, ZSTD_entropyCTables_t* nextEntropy, ZSTD_CCtx_params_s* cctxParams, ZSTD_entropyCTablesMetadata_t* entropyMetadata, void* workspace, nuint wkspSize)
        {
            nuint litSize = (nuint)(seqStorePtr->lit - seqStorePtr->litStart);

            entropyMetadata->hufMetadata.hufDesSize = ZSTD_buildBlockEntropyStats_literals((void*)seqStorePtr->litStart, litSize, &prevEntropy->huf, &nextEntropy->huf, &entropyMetadata->hufMetadata, ZSTD_disableLiteralsCompression(cctxParams), workspace, wkspSize);

            {
                nuint err_code = (entropyMetadata->hufMetadata.hufDesSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            entropyMetadata->fseMetadata.fseTablesSize = ZSTD_buildBlockEntropyStats_sequences(seqStorePtr, &prevEntropy->fse, &nextEntropy->fse, cctxParams, &entropyMetadata->fseMetadata, workspace, wkspSize);

            {
                nuint err_code = (entropyMetadata->fseMetadata.fseTablesSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /* Returns the size estimate for the literals section (header + content) of a block */
        private static nuint ZSTD_estimateBlockSize_literal(byte* literals, nuint litSize, ZSTD_hufCTables_t* huf, ZSTD_hufCTablesMetadata_t* hufMetadata, void* workspace, nuint wkspSize, int writeEntropy)
        {
            uint* countWksp = (uint*)(workspace);
            uint maxSymbolValue = 255;
            nuint literalSectionHeaderSize = (nuint)(3 + ((litSize >= (uint)(1 * (1 << 10))) ? 1 : 0) + ((litSize >= (uint)(16 * (1 << 10))) ? 1 : 0));
            uint singleStream = ((litSize < 256) ? 1U : 0U);

            if (hufMetadata->hType == symbolEncodingType_e.set_basic)
            {
                return litSize;
            }
            else if (hufMetadata->hType == symbolEncodingType_e.set_rle)
            {
                return 1;
            }
            else if (hufMetadata->hType == symbolEncodingType_e.set_compressed || hufMetadata->hType == symbolEncodingType_e.set_repeat)
            {
                nuint largest = HIST_count_wksp(countWksp, &maxSymbolValue, (void*)(byte*)(literals), litSize, workspace, wkspSize);

                if ((ERR_isError(largest)) != 0)
                {
                    return litSize;
                }


                {
                    nuint cLitSizeEstimate = HUF_estimateCompressedSize((HUF_CElt_s*)(huf->CTable), countWksp, maxSymbolValue);

                    if (writeEntropy != 0)
                    {
                        cLitSizeEstimate += hufMetadata->hufDesSize;
                    }

                    if (singleStream == 0)
                    {
                        cLitSizeEstimate += 6;
                    }

                    return cLitSizeEstimate + literalSectionHeaderSize;
                }
            }

            assert(0 != 0);
            return 0;
        }

        /* Returns the size estimate for the FSE-compressed symbols (of, ml, ll) of a block */
        private static nuint ZSTD_estimateBlockSize_symbolType(symbolEncodingType_e type, byte* codeTable, nuint nbSeq, uint maxCode, uint* fseCTable, uint* additionalBits, short* defaultNorm, uint defaultNormLog, uint defaultMax, void* workspace, nuint wkspSize)
        {
            uint* countWksp = (uint*)(workspace);
            byte* ctp = codeTable;
            byte* ctStart = ctp;
            byte* ctEnd = ctStart + nbSeq;
            nuint cSymbolTypeSizeEstimateInBits = 0;
            uint max = maxCode;

            HIST_countFast_wksp(countWksp, &max, (void*)codeTable, nbSeq, workspace, wkspSize);
            if (type == symbolEncodingType_e.set_basic)
            {
                assert(max <= defaultMax);
                cSymbolTypeSizeEstimateInBits = ZSTD_crossEntropyCost(defaultNorm, defaultNormLog, countWksp, max);
            }
            else if (type == symbolEncodingType_e.set_rle)
            {
                cSymbolTypeSizeEstimateInBits = 0;
            }
            else if (type == symbolEncodingType_e.set_compressed || type == symbolEncodingType_e.set_repeat)
            {
                cSymbolTypeSizeEstimateInBits = ZSTD_fseBitCost(fseCTable, countWksp, max);
            }

            if ((ERR_isError(cSymbolTypeSizeEstimateInBits)) != 0)
            {
                return nbSeq * 10;
            }

            while (ctp < ctEnd)
            {
                if (additionalBits != null)
                {
                    cSymbolTypeSizeEstimateInBits += additionalBits[*ctp];
                }
                else
                {
                    cSymbolTypeSizeEstimateInBits += *ctp;
                }

                ctp++;
            }

            return cSymbolTypeSizeEstimateInBits >> 3;
        }

        /* Returns the size estimate for the sequences section (header + content) of a block */
        private static nuint ZSTD_estimateBlockSize_sequences(byte* ofCodeTable, byte* llCodeTable, byte* mlCodeTable, nuint nbSeq, ZSTD_fseCTables_t* fseTables, ZSTD_fseCTablesMetadata_t* fseMetadata, void* workspace, nuint wkspSize, int writeEntropy)
        {
            nuint sequencesSectionHeaderSize = (nuint)(1 + 1 + ((nbSeq >= 128) ? 1 : 0) + ((nbSeq >= 0x7F00) ? 1 : 0));
            nuint cSeqSizeEstimate = 0;

            cSeqSizeEstimate += ZSTD_estimateBlockSize_symbolType(fseMetadata->ofType, ofCodeTable, nbSeq, 31, (uint*)fseTables->offcodeCTable, (uint*)null, (short*)OF_defaultNorm, OF_defaultNormLog, 28, workspace, wkspSize);
            cSeqSizeEstimate += ZSTD_estimateBlockSize_symbolType(fseMetadata->llType, llCodeTable, nbSeq, 35, (uint*)fseTables->litlengthCTable, (uint*)LL_bits, (short*)LL_defaultNorm, LL_defaultNormLog, 35, workspace, wkspSize);
            cSeqSizeEstimate += ZSTD_estimateBlockSize_symbolType(fseMetadata->mlType, mlCodeTable, nbSeq, 52, (uint*)fseTables->matchlengthCTable, (uint*)ML_bits, (short*)ML_defaultNorm, ML_defaultNormLog, 52, workspace, wkspSize);
            if (writeEntropy != 0)
            {
                cSeqSizeEstimate += fseMetadata->fseTablesSize;
            }

            return cSeqSizeEstimate + sequencesSectionHeaderSize;
        }

        /* Returns the size estimate for a given stream of literals, of, ll, ml */
        private static nuint ZSTD_estimateBlockSize(byte* literals, nuint litSize, byte* ofCodeTable, byte* llCodeTable, byte* mlCodeTable, nuint nbSeq, ZSTD_entropyCTables_t* entropy, ZSTD_entropyCTablesMetadata_t* entropyMetadata, void* workspace, nuint wkspSize, int writeLitEntropy, int writeSeqEntropy)
        {
            nuint literalsSize = ZSTD_estimateBlockSize_literal(literals, litSize, &entropy->huf, &entropyMetadata->hufMetadata, workspace, wkspSize, writeLitEntropy);
            nuint seqSize = ZSTD_estimateBlockSize_sequences(ofCodeTable, llCodeTable, mlCodeTable, nbSeq, &entropy->fse, &entropyMetadata->fseMetadata, workspace, wkspSize, writeSeqEntropy);

            return seqSize + literalsSize + ZSTD_blockHeaderSize;
        }

        /* Builds entropy statistics and uses them for blocksize estimation.
         *
         * Returns the estimated compressed size of the seqStore, or a zstd error.
         */
        private static nuint ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(seqStore_t* seqStore, ZSTD_CCtx_s* zc)
        {
            ZSTD_entropyCTablesMetadata_t entropyMetadata;


            {
                nuint err_code = (ZSTD_buildBlockEntropyStats(seqStore, &zc->blockState.prevCBlock->entropy, &zc->blockState.nextCBlock->entropy, &zc->appliedParams, &entropyMetadata, (void*)zc->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(4) * (uint)((((35) > (52) ? (35) : (52)) + 2))))));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_estimateBlockSize(seqStore->litStart, (nuint)(seqStore->lit - seqStore->litStart), seqStore->ofCode, seqStore->llCode, seqStore->mlCode, (nuint)(seqStore->sequences - seqStore->sequencesStart), &zc->blockState.nextCBlock->entropy, &entropyMetadata, (void*)zc->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2)))), (entropyMetadata.hufMetadata.hType == symbolEncodingType_e.set_compressed ? 1 : 0), 1);
        }

        /* Returns literals bytes represented in a seqStore */
        private static nuint ZSTD_countSeqStoreLiteralsBytes(seqStore_t* seqStore)
        {
            nuint literalsBytes = 0;
            nuint nbSeqs = (nuint)(seqStore->sequences - seqStore->sequencesStart);
            nuint i;

            for (i = 0; i < nbSeqs; ++i)
            {
                seqDef_s seq = seqStore->sequencesStart[i];

                literalsBytes += seq.litLength;
                if (i == seqStore->longLengthPos && seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_literalLength)
                {
                    literalsBytes += 0x10000;
                }
            }

            return literalsBytes;
        }

        /* Returns match bytes represented in a seqStore */
        private static nuint ZSTD_countSeqStoreMatchBytes(seqStore_t* seqStore)
        {
            nuint matchBytes = 0;
            nuint nbSeqs = (nuint)(seqStore->sequences - seqStore->sequencesStart);
            nuint i;

            for (i = 0; i < nbSeqs; ++i)
            {
                seqDef_s seq = seqStore->sequencesStart[i];

                matchBytes += (nuint)(seq.matchLength + 3);
                if (i == seqStore->longLengthPos && seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_matchLength)
                {
                    matchBytes += 0x10000;
                }
            }

            return matchBytes;
        }

        /* Derives the seqStore that is a chunk of the originalSeqStore from [startIdx, endIdx).
         * Stores the result in resultSeqStore.
         */
        private static void ZSTD_deriveSeqStoreChunk(seqStore_t* resultSeqStore, seqStore_t* originalSeqStore, nuint startIdx, nuint endIdx)
        {
            byte* litEnd = originalSeqStore->lit;
            nuint literalsBytes;
            nuint literalsBytesPreceding = 0;

            *resultSeqStore = *originalSeqStore;
            if (startIdx > 0)
            {
                resultSeqStore->sequences = originalSeqStore->sequencesStart + startIdx;
                literalsBytesPreceding = ZSTD_countSeqStoreLiteralsBytes(resultSeqStore);
            }

            if (originalSeqStore->longLengthType != ZSTD_longLengthType_e.ZSTD_llt_none)
            {
                if (originalSeqStore->longLengthPos < startIdx || originalSeqStore->longLengthPos > endIdx)
                {
                    resultSeqStore->longLengthType = ZSTD_longLengthType_e.ZSTD_llt_none;
                }
                else
                {
                    resultSeqStore->longLengthPos -= (uint)(startIdx);
                }
            }

            resultSeqStore->sequencesStart = originalSeqStore->sequencesStart + startIdx;
            resultSeqStore->sequences = originalSeqStore->sequencesStart + endIdx;
            literalsBytes = ZSTD_countSeqStoreLiteralsBytes(resultSeqStore);
            resultSeqStore->litStart += literalsBytesPreceding;
            if (endIdx == (nuint)(originalSeqStore->sequences - originalSeqStore->sequencesStart))
            {
                resultSeqStore->lit = litEnd;
            }
            else
            {
                resultSeqStore->lit = resultSeqStore->litStart + literalsBytes;
            }

            resultSeqStore->llCode += startIdx;
            resultSeqStore->mlCode += startIdx;
            resultSeqStore->ofCode += startIdx;
        }

        /**
         * Returns the raw offset represented by the combination of offCode, ll0, and repcode history.
         * offCode must be an offCode representing a repcode, therefore in the range of [0, 2].
         */
        private static uint ZSTD_resolveRepcodeToRawOffset(uint* rep, uint offCode, uint ll0)
        {
            uint adjustedOffCode = offCode + ll0;

            assert(offCode < 3);
            if (adjustedOffCode == 3)
            {
                assert(rep[0] > 0);
                return rep[0] - 1;
            }

            return rep[adjustedOffCode];
        }

        /**
         * ZSTD_seqStore_resolveOffCodes() reconciles any possible divergences in offset history that may arise
         * due to emission of RLE/raw blocks that disturb the offset history, and replaces any repcodes within
         * the seqStore that may be invalid.
         *
         * dRepcodes are updated as would be on the decompression side. cRepcodes are updated exactly in
         * accordance with the seqStore.
         */
        private static void ZSTD_seqStore_resolveOffCodes(repcodes_s* dRepcodes, repcodes_s* cRepcodes, seqStore_t* seqStore, uint nbSeq)
        {
            uint idx = 0;

            for (; idx < nbSeq; ++idx)
            {
                seqDef_s* seq = seqStore->sequencesStart + idx;
                uint ll0 = (((seq->litLength == 0)) ? 1U : 0U);
                uint offCode = seq->offset - 1;

                assert(seq->offset > 0);
                if (offCode <= (uint)((3 - 1)))
                {
                    uint dRawOffset = ZSTD_resolveRepcodeToRawOffset(dRepcodes->rep, offCode, ll0);
                    uint cRawOffset = ZSTD_resolveRepcodeToRawOffset(cRepcodes->rep, offCode, ll0);

                    if (dRawOffset != cRawOffset)
                    {
                        seq->offset = cRawOffset + 3;
                    }
                }

                *dRepcodes = ZSTD_updateRep(dRepcodes->rep, seq->offset - 1, ll0);
                *cRepcodes = ZSTD_updateRep(cRepcodes->rep, offCode, ll0);
            }
        }

        /* ZSTD_compressSeqStore_singleBlock():
         * Compresses a seqStore into a block with a block header, into the buffer dst.
         *
         * Returns the total size of that block (including header) or a ZSTD error code.
         */
        private static nuint ZSTD_compressSeqStore_singleBlock(ZSTD_CCtx_s* zc, seqStore_t* seqStore, repcodes_s* dRep, repcodes_s* cRep, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint lastBlock, uint isPartition)
        {
            uint rleMaxLength = 25;
            byte* op = (byte*)(dst);
            byte* ip = (byte*)(src);
            nuint cSize;
            nuint cSeqsSize;
            repcodes_s dRepOriginal = *dRep;

            if (isPartition != 0)
            {
                ZSTD_seqStore_resolveOffCodes(dRep, cRep, seqStore, (uint)(seqStore->sequences - seqStore->sequencesStart));
            }

            cSeqsSize = ZSTD_entropyCompressSeqStore(seqStore, &zc->blockState.prevCBlock->entropy, &zc->blockState.nextCBlock->entropy, &zc->appliedParams, (void*)(op + ZSTD_blockHeaderSize), dstCapacity - ZSTD_blockHeaderSize, srcSize, (void*)zc->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2)))), zc->bmi2);

            {
                nuint err_code = (cSeqsSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            if (zc->isFirstBlock == 0 && cSeqsSize < rleMaxLength && (ZSTD_isRLE((byte*)(src), srcSize)) != 0)
            {
                cSeqsSize = 1;
            }

            if (zc->seqCollector.collectSequences != 0)
            {
                ZSTD_copyBlockSequences(zc);
                ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
                return 0;
            }

            if (zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid)
            {
                zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
            }

            if (cSeqsSize == 0)
            {
                cSize = ZSTD_noCompressBlock((void*)op, dstCapacity, (void*)ip, srcSize, lastBlock);

                {
                    nuint err_code = (cSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                *dRep = dRepOriginal;
            }
            else if (cSeqsSize == 1)
            {
                cSize = ZSTD_rleCompressBlock((void*)op, dstCapacity, *ip, srcSize, lastBlock);

                {
                    nuint err_code = (cSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                *dRep = dRepOriginal;
            }
            else
            {
                ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
                writeBlockHeader((void*)op, cSeqsSize, srcSize, lastBlock);
                cSize = ZSTD_blockHeaderSize + cSeqsSize;
            }

            return cSize;
        }

        /* Helper function to perform the recursive search for block splits.
         * Estimates the cost of seqStore prior to split, and estimates the cost of splitting the sequences in half.
         * If advantageous to split, then we recurse down the two sub-blocks. If not, or if an error occurred in estimation, then
         * we do not recurse.
         *
         * Note: The recursion depth is capped by a heuristic minimum number of sequences, defined by MIN_SEQUENCES_BLOCK_SPLITTING.
         * In theory, this means the absolute largest recursion depth is 10 == log2(maxNbSeqInBlock/MIN_SEQUENCES_BLOCK_SPLITTING).
         * In practice, recursion depth usually doesn't go beyond 4.
         *
         * Furthermore, the number of splits is capped by MAX_NB_SPLITS. At MAX_NB_SPLITS == 196 with the current existing blockSize
         * maximum of 128 KB, this value is actually impossible to reach.
         */
        private static void ZSTD_deriveBlockSplitsHelper(seqStoreSplits* splits, nuint startIdx, nuint endIdx, ZSTD_CCtx_s* zc, seqStore_t* origSeqStore)
        {
            seqStore_t fullSeqStoreChunk;
            seqStore_t firstHalfSeqStore;
            seqStore_t secondHalfSeqStore;
            nuint estimatedOriginalSize;
            nuint estimatedFirstHalfSize;
            nuint estimatedSecondHalfSize;
            nuint midIdx = (startIdx + endIdx) / 2;

            if (endIdx - startIdx < 300 || splits->idx >= 196)
            {
                return;
            }

            ZSTD_deriveSeqStoreChunk(&fullSeqStoreChunk, origSeqStore, startIdx, endIdx);
            ZSTD_deriveSeqStoreChunk(&firstHalfSeqStore, origSeqStore, startIdx, midIdx);
            ZSTD_deriveSeqStoreChunk(&secondHalfSeqStore, origSeqStore, midIdx, endIdx);
            estimatedOriginalSize = ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(&fullSeqStoreChunk, zc);
            estimatedFirstHalfSize = ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(&firstHalfSeqStore, zc);
            estimatedSecondHalfSize = ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(&secondHalfSeqStore, zc);
            if ((ERR_isError(estimatedOriginalSize)) != 0 || (ERR_isError(estimatedFirstHalfSize)) != 0 || (ERR_isError(estimatedSecondHalfSize)) != 0)
            {
                return;
            }

            if (estimatedFirstHalfSize + estimatedSecondHalfSize < estimatedOriginalSize)
            {
                ZSTD_deriveBlockSplitsHelper(splits, startIdx, midIdx, zc, origSeqStore);
                splits->splitLocations[splits->idx] = (uint)(midIdx);
                splits->idx++;
                ZSTD_deriveBlockSplitsHelper(splits, midIdx, endIdx, zc, origSeqStore);
            }
        }

        /* Base recursive function. Populates a table with intra-block partition indices that can improve compression ratio.
         *
         * Returns the number of splits made (which equals the size of the partition table - 1).
         */
        private static nuint ZSTD_deriveBlockSplits(ZSTD_CCtx_s* zc, uint* partitions, uint nbSeq)
        {
            seqStoreSplits splits = new seqStoreSplits
            {
                splitLocations = partitions,
                idx = 0,
            };

            if (nbSeq <= 4)
            {
                return 0;
            }

            ZSTD_deriveBlockSplitsHelper(&splits, 0, nbSeq, zc, &zc->seqStore);
            splits.splitLocations[splits.idx] = nbSeq;
            return splits.idx;
        }

        /* ZSTD_compressBlock_splitBlock():
         * Attempts to split a given block into multiple blocks to improve compression ratio.
         *
         * Returns combined size of all blocks (which includes headers), or a ZSTD error code.
         */
        private static nuint ZSTD_compressBlock_splitBlock_internal(ZSTD_CCtx_s* zc, void* dst, nuint dstCapacity, void* src, nuint blockSize, uint lastBlock, uint nbSeq)
        {
            nuint cSize = 0;
            byte* ip = (byte*)(src);
            byte* op = (byte*)(dst);
            uint* partitions = stackalloc uint[196];
            nuint i = 0;
            nuint srcBytesTotal = 0;
            nuint numSplits = ZSTD_deriveBlockSplits(zc, partitions, nbSeq);
            seqStore_t nextSeqStore;
            var _ = &nextSeqStore;
            seqStore_t currSeqStore;
            repcodes_s dRep;
            repcodes_s cRep;

            memcpy((void*)(dRep.rep), (void*)(zc->blockState.prevCBlock->rep), ((nuint)(sizeof(repcodes_s))));
            memcpy((void*)(cRep.rep), (void*)(zc->blockState.prevCBlock->rep), ((nuint)(sizeof(repcodes_s))));
            if (numSplits == 0)
            {
                nuint cSizeSingleBlock = ZSTD_compressSeqStore_singleBlock(zc, &zc->seqStore, &dRep, &cRep, (void*)op, dstCapacity, (void*)ip, blockSize, lastBlock, 0);


                {
                    nuint err_code = (cSizeSingleBlock);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(cSizeSingleBlock <= (uint)((1 << 17)) + ZSTD_blockHeaderSize);
                return cSizeSingleBlock;
            }

            ZSTD_deriveSeqStoreChunk(&currSeqStore, &zc->seqStore, 0, partitions[0]);
            for (i = 0; i <= numSplits; ++i)
            {
                nuint srcBytes;
                nuint cSizeChunk;
                uint lastPartition = (((i == numSplits)) ? 1U : 0U);
                uint lastBlockEntireSrc = 0;

                srcBytes = ZSTD_countSeqStoreLiteralsBytes(&currSeqStore) + ZSTD_countSeqStoreMatchBytes(&currSeqStore);
                srcBytesTotal += srcBytes;
                if (lastPartition != 0)
                {
                    srcBytes += blockSize - srcBytesTotal;
                    lastBlockEntireSrc = lastBlock;
                }
                else
                {
                    ZSTD_deriveSeqStoreChunk(&nextSeqStore, &zc->seqStore, partitions[i], partitions[i + 1]);
                }

                cSizeChunk = ZSTD_compressSeqStore_singleBlock(zc, &currSeqStore, &dRep, &cRep, (void*)op, dstCapacity, (void*)ip, srcBytes, lastBlockEntireSrc, 1);

                {
                    nuint err_code = (cSizeChunk);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                ip += srcBytes;
                op += cSizeChunk;
                dstCapacity -= cSizeChunk;
                cSize += cSizeChunk;
                currSeqStore = nextSeqStore;
                assert(cSizeChunk <= (uint)((1 << 17)) + ZSTD_blockHeaderSize);
            }

            memcpy((void*)(zc->blockState.prevCBlock->rep), (void*)(dRep.rep), ((nuint)(sizeof(repcodes_s))));
            return cSize;
        }

        private static nuint ZSTD_compressBlock_splitBlock(ZSTD_CCtx_s* zc, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint lastBlock)
        {
            byte* ip = (byte*)(src);
            byte* op = (byte*)(dst);
            uint nbSeq;
            nuint cSize;


            {
                nuint bss = ZSTD_buildSeqStore(zc, src, srcSize);


                {
                    nuint err_code = (bss);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (bss == (nuint)ZSTD_buildSeqStore_e.ZSTDbss_noCompress)
                {
                    if (zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid)
                    {
                        zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
                    }

                    cSize = ZSTD_noCompressBlock((void*)op, dstCapacity, (void*)ip, srcSize, lastBlock);

                    {
                        nuint err_code = (cSize);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    return cSize;
                }

                nbSeq = (uint)(zc->seqStore.sequences - zc->seqStore.sequencesStart);
            }

            assert(zc->appliedParams.splitBlocks == 1);
            cSize = ZSTD_compressBlock_splitBlock_internal(zc, dst, dstCapacity, src, srcSize, lastBlock, nbSeq);

            {
                nuint err_code = (cSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return cSize;
        }

        private static nuint ZSTD_compressBlock_internal(ZSTD_CCtx_s* zc, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint frame)
        {
            uint rleMaxLength = 25;
            nuint cSize;
            byte* ip = (byte*)(src);
            byte* op = (byte*)(dst);


            {
                nuint bss = ZSTD_buildSeqStore(zc, src, srcSize);


                {
                    nuint err_code = (bss);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (bss == (nuint)ZSTD_buildSeqStore_e.ZSTDbss_noCompress)
                {
                    cSize = 0;
                    goto @out;
                }
            }

            if (zc->seqCollector.collectSequences != 0)
            {
                ZSTD_copyBlockSequences(zc);
                ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
                return 0;
            }

            cSize = ZSTD_entropyCompressSeqStore(&zc->seqStore, &zc->blockState.prevCBlock->entropy, &zc->blockState.nextCBlock->entropy, &zc->appliedParams, dst, dstCapacity, srcSize, (void*)zc->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2)))), zc->bmi2);
            if (zc->seqCollector.collectSequences != 0)
            {
                ZSTD_copyBlockSequences(zc);
                return 0;
            }

            if (frame != 0 && zc->isFirstBlock == 0 && cSize < rleMaxLength && (ZSTD_isRLE(ip, srcSize)) != 0)
            {
                cSize = 1;
                op[0] = ip[0];
            }

            @out:
            if ((ERR_isError(cSize)) == 0 && cSize > 1)
            {
                ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
            }

            if (zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid)
            {
                zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
            }

            return cSize;
        }

        private static nuint ZSTD_compressBlock_targetCBlockSize_body(ZSTD_CCtx_s* zc, void* dst, nuint dstCapacity, void* src, nuint srcSize, nuint bss, uint lastBlock)
        {
            if (bss == (nuint)ZSTD_buildSeqStore_e.ZSTDbss_compress)
            {
                if (zc->isFirstBlock == 0 && (ZSTD_maybeRLE(&zc->seqStore)) != 0 && (ZSTD_isRLE((byte*)(src), srcSize)) != 0)
                {
                    return ZSTD_rleCompressBlock(dst, dstCapacity, *(byte*)(src), srcSize, lastBlock);
                }


                {
                    nuint cSize = ZSTD_compressSuperBlock(zc, dst, dstCapacity, src, srcSize, lastBlock);

                    if (cSize != (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall))))
                    {
                        nuint maxCSize = srcSize - ZSTD_minGain(srcSize, zc->appliedParams.cParams.strategy);


                        {
                            nuint err_code = (cSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        if (cSize != 0 && cSize < maxCSize + ZSTD_blockHeaderSize)
                        {
                            ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
                            return cSize;
                        }
                    }
                }
            }

            return ZSTD_noCompressBlock(dst, dstCapacity, src, srcSize, lastBlock);
        }

        private static nuint ZSTD_compressBlock_targetCBlockSize(ZSTD_CCtx_s* zc, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint lastBlock)
        {
            nuint cSize = 0;
            nuint bss = ZSTD_buildSeqStore(zc, src, srcSize);


            {
                nuint err_code = (bss);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            cSize = ZSTD_compressBlock_targetCBlockSize_body(zc, dst, dstCapacity, src, srcSize, bss, lastBlock);

            {
                nuint err_code = (cSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            if (zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid)
            {
                zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
            }

            return cSize;
        }

        private static void ZSTD_overflowCorrectIfNeeded(ZSTD_matchState_t* ms, ZSTD_cwksp* ws, ZSTD_CCtx_params_s* @params, void* ip, void* iend)
        {
            uint cycleLog = ZSTD_cycleLog(@params->cParams.chainLog, @params->cParams.strategy);
            uint maxDist = (uint)(1) << (int)@params->cParams.windowLog;

            if ((ZSTD_window_needOverflowCorrection(ms->window, cycleLog, maxDist, ms->loadedDictEnd, ip, iend)) != 0)
            {
                uint correction = ZSTD_window_correctOverflow(&ms->window, cycleLog, maxDist, ip);

                ZSTD_cwksp_mark_tables_dirty(ws);
                ZSTD_reduceIndex(ms, @params, correction);
                ZSTD_cwksp_mark_tables_clean(ws);
                if (ms->nextToUpdate < correction)
                {
                    ms->nextToUpdate = 0;
                }
                else
                {
                    ms->nextToUpdate -= correction;
                }

                ms->loadedDictEnd = 0;
                ms->dictMatchState = null;
            }
        }

        /*! ZSTD_compress_frameChunk() :
        *   Compress a chunk of data into one or multiple blocks.
        *   All blocks will be terminated, all input will be consumed.
        *   Function will issue an error if there is not enough `dstCapacity` to hold the compressed content.
        *   Frame is supposed already started (header already produced)
        *   @return : compressed size, or an error code
        */
        private static nuint ZSTD_compress_frameChunk(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint lastFrameChunk)
        {
            nuint blockSize = cctx->blockSize;
            nuint remaining = srcSize;
            byte* ip = (byte*)(src);
            byte* ostart = (byte*)(dst);
            byte* op = ostart;
            uint maxDist = (uint)(1) << (int)cctx->appliedParams.cParams.windowLog;

            assert(cctx->appliedParams.cParams.windowLog <= (uint)(((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))));
            if (cctx->appliedParams.fParams.checksumFlag != 0 && srcSize != 0)
            {
                XXH64_update(&cctx->xxhState, src, srcSize);
            }

            while (remaining != 0)
            {
                ZSTD_matchState_t* ms = &cctx->blockState.matchState;
                uint lastBlock = lastFrameChunk & (uint)((((blockSize >= remaining)) ? 1 : 0));

                if (dstCapacity < ZSTD_blockHeaderSize + (uint)((1 + 1 + 1)))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                if (remaining < blockSize)
                {
                    blockSize = remaining;
                }

                ZSTD_overflowCorrectIfNeeded(ms, &cctx->workspace, &cctx->appliedParams, (void*)ip, (void*)(ip + blockSize));
                ZSTD_checkDictValidity(&ms->window, (void*)(ip + blockSize), maxDist, &ms->loadedDictEnd, &ms->dictMatchState);
                if (ms->nextToUpdate < ms->window.lowLimit)
                {
                    ms->nextToUpdate = ms->window.lowLimit;
                }


                {
                    nuint cSize;

                    if ((ZSTD_useTargetCBlockSize(&cctx->appliedParams)) != 0)
                    {
                        cSize = ZSTD_compressBlock_targetCBlockSize(cctx, (void*)op, dstCapacity, (void*)ip, blockSize, lastBlock);

                        {
                            nuint err_code = (cSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        assert(cSize > 0);
                        assert(cSize <= blockSize + ZSTD_blockHeaderSize);
                    }
                    else if ((ZSTD_blockSplitterEnabled(&cctx->appliedParams)) != 0)
                    {
                        cSize = ZSTD_compressBlock_splitBlock(cctx, (void*)op, dstCapacity, (void*)ip, blockSize, lastBlock);

                        {
                            nuint err_code = (cSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        assert(cSize > 0 || cctx->seqCollector.collectSequences == 1);
                    }
                    else
                    {
                        cSize = ZSTD_compressBlock_internal(cctx, (void*)(op + ZSTD_blockHeaderSize), dstCapacity - ZSTD_blockHeaderSize, (void*)ip, blockSize, 1);

                        {
                            nuint err_code = (cSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        if (cSize == 0)
                        {
                            cSize = ZSTD_noCompressBlock((void*)op, dstCapacity, (void*)ip, blockSize, lastBlock);

                            {
                                nuint err_code = (cSize);

                                if ((ERR_isError(err_code)) != 0)
                                {
                                    return err_code;
                                }
                            }

                        }
                        else
                        {
                            uint cBlockHeader = cSize == 1 ? lastBlock + (((uint)(blockType_e.bt_rle)) << 1) + (uint)(blockSize << 3) : lastBlock + (((uint)(blockType_e.bt_compressed)) << 1) + (uint)(cSize << 3);

                            MEM_writeLE24((void*)op, cBlockHeader);
                            cSize += ZSTD_blockHeaderSize;
                        }
                    }

                    ip += blockSize;
                    assert(remaining >= blockSize);
                    remaining -= blockSize;
                    op += cSize;
                    assert(dstCapacity >= cSize);
                    dstCapacity -= cSize;
                    cctx->isFirstBlock = 0;
                }
            }

            if (lastFrameChunk != 0 && (op > ostart))
            {
                cctx->stage = ZSTD_compressionStage_e.ZSTDcs_ending;
            }

            return (nuint)(op - ostart);
        }

        private static nuint ZSTD_writeFrameHeader(void* dst, nuint dstCapacity, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize, uint dictID)
        {
            byte* op = (byte*)(dst);
            uint dictIDSizeCodeLength = (uint)(((dictID > 0) ? 1 : 0) + ((dictID >= 256) ? 1 : 0) + ((dictID >= 65536) ? 1 : 0));
            uint dictIDSizeCode = (uint)(@params->fParams.noDictIDFlag != 0 ? 0 : dictIDSizeCodeLength);
            uint checksumFlag = ((@params->fParams.checksumFlag > 0) ? 1U : 0U);
            uint windowSize = (uint)(1) << (int)@params->cParams.windowLog;
            uint singleSegment = ((@params->fParams.contentSizeFlag != 0 && (windowSize >= pledgedSrcSize)) ? 1U : 0U);
            byte windowLogByte = (byte)((@params->cParams.windowLog - 10) << 3);
            uint fcsCode = (uint)(@params->fParams.contentSizeFlag != 0 ? ((pledgedSrcSize >= 256) ? 1 : 0) + ((pledgedSrcSize >= (uint)(65536 + 256)) ? 1 : 0) + ((pledgedSrcSize >= 0xFFFFFFFFU) ? 1 : 0) : 0);
            byte frameHeaderDescriptionByte = (byte)(dictIDSizeCode + (checksumFlag << 2) + (singleSegment << 5) + (fcsCode << 6));
            nuint pos = 0;

            assert(!(@params->fParams.contentSizeFlag != 0 && pledgedSrcSize == (unchecked(0UL - 1))));
            if (dstCapacity < 18)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (@params->format == ZSTD_format_e.ZSTD_f_zstd1)
            {
                MEM_writeLE32(dst, 0xFD2FB528);
                pos = 4;
            }

            op[pos++] = frameHeaderDescriptionByte;
            if (singleSegment == 0)
            {
                op[pos++] = windowLogByte;
            }

            switch (dictIDSizeCode)
            {
                default:
                {
                    assert(0 != 0);
                }


                goto case 0;
                case 0:
                {
                    break;
                }

                case 1:
                {
                    op[pos] = (byte)(dictID);
                }

                pos++;
                break;
                case 2:
                {
                    MEM_writeLE16((void*)(op + pos), (ushort)(dictID));
                }

                pos += 2;
                break;
                case 3:
                {
                    MEM_writeLE32((void*)(op + pos), dictID);
                }

                pos += 4;
                break;
            }

            switch (fcsCode)
            {
                default:
                {
                    assert(0 != 0);
                }


                goto case 0;
                case 0:
                {
                    if (singleSegment != 0)
                    {
                        op[pos++] = (byte)(pledgedSrcSize);
                    }
                }

                break;
                case 1:
                {
                    MEM_writeLE16((void*)(op + pos), (ushort)(pledgedSrcSize - 256));
                }

                pos += 2;
                break;
                case 2:
                {
                    MEM_writeLE32((void*)(op + pos), (uint)(pledgedSrcSize));
                }

                pos += 4;
                break;
                case 3:
                {
                    MEM_writeLE64((void*)(op + pos), (ulong)(pledgedSrcSize));
                }

                pos += 8;
                break;
            }

            return pos;
        }

        /* ZSTD_writeSkippableFrame_advanced() :
         * Writes out a skippable frame with the specified magic number variant (16 are supported),
         * from ZSTD_MAGIC_SKIPPABLE_START to ZSTD_MAGIC_SKIPPABLE_START+15, and the desired source data.
         *
         * Returns the total number of bytes written, or a ZSTD error code.
         */
        public static nuint ZSTD_writeSkippableFrame(void* dst, nuint dstCapacity, void* src, nuint srcSize, uint magicVariant)
        {
            byte* op = (byte*)(dst);

            if (dstCapacity < srcSize + 8)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (srcSize > (uint)(0xFFFFFFFF))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if (magicVariant > 15)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
            }

            MEM_writeLE32((void*)op, (uint)(0x184D2A50 + magicVariant));
            MEM_writeLE32((void*)(op + 4), (uint)(srcSize));
            memcpy((void*)((op + 8)), (src), (srcSize));
            return srcSize + 8;
        }

        /* ZSTD_writeLastEmptyBlock() :
         * output an empty Block with end-of-frame mark to complete a frame
         * @return : size of data written into `dst` (== ZSTD_blockHeaderSize (defined in zstd_internal.h))
         *           or an error code if `dstCapacity` is too small (<ZSTD_blockHeaderSize)
         */
        public static nuint ZSTD_writeLastEmptyBlock(void* dst, nuint dstCapacity)
        {
            if (dstCapacity < ZSTD_blockHeaderSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }


            {
                uint cBlockHeader24 = 1 + (((uint)(blockType_e.bt_raw)) << 1);

                MEM_writeLE24(dst, cBlockHeader24);
                return ZSTD_blockHeaderSize;
            }
        }

        /* ZSTD_referenceExternalSequences() :
         * Must be called before starting a compression operation.
         * seqs must parse a prefix of the source.
         * This cannot be used when long range matching is enabled.
         * Zstd will use these sequences, and pass the literals to a secondary block
         * compressor.
         * @return : An error code on failure.
         * NOTE: seqs are not verified! Invalid sequences can cause out-of-bounds memory
         * access and data corruption.
         */
        public static nuint ZSTD_referenceExternalSequences(ZSTD_CCtx_s* cctx, rawSeq* seq, nuint nbSeq)
        {
            if (cctx->stage != ZSTD_compressionStage_e.ZSTDcs_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            if (cctx->appliedParams.ldmParams.enableLdm != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
            }

            cctx->externSeqStore.seq = seq;
            cctx->externSeqStore.size = nbSeq;
            cctx->externSeqStore.capacity = nbSeq;
            cctx->externSeqStore.pos = 0;
            cctx->externSeqStore.posInSequence = 0;
            return 0;
        }

        private static nuint ZSTD_compressContinue_internal(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint frame, uint lastFrameChunk)
        {
            ZSTD_matchState_t* ms = &cctx->blockState.matchState;
            nuint fhSize = 0;

            if (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_created)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            if (frame != 0 && (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_init))
            {
                fhSize = ZSTD_writeFrameHeader(dst, dstCapacity, &cctx->appliedParams, cctx->pledgedSrcSizePlusOne - 1, cctx->dictID);

                {
                    nuint err_code = (fhSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(fhSize <= dstCapacity);
                dstCapacity -= fhSize;
                dst = (sbyte*)(dst) + fhSize;
                cctx->stage = ZSTD_compressionStage_e.ZSTDcs_ongoing;
            }

            if (srcSize == 0)
            {
                return fhSize;
            }

            if ((ZSTD_window_update(&ms->window, src, srcSize, (int)ms->forceNonContiguous)) == 0)
            {
                ms->forceNonContiguous = 0;
                ms->nextToUpdate = ms->window.dictLimit;
            }

            if (cctx->appliedParams.ldmParams.enableLdm != 0)
            {
                ZSTD_window_update(&cctx->ldmState.window, src, srcSize, 0);
            }

            if (frame == 0)
            {
                ZSTD_overflowCorrectIfNeeded(ms, &cctx->workspace, &cctx->appliedParams, src, (void*)((byte*)(src) + srcSize));
            }


            {
                nuint cSize = frame != 0 ? ZSTD_compress_frameChunk(cctx, dst, dstCapacity, src, srcSize, lastFrameChunk) : ZSTD_compressBlock_internal(cctx, dst, dstCapacity, src, srcSize, 0);


                {
                    nuint err_code = (cSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                cctx->consumedSrcSize += (ulong)srcSize;
                cctx->producedCSize += (ulong)(cSize + fhSize);
                assert(!(cctx->appliedParams.fParams.contentSizeFlag != 0 && cctx->pledgedSrcSizePlusOne == 0));
                if (cctx->pledgedSrcSizePlusOne != 0)
                {
                    if (cctx->consumedSrcSize + 1 > cctx->pledgedSrcSizePlusOne)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                    }

                }

                return cSize + fhSize;
            }
        }

        public static nuint ZSTD_compressContinue(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            return ZSTD_compressContinue_internal(cctx, dst, dstCapacity, src, srcSize, 1, 0);
        }

        /*=====   Raw zstd block functions  =====*/
        public static nuint ZSTD_getBlockSize(ZSTD_CCtx_s* cctx)
        {
            ZSTD_compressionParameters cParams = cctx->appliedParams.cParams;

            assert((ZSTD_checkCParams(cParams)) == 0);
            return ((uint)(((1 << 17))) < ((uint)(1) << (int)cParams.windowLog) ? ((1 << 17)) : ((uint)(1) << (int)cParams.windowLog));
        }

        public static nuint ZSTD_compressBlock(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {

            {
                nuint blockSizeMax = ZSTD_getBlockSize(cctx);

                if (srcSize > blockSizeMax)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                }

            }

            return ZSTD_compressContinue_internal(cctx, dst, dstCapacity, src, srcSize, 0, 0);
        }

        /*! ZSTD_loadDictionaryContent() :
         *  @return : 0, or an error code
         */
        private static nuint ZSTD_loadDictionaryContent(ZSTD_matchState_t* ms, ldmState_t* ls, ZSTD_cwksp* ws, ZSTD_CCtx_params_s* @params, void* src, nuint srcSize, ZSTD_dictTableLoadMethod_e dtlm)
        {
            byte* ip = (byte*)(src);
            byte* iend = ip + srcSize;
            int loadLdmDict = ((@params->ldmParams.enableLdm != 0 && ls != null) ? 1 : 0);

            ZSTD_assertEqualCParams(@params->cParams, ms->cParams);
            if (srcSize > ((unchecked((uint)(-1))) - ((3U << 29) + (1U << ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))))))
            {
                uint maxDictSize = ((3U << 29) + (1U << (unchecked((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))))) - 1;

                assert((ZSTD_window_isEmpty(ms->window)) != 0);
                if (loadLdmDict != 0)
                {
                    assert((ZSTD_window_isEmpty(ls->window)) != 0);
                }

                if (srcSize > maxDictSize)
                {
                    ip = iend - maxDictSize;
                    src = ip;
                    srcSize = maxDictSize;
                }
            }

            ZSTD_window_update(&ms->window, src, srcSize, 0);
            ms->loadedDictEnd = (uint)(@params->forceWindow != 0 ? 0 : (uint)(iend - ms->window.@base));
            ms->forceNonContiguous = (uint)@params->deterministicRefPrefix;
            if (loadLdmDict != 0)
            {
                ZSTD_window_update(&ls->window, src, srcSize, 0);
                ls->loadedDictEnd = (uint)(@params->forceWindow != 0 ? 0 : (uint)(iend - ls->window.@base));
            }

            if (srcSize <= 8)
            {
                return 0;
            }

            ZSTD_overflowCorrectIfNeeded(ms, ws, @params, (void*)ip, (void*)iend);
            if (loadLdmDict != 0)
            {
                ZSTD_ldm_fillHashTable(ls, ip, iend, &@params->ldmParams);
            }

            switch (@params->cParams.strategy)
            {
                case ZSTD_strategy.ZSTD_fast:
                {
                    ZSTD_fillHashTable(ms, (void*)iend, dtlm);
                }

                break;
                case ZSTD_strategy.ZSTD_dfast:
                {
                    ZSTD_fillDoubleHashTable(ms, (void*)iend, dtlm);
                }

                break;
                case ZSTD_strategy.ZSTD_greedy:
                case ZSTD_strategy.ZSTD_lazy:
                case ZSTD_strategy.ZSTD_lazy2:
                {
                    assert(srcSize >= 8);
                }

                if (ms->dedicatedDictSearch != 0)
                {
                    assert(ms->chainTable != null);
                    ZSTD_dedicatedDictSearch_lazy_loadDictionary(ms, iend - 8);
                }
                else
                {
                    assert(@params->useRowMatchFinder != ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto);
                    if (@params->useRowMatchFinder == ZSTD_useRowMatchFinderMode_e.ZSTD_urm_enableRowMatchFinder)
                    {
                        nuint tagTableSize = ((nuint)(1) << (int)@params->cParams.hashLog) * (nuint)(2);

                        memset((void*)(ms->tagTable), (0), (tagTableSize));
                        ZSTD_row_update(ms, iend - 8);
                    }
                    else
                    {
                        ZSTD_insertAndFindFirstIndex(ms, iend - 8);
                    }
                }

                break;
                case ZSTD_strategy.ZSTD_btlazy2:
                case ZSTD_strategy.ZSTD_btopt:
                case ZSTD_strategy.ZSTD_btultra:
                case ZSTD_strategy.ZSTD_btultra2:
                {
                    assert(srcSize >= 8);
                }

                ZSTD_updateTree(ms, iend - 8, iend);
                break;
                default:
                {
                    assert(0 != 0);
                }
                break;
            }

            ms->nextToUpdate = (uint)(iend - ms->window.@base);
            return 0;
        }

        /* Dictionaries that assign zero probability to symbols that show up causes problems
         * when FSE encoding. Mark dictionaries with zero probability symbols as FSE_repeat_check
         * and only dictionaries with 100% valid symbols can be assumed valid.
         */
        private static FSE_repeat ZSTD_dictNCountRepeat(short* normalizedCounter, uint dictMaxSymbolValue, uint maxSymbolValue)
        {
            uint s;

            if (dictMaxSymbolValue < maxSymbolValue)
            {
                return FSE_repeat.FSE_repeat_check;
            }

            for (s = 0; s <= maxSymbolValue; ++s)
            {
                if (normalizedCounter[s] == 0)
                {
                    return FSE_repeat.FSE_repeat_check;
                }
            }

            return FSE_repeat.FSE_repeat_valid;
        }

        /* ZSTD_loadCEntropy() :
         * dict : must point at beginning of a valid zstd dictionary.
         * return : size of dictionary header (size of magic number + dict ID + entropy tables)
         * assumptions : magic number supposed already checked
         *               and dictSize >= 8 */
        public static nuint ZSTD_loadCEntropy(ZSTD_compressedBlockState_t* bs, void* workspace, void* dict, nuint dictSize)
        {
            short* offcodeNCount = stackalloc short[32];
            uint offcodeMaxValue = 31;
            byte* dictPtr = (byte*)(dict);
            byte* dictEnd = dictPtr + dictSize;

            dictPtr += 8;
            bs->entropy.huf.repeatMode = HUF_repeat.HUF_repeat_check;

            {
                uint maxSymbolValue = 255;
                uint hasZeroWeights = 1;
                nuint hufHeaderSize = HUF_readCTable((HUF_CElt_s*)(bs->entropy.huf.CTable), &maxSymbolValue, (void*)dictPtr, (nuint)(dictEnd - dictPtr), &hasZeroWeights);

                if (hasZeroWeights == 0)
                {
                    bs->entropy.huf.repeatMode = HUF_repeat.HUF_repeat_valid;
                }

                if ((ERR_isError(hufHeaderSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (maxSymbolValue < 255)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                dictPtr += hufHeaderSize;
            }


            {
                uint offcodeLog;
                nuint offcodeHeaderSize = FSE_readNCount((short*)offcodeNCount, &offcodeMaxValue, &offcodeLog, (void*)dictPtr, (nuint)(dictEnd - dictPtr));

                if ((ERR_isError(offcodeHeaderSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (offcodeLog > 8)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if ((ERR_isError(FSE_buildCTable_wksp((uint*)bs->entropy.fse.offcodeCTable, (short*)offcodeNCount, 31, offcodeLog, workspace, (nuint)(((6 << 10) + 256))))) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                dictPtr += offcodeHeaderSize;
            }


            {
                short* matchlengthNCount = stackalloc short[53];
                uint matchlengthMaxValue = 52, matchlengthLog;
                nuint matchlengthHeaderSize = FSE_readNCount((short*)matchlengthNCount, &matchlengthMaxValue, &matchlengthLog, (void*)dictPtr, (nuint)(dictEnd - dictPtr));

                if ((ERR_isError(matchlengthHeaderSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (matchlengthLog > 9)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if ((ERR_isError(FSE_buildCTable_wksp((uint*)bs->entropy.fse.matchlengthCTable, (short*)matchlengthNCount, matchlengthMaxValue, matchlengthLog, workspace, (nuint)(((6 << 10) + 256))))) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                bs->entropy.fse.matchlength_repeatMode = ZSTD_dictNCountRepeat((short*)matchlengthNCount, matchlengthMaxValue, 52);
                dictPtr += matchlengthHeaderSize;
            }


            {
                short* litlengthNCount = stackalloc short[36];
                uint litlengthMaxValue = 35, litlengthLog;
                nuint litlengthHeaderSize = FSE_readNCount((short*)litlengthNCount, &litlengthMaxValue, &litlengthLog, (void*)dictPtr, (nuint)(dictEnd - dictPtr));

                if ((ERR_isError(litlengthHeaderSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (litlengthLog > 9)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if ((ERR_isError(FSE_buildCTable_wksp((uint*)bs->entropy.fse.litlengthCTable, (short*)litlengthNCount, litlengthMaxValue, litlengthLog, workspace, (nuint)(((6 << 10) + 256))))) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                bs->entropy.fse.litlength_repeatMode = ZSTD_dictNCountRepeat((short*)litlengthNCount, litlengthMaxValue, 35);
                dictPtr += litlengthHeaderSize;
            }

            if (dictPtr + 12 > dictEnd)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
            }

            bs->rep[0] = MEM_readLE32((void*)(dictPtr + 0));
            bs->rep[1] = MEM_readLE32((void*)(dictPtr + 4));
            bs->rep[2] = MEM_readLE32((void*)(dictPtr + 8));
            dictPtr += 12;

            {
                nuint dictContentSize = (nuint)(dictEnd - dictPtr);
                uint offcodeMax = 31;

                if (dictContentSize <= (unchecked((uint)(-1))) - (uint)(128 * (1 << 10)))
                {
                    uint maxOffset = (uint)(dictContentSize) + (uint)(128 * (1 << 10));

                    offcodeMax = ZSTD_highbit32(maxOffset);
                }

                bs->entropy.fse.offcode_repeatMode = ZSTD_dictNCountRepeat((short*)offcodeNCount, offcodeMaxValue, ((offcodeMax) < (31) ? (offcodeMax) : (31)));

                {
                    uint u;

                    for (u = 0; u < 3; u++)
                    {
                        if (bs->rep[u] == 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                        }

                        if (bs->rep[u] > dictContentSize)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                        }

                    }
                }
            }

            return (nuint)(dictPtr - (byte*)(dict));
        }

        /* Dictionary format :
         * See :
         * https://github.com/facebook/zstd/blob/release/doc/zstd_compression_format.md#dictionary-format
         */
        /*! ZSTD_loadZstdDictionary() :
         * @return : dictID, or an error code
         *  assumptions : magic number supposed already checked
         *                dictSize supposed >= 8
         */
        private static nuint ZSTD_loadZstdDictionary(ZSTD_compressedBlockState_t* bs, ZSTD_matchState_t* ms, ZSTD_cwksp* ws, ZSTD_CCtx_params_s* @params, void* dict, nuint dictSize, ZSTD_dictTableLoadMethod_e dtlm, void* workspace)
        {
            byte* dictPtr = (byte*)(dict);
            byte* dictEnd = dictPtr + dictSize;
            nuint dictID;
            nuint eSize;

            assert(dictSize >= 8);
            assert(MEM_readLE32((void*)dictPtr) == 0xEC30A437);
            dictID = (nuint)(@params->fParams.noDictIDFlag != 0 ? 0 : MEM_readLE32((void*)(dictPtr + 4)));
            eSize = ZSTD_loadCEntropy(bs, workspace, dict, dictSize);

            {
                nuint err_code = (eSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            dictPtr += eSize;

            {
                nuint dictContentSize = (nuint)(dictEnd - dictPtr);


                {
                    nuint err_code = (ZSTD_loadDictionaryContent(ms, (ldmState_t*)null, ws, @params, (void*)dictPtr, dictContentSize, dtlm));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

            }

            return dictID;
        }

        /** ZSTD_compress_insertDictionary() :
        *   @return : dictID, or an error code */
        private static nuint ZSTD_compress_insertDictionary(ZSTD_compressedBlockState_t* bs, ZSTD_matchState_t* ms, ldmState_t* ls, ZSTD_cwksp* ws, ZSTD_CCtx_params_s* @params, void* dict, nuint dictSize, ZSTD_dictContentType_e dictContentType, ZSTD_dictTableLoadMethod_e dtlm, void* workspace)
        {
            if ((dict == null) || (dictSize < 8))
            {
                if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_fullDict)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong)));
                }

                return 0;
            }

            ZSTD_reset_compressedBlockState(bs);
            if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_rawContent)
            {
                return ZSTD_loadDictionaryContent(ms, ls, ws, @params, dict, dictSize, dtlm);
            }

            if (MEM_readLE32(dict) != 0xEC30A437)
            {
                if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_auto)
                {
                    return ZSTD_loadDictionaryContent(ms, ls, ws, @params, dict, dictSize, dtlm);
                }

                if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_fullDict)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong)));
                }

                assert(0 != 0);
            }

            return ZSTD_loadZstdDictionary(bs, ms, ws, @params, dict, dictSize, dtlm, workspace);
        }

        /*! ZSTD_compressBegin_internal() :
         * @return : 0, or an error code */
        private static nuint ZSTD_compressBegin_internal(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize, ZSTD_dictContentType_e dictContentType, ZSTD_dictTableLoadMethod_e dtlm, ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize, ZSTD_buffered_policy_e zbuff)
        {
            nuint dictContentSize = cdict != null ? cdict->dictContentSize : dictSize;

            assert((ERR_isError(ZSTD_checkCParams(@params->cParams))) == 0);
            assert(!((dict) != null && (cdict) != null));
            if ((cdict) != null && (cdict->dictContentSize > 0) && (pledgedSrcSize < (uint)((128 * (1 << 10))) || pledgedSrcSize < cdict->dictContentSize * (6UL) || pledgedSrcSize == (unchecked(0UL - 1)) || cdict->compressionLevel == 0) && (@params->attachDictPref != ZSTD_dictAttachPref_e.ZSTD_dictForceLoad))
            {
                return ZSTD_resetCCtx_usingCDict(cctx, cdict, @params, pledgedSrcSize, zbuff);
            }


            {
                nuint err_code = (ZSTD_resetCCtx_internal(cctx, @params, pledgedSrcSize, dictContentSize, ZSTD_compResetPolicy_e.ZSTDcrp_makeClean, zbuff));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint dictID = cdict != null ? ZSTD_compress_insertDictionary(cctx->blockState.prevCBlock, &cctx->blockState.matchState, &cctx->ldmState, &cctx->workspace, &cctx->appliedParams, cdict->dictContent, cdict->dictContentSize, cdict->dictContentType, dtlm, (void*)cctx->entropyWorkspace) : ZSTD_compress_insertDictionary(cctx->blockState.prevCBlock, &cctx->blockState.matchState, &cctx->ldmState, &cctx->workspace, &cctx->appliedParams, dict, dictSize, dictContentType, dtlm, (void*)cctx->entropyWorkspace);


                {
                    nuint err_code = (dictID);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(dictID <= 0xffffffff);
                cctx->dictID = (uint)(dictID);
                cctx->dictContentSize = dictContentSize;
            }

            return 0;
        }

        /* ZSTD_compressBegin_advanced_internal() :
         * Private use only. To be called from zstdmt_compress.c. */
        public static nuint ZSTD_compressBegin_advanced_internal(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize, ZSTD_dictContentType_e dictContentType, ZSTD_dictTableLoadMethod_e dtlm, ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize)
        {

            {
                nuint err_code = (ZSTD_checkCParams(@params->cParams));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_compressBegin_internal(cctx, dict, dictSize, dictContentType, dtlm, cdict, @params, pledgedSrcSize, ZSTD_buffered_policy_e.ZSTDb_not_buffered);
        }

        /*! ZSTD_compressBegin_advanced() :
        *   @return : 0, or an error code */
        public static nuint ZSTD_compressBegin_advanced(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize, ZSTD_parameters @params, ulong pledgedSrcSize)
        {
            ZSTD_CCtx_params_s cctxParams;

            ZSTD_CCtxParams_init_internal(&cctxParams, &@params, 0);
            return ZSTD_compressBegin_advanced_internal(cctx, dict, dictSize, ZSTD_dictContentType_e.ZSTD_dct_auto, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast, (ZSTD_CDict_s*)null, &cctxParams, pledgedSrcSize);
        }

        public static nuint ZSTD_compressBegin_usingDict(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize, int compressionLevel)
        {
            ZSTD_CCtx_params_s cctxParams;


            {
                ZSTD_parameters @params = ZSTD_getParams_internal(compressionLevel, (unchecked(0UL - 1)), dictSize, ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict);

                ZSTD_CCtxParams_init_internal(&cctxParams, &@params, (compressionLevel == 0) ? 3 : compressionLevel);
            }

            return ZSTD_compressBegin_internal(cctx, dict, dictSize, ZSTD_dictContentType_e.ZSTD_dct_auto, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast, (ZSTD_CDict_s*)null, &cctxParams, (unchecked(0UL - 1)), ZSTD_buffered_policy_e.ZSTDb_not_buffered);
        }

        /*=====   Buffer-less streaming compression functions  =====*/
        public static nuint ZSTD_compressBegin(ZSTD_CCtx_s* cctx, int compressionLevel)
        {
            return ZSTD_compressBegin_usingDict(cctx, null, 0, compressionLevel);
        }

        /*! ZSTD_writeEpilogue() :
        *   Ends a frame.
        *   @return : nb of bytes written into dst (or an error code) */
        private static nuint ZSTD_writeEpilogue(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity)
        {
            byte* ostart = (byte*)(dst);
            byte* op = ostart;
            nuint fhSize = 0;

            if (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_created)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            if (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_init)
            {
                fhSize = ZSTD_writeFrameHeader(dst, dstCapacity, &cctx->appliedParams, 0, 0);

                {
                    nuint err_code = (fhSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                dstCapacity -= fhSize;
                op += fhSize;
                cctx->stage = ZSTD_compressionStage_e.ZSTDcs_ongoing;
            }

            if (cctx->stage != ZSTD_compressionStage_e.ZSTDcs_ending)
            {
                uint cBlockHeader24 = 1 + (((uint)(blockType_e.bt_raw)) << 1) + 0;

                if (dstCapacity < 4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                MEM_writeLE32((void*)op, cBlockHeader24);
                op += ZSTD_blockHeaderSize;
                dstCapacity -= ZSTD_blockHeaderSize;
            }

            if (cctx->appliedParams.fParams.checksumFlag != 0)
            {
                uint checksum = (uint)(XXH64_digest(&cctx->xxhState));

                if (dstCapacity < 4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                MEM_writeLE32((void*)op, checksum);
                op += 4;
            }

            cctx->stage = ZSTD_compressionStage_e.ZSTDcs_created;
            return (nuint)(op - ostart);
        }

        /** ZSTD_CCtx_trace() :
         *  Trace the end of a compression call.
         */
        public static void ZSTD_CCtx_trace(ZSTD_CCtx_s* cctx, nuint extraCSize)
        {
        }

        public static nuint ZSTD_compressEnd(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            nuint endResult;
            nuint cSize = ZSTD_compressContinue_internal(cctx, dst, dstCapacity, src, srcSize, 1, 1);


            {
                nuint err_code = (cSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            endResult = ZSTD_writeEpilogue(cctx, (void*)((sbyte*)(dst) + cSize), dstCapacity - cSize);

            {
                nuint err_code = (endResult);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            assert(!(cctx->appliedParams.fParams.contentSizeFlag != 0 && cctx->pledgedSrcSizePlusOne == 0));
            if (cctx->pledgedSrcSizePlusOne != 0)
            {
                if (cctx->pledgedSrcSizePlusOne != cctx->consumedSrcSize + 1)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                }

            }

            ZSTD_CCtx_trace(cctx, endResult);
            return cSize + endResult;
        }

        /*! ZSTD_compress_advanced() :
         *  Note : this function is now DEPRECATED.
         *         It can be replaced by ZSTD_compress2(), in combination with ZSTD_CCtx_setParameter() and other parameter setters.
         *  This prototype will generate compilation warnings. */
        public static nuint ZSTD_compress_advanced(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize, ZSTD_parameters @params)
        {

            {
                nuint err_code = (ZSTD_checkCParams(@params.cParams));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            ZSTD_CCtxParams_init_internal(&cctx->simpleApiParams, &@params, 0);
            return ZSTD_compress_advanced_internal(cctx, dst, dstCapacity, src, srcSize, dict, dictSize, &cctx->simpleApiParams);
        }

        /* Internal */
        public static nuint ZSTD_compress_advanced_internal(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize, ZSTD_CCtx_params_s* @params)
        {

            {
                nuint err_code = (ZSTD_compressBegin_internal(cctx, dict, dictSize, ZSTD_dictContentType_e.ZSTD_dct_auto, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast, (ZSTD_CDict_s*)null, @params, (ulong)srcSize, ZSTD_buffered_policy_e.ZSTDb_not_buffered));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_compressEnd(cctx, dst, dstCapacity, src, srcSize);
        }

        /**************************
        *  Simple dictionary API
        ***************************/
        /*! ZSTD_compress_usingDict() :
         *  Compression at an explicit compression level using a Dictionary.
         *  A dictionary can be any arbitrary data segment (also called a prefix),
         *  or a buffer with specified information (see zdict.h).
         *  Note : This function loads the dictionary, resulting in significant startup delay.
         *         It's intended for a dictionary used only once.
         *  Note 2 : When `dict == NULL || dictSize < 8` no dictionary is used. */
        public static nuint ZSTD_compress_usingDict(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize, int compressionLevel)
        {

            {
                ZSTD_parameters @params = ZSTD_getParams_internal(compressionLevel, (ulong)srcSize, dict != null ? dictSize : 0, ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict);

                assert(@params.fParams.contentSizeFlag == 1);
                ZSTD_CCtxParams_init_internal(&cctx->simpleApiParams, &@params, (compressionLevel == 0) ? 3 : compressionLevel);
            }

            return ZSTD_compress_advanced_internal(cctx, dst, dstCapacity, src, srcSize, dict, dictSize, &cctx->simpleApiParams);
        }

        /*! ZSTD_compressCCtx() :
         *  Same as ZSTD_compress(), using an explicit ZSTD_CCtx.
         *  Important : in order to behave similarly to `ZSTD_compress()`,
         *  this function compresses at requested compression level,
         *  __ignoring any other parameter__ .
         *  If any advanced parameter was set using the advanced API,
         *  they will all be reset. Only `compressionLevel` remains.
         */
        public static nuint ZSTD_compressCCtx(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, int compressionLevel)
        {
            assert(cctx != null);
            return ZSTD_compress_usingDict(cctx, dst, dstCapacity, src, srcSize, null, 0, compressionLevel);
        }

        /***************************************
        *  Simple API
        ***************************************/
        /*! ZSTD_compress() :
         *  Compresses `src` content as a single zstd compressed frame into already allocated `dst`.
         *  Hint : compression runs faster if `dstCapacity` >=  `ZSTD_compressBound(srcSize)`.
         *  @return : compressed size written into `dst` (<= `dstCapacity),
         *            or an error code if it fails (which can be tested using ZSTD_isError()). */
        public static nuint ZSTD_compress(void* dst, nuint dstCapacity, void* src, nuint srcSize, int compressionLevel)
        {
            nuint result;
            ZSTD_CCtx_s ctxBody;

            ZSTD_initCCtx(&ctxBody, ZSTD_defaultCMem);
            result = ZSTD_compressCCtx(&ctxBody, dst, dstCapacity, src, srcSize, compressionLevel);
            ZSTD_freeCCtxContent(&ctxBody);
            return result;
        }

        /*! ZSTD_estimateCDictSize_advanced() :
         *  Estimate amount of memory that will be needed to create a dictionary with following arguments */
        public static nuint ZSTD_estimateCDictSize_advanced(nuint dictSize, ZSTD_compressionParameters cParams, ZSTD_dictLoadMethod_e dictLoadMethod)
        {
            return ZSTD_cwksp_alloc_size((nuint)(sizeof(ZSTD_CDict_s))) + ZSTD_cwksp_alloc_size((nuint)(((6 << 10) + 256))) + ZSTD_sizeof_matchState(&cParams, ZSTD_resolveRowMatchFinderMode(ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto, &cParams), 1, 0) + (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef ? 0 : ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(dictSize, (nuint)(sizeof(void*)))));
        }

        /*! ZSTD_estimate?DictSize() :
         *  ZSTD_estimateCDictSize() will bet that src size is relatively "small", and content is copied, like ZSTD_createCDict().
         *  ZSTD_estimateCDictSize_advanced() makes it possible to control compression parameters precisely, like ZSTD_createCDict_advanced().
         *  Note : dictionaries created by reference (`ZSTD_dlm_byRef`) are logically smaller.
         */
        public static nuint ZSTD_estimateCDictSize(nuint dictSize, int compressionLevel)
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, (unchecked(0UL - 1)), dictSize, ZSTD_cParamMode_e.ZSTD_cpm_createCDict);

            return ZSTD_estimateCDictSize_advanced(dictSize, cParams, ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy);
        }

        public static nuint ZSTD_sizeof_CDict(ZSTD_CDict_s* cdict)
        {
            if (cdict == null)
            {
                return 0;
            }

            return (cdict->workspace.workspace == cdict ? 0 : (nuint)(sizeof(ZSTD_CDict_s))) + ZSTD_cwksp_sizeof(&cdict->workspace);
        }

        private static nuint ZSTD_initCDict_internal(ZSTD_CDict_s* cdict, void* dictBuffer, nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_dictContentType_e dictContentType, ZSTD_CCtx_params_s @params)
        {
            assert((ZSTD_checkCParams(@params.cParams)) == 0);
            cdict->matchState.cParams = @params.cParams;
            cdict->matchState.dedicatedDictSearch = @params.enableDedicatedDictSearch;
            if ((dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef) || dictBuffer == null || dictSize == 0)
            {
                cdict->dictContent = dictBuffer;
            }
            else
            {
                void* internalBuffer = ZSTD_cwksp_reserve_object(&cdict->workspace, ZSTD_cwksp_align(dictSize, (nuint)(sizeof(void*))));

                if (internalBuffer == null)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                }

                cdict->dictContent = internalBuffer;
                memcpy((internalBuffer), (dictBuffer), (dictSize));
            }

            cdict->dictContentSize = dictSize;
            cdict->dictContentType = dictContentType;
            cdict->entropyWorkspace = (uint*)(ZSTD_cwksp_reserve_object(&cdict->workspace, (nuint)(((6 << 10) + 256))));
            ZSTD_reset_compressedBlockState(&cdict->cBlockState);

            {
                nuint err_code = (ZSTD_reset_matchState(&cdict->matchState, &cdict->workspace, &@params.cParams, @params.useRowMatchFinder, ZSTD_compResetPolicy_e.ZSTDcrp_makeClean, ZSTD_indexResetPolicy_e.ZSTDirp_reset, ZSTD_resetTarget_e.ZSTD_resetTarget_CDict));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                @params.compressionLevel = 3;
                @params.fParams.contentSizeFlag = 1;

                {
                    nuint dictID = ZSTD_compress_insertDictionary(&cdict->cBlockState, &cdict->matchState, (ldmState_t*)null, &cdict->workspace, &@params, cdict->dictContent, cdict->dictContentSize, dictContentType, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_full, (void*)cdict->entropyWorkspace);


                    {
                        nuint err_code = (dictID);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    assert(dictID <= (nuint)(unchecked((uint)(-1))));
                    cdict->dictID = (uint)(dictID);
                }
            }

            return 0;
        }

        private static ZSTD_CDict_s* ZSTD_createCDict_advanced_internal(nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_compressionParameters cParams, ZSTD_useRowMatchFinderMode_e useRowMatchFinder, uint enableDedicatedDictSearch, ZSTD_customMem customMem)
        {
            if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            {
                return (ZSTD_CDict_s*)null;
            }


            {
                nuint workspaceSize = ZSTD_cwksp_alloc_size((nuint)(sizeof(ZSTD_CDict_s))) + ZSTD_cwksp_alloc_size((nuint)(((6 << 10) + 256))) + ZSTD_sizeof_matchState(&cParams, useRowMatchFinder, enableDedicatedDictSearch, 0) + (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef ? 0 : ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(dictSize, (nuint)(sizeof(void*)))));
                void* workspace = ZSTD_customMalloc(workspaceSize, customMem);
                ZSTD_cwksp ws;
                ZSTD_CDict_s* cdict;

                if (workspace == null)
                {
                    ZSTD_customFree(workspace, customMem);
                    return (ZSTD_CDict_s*)null;
                }

                ZSTD_cwksp_init(&ws, workspace, workspaceSize, ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_dynamic_alloc);
                cdict = (ZSTD_CDict_s*)(ZSTD_cwksp_reserve_object(&ws, (nuint)(sizeof(ZSTD_CDict_s))));
                assert(cdict != null);
                ZSTD_cwksp_move(&cdict->workspace, &ws);
                cdict->customMem = customMem;
                cdict->compressionLevel = 0;
                cdict->useRowMatchFinder = useRowMatchFinder;
                return cdict;
            }
        }

        public static ZSTD_CDict_s* ZSTD_createCDict_advanced(void* dictBuffer, nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_dictContentType_e dictContentType, ZSTD_compressionParameters cParams, ZSTD_customMem customMem)
        {
            ZSTD_CCtx_params_s cctxParams;

            memset((void*)(&cctxParams), (0), ((nuint)(sizeof(ZSTD_CCtx_params_s))));
            ZSTD_CCtxParams_init(&cctxParams, 0);
            cctxParams.cParams = cParams;
            cctxParams.customMem = customMem;
            return ZSTD_createCDict_advanced2(dictBuffer, dictSize, dictLoadMethod, dictContentType, &cctxParams, customMem);
        }

        /*
         * This API is temporary and is expected to change or disappear in the future!
         */
        public static ZSTD_CDict_s* ZSTD_createCDict_advanced2(void* dict, nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_dictContentType_e dictContentType, ZSTD_CCtx_params_s* originalCctxParams, ZSTD_customMem customMem)
        {
            ZSTD_CCtx_params_s cctxParams = *originalCctxParams;
            ZSTD_compressionParameters cParams;
            ZSTD_CDict_s* cdict;

            if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            {
                return (ZSTD_CDict_s*)null;
            }

            if (cctxParams.enableDedicatedDictSearch != 0)
            {
                cParams = ZSTD_dedicatedDictSearch_getCParams(cctxParams.compressionLevel, dictSize);
                ZSTD_overrideCParams(&cParams, &cctxParams.cParams);
            }
            else
            {
                cParams = ZSTD_getCParamsFromCCtxParams(&cctxParams, (unchecked(0UL - 1)), dictSize, ZSTD_cParamMode_e.ZSTD_cpm_createCDict);
            }

            if ((ZSTD_dedicatedDictSearch_isSupported(&cParams)) == 0)
            {
                cctxParams.enableDedicatedDictSearch = 0;
                cParams = ZSTD_getCParamsFromCCtxParams(&cctxParams, (unchecked(0UL - 1)), dictSize, ZSTD_cParamMode_e.ZSTD_cpm_createCDict);
            }

            cctxParams.cParams = cParams;
            cctxParams.useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(cctxParams.useRowMatchFinder, &cParams);
            cdict = ZSTD_createCDict_advanced_internal(dictSize, dictLoadMethod, cctxParams.cParams, cctxParams.useRowMatchFinder, (uint)cctxParams.enableDedicatedDictSearch, customMem);
            if ((ERR_isError(ZSTD_initCDict_internal(cdict, dict, dictSize, dictLoadMethod, dictContentType, cctxParams))) != 0)
            {
                ZSTD_freeCDict(cdict);
                return (ZSTD_CDict_s*)null;
            }

            return cdict;
        }

        /*! ZSTD_createCDict() :
         *  When compressing multiple messages or blocks using the same dictionary,
         *  it's recommended to digest the dictionary only once, since it's a costly operation.
         *  ZSTD_createCDict() will create a state from digesting a dictionary.
         *  The resulting state can be used for future compression operations with very limited startup cost.
         *  ZSTD_CDict can be created once and shared by multiple threads concurrently, since its usage is read-only.
         * @dictBuffer can be released after ZSTD_CDict creation, because its content is copied within CDict.
         *  Note 1 : Consider experimental function `ZSTD_createCDict_byReference()` if you prefer to not duplicate @dictBuffer content.
         *  Note 2 : A ZSTD_CDict can be created from an empty @dictBuffer,
         *      in which case the only thing that it transports is the @compressionLevel.
         *      This can be useful in a pipeline featuring ZSTD_compress_usingCDict() exclusively,
         *      expecting a ZSTD_CDict parameter with any data, including those without a known dictionary. */
        public static ZSTD_CDict_s* ZSTD_createCDict(void* dict, nuint dictSize, int compressionLevel)
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, (unchecked(0UL - 1)), dictSize, ZSTD_cParamMode_e.ZSTD_cpm_createCDict);
            ZSTD_CDict_s* cdict = ZSTD_createCDict_advanced(dict, dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy, ZSTD_dictContentType_e.ZSTD_dct_auto, cParams, ZSTD_defaultCMem);

            if (cdict != null)
            {
                cdict->compressionLevel = (compressionLevel == 0) ? 3 : compressionLevel;
            }

            return cdict;
        }

        /*! ZSTD_createCDict_byReference() :
         *  Create a digested dictionary for compression
         *  Dictionary content is just referenced, not duplicated.
         *  As a consequence, `dictBuffer` **must** outlive CDict,
         *  and its content must remain unmodified throughout the lifetime of CDict.
         *  note: equivalent to ZSTD_createCDict_advanced(), with dictLoadMethod==ZSTD_dlm_byRef */
        public static ZSTD_CDict_s* ZSTD_createCDict_byReference(void* dict, nuint dictSize, int compressionLevel)
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, (unchecked(0UL - 1)), dictSize, ZSTD_cParamMode_e.ZSTD_cpm_createCDict);
            ZSTD_CDict_s* cdict = ZSTD_createCDict_advanced(dict, dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef, ZSTD_dictContentType_e.ZSTD_dct_auto, cParams, ZSTD_defaultCMem);

            if (cdict != null)
            {
                cdict->compressionLevel = (compressionLevel == 0) ? 3 : compressionLevel;
            }

            return cdict;
        }

        /*! ZSTD_freeCDict() :
         *  Function frees memory allocated by ZSTD_createCDict().
         *  If a NULL pointer is passed, no operation is performed. */
        public static nuint ZSTD_freeCDict(ZSTD_CDict_s* cdict)
        {
            if (cdict == null)
            {
                return 0;
            }


            {
                ZSTD_customMem cMem = cdict->customMem;
                int cdictInWorkspace = ZSTD_cwksp_owns_buffer(&cdict->workspace, (void*)cdict);

                ZSTD_cwksp_free(&cdict->workspace, cMem);
                if (cdictInWorkspace == 0)
                {
                    ZSTD_customFree((void*)cdict, cMem);
                }

                return 0;
            }
        }

        /*! ZSTD_initStaticCDict_advanced() :
         *  Generate a digested dictionary in provided memory area.
         *  workspace: The memory area to emplace the dictionary into.
         *             Provided pointer must 8-bytes aligned.
         *             It must outlive dictionary usage.
         *  workspaceSize: Use ZSTD_estimateCDictSize()
         *                 to determine how large workspace must be.
         *  cParams : use ZSTD_getCParams() to transform a compression level
         *            into its relevants cParams.
         * @return : pointer to ZSTD_CDict*, or NULL if error (size too small)
         *  Note : there is no corresponding "free" function.
         *         Since workspace was allocated externally, it must be freed externally.
         */
        public static ZSTD_CDict_s* ZSTD_initStaticCDict(void* workspace, nuint workspaceSize, void* dict, nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_dictContentType_e dictContentType, ZSTD_compressionParameters cParams)
        {
            ZSTD_useRowMatchFinderMode_e useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(ZSTD_useRowMatchFinderMode_e.ZSTD_urm_auto, &cParams);
            nuint matchStateSize = ZSTD_sizeof_matchState(&cParams, useRowMatchFinder, 1, 0);
            nuint neededSize = ZSTD_cwksp_alloc_size((nuint)(sizeof(ZSTD_CDict_s))) + (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef ? 0 : ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(dictSize, (nuint)(sizeof(void*))))) + ZSTD_cwksp_alloc_size((nuint)(((6 << 10) + 256))) + matchStateSize;
            ZSTD_CDict_s* cdict;
            ZSTD_CCtx_params_s @params;

            if (((nuint)(workspace) & 7) != 0)
            {
                return (ZSTD_CDict_s*)null;
            }


            {
                ZSTD_cwksp ws;

                ZSTD_cwksp_init(&ws, workspace, workspaceSize, ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_static_alloc);
                cdict = (ZSTD_CDict_s*)(ZSTD_cwksp_reserve_object(&ws, (nuint)(sizeof(ZSTD_CDict_s))));
                if (cdict == null)
                {
                    return (ZSTD_CDict_s*)null;
                }

                ZSTD_cwksp_move(&cdict->workspace, &ws);
            }

            if (workspaceSize < neededSize)
            {
                return (ZSTD_CDict_s*)null;
            }

            ZSTD_CCtxParams_init(&@params, 0);
            @params.cParams = cParams;
            @params.useRowMatchFinder = useRowMatchFinder;
            cdict->useRowMatchFinder = useRowMatchFinder;
            if ((ERR_isError(ZSTD_initCDict_internal(cdict, dict, dictSize, dictLoadMethod, dictContentType, @params))) != 0)
            {
                return (ZSTD_CDict_s*)null;
            }

            return cdict;
        }

        /*! ZSTD_getCParamsFromCDict() :
         *  as the name implies */
        public static ZSTD_compressionParameters ZSTD_getCParamsFromCDict(ZSTD_CDict_s* cdict)
        {
            assert(cdict != null);
            return cdict->matchState.cParams;
        }

        /*! ZSTD_getDictID_fromCDict() :
         *  Provides the dictID of the dictionary loaded into `cdict`.
         *  If @return == 0, the dictionary is not conformant to Zstandard specification, or empty.
         *  Non-conformant dictionaries can still be loaded, but as content-only dictionaries. */
        public static uint ZSTD_getDictID_fromCDict(ZSTD_CDict_s* cdict)
        {
            if (cdict == null)
            {
                return 0;
            }

            return cdict->dictID;
        }

        /* ZSTD_compressBegin_usingCDict_internal() :
         * Implementation of various ZSTD_compressBegin_usingCDict* functions.
         */
        private static nuint ZSTD_compressBegin_usingCDict_internal(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict, ZSTD_frameParameters fParams, ulong pledgedSrcSize)
        {
            ZSTD_CCtx_params_s cctxParams;

            if (cdict == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong)));
            }


            {
                ZSTD_parameters @params;

                @params.fParams = fParams;
                @params.cParams = (pledgedSrcSize < (uint)((128 * (1 << 10))) || pledgedSrcSize < cdict->dictContentSize * (6UL) || pledgedSrcSize == (unchecked(0UL - 1)) || cdict->compressionLevel == 0) ? ZSTD_getCParamsFromCDict(cdict) : ZSTD_getCParams(cdict->compressionLevel, pledgedSrcSize, cdict->dictContentSize);
                ZSTD_CCtxParams_init_internal(&cctxParams, &@params, cdict->compressionLevel);
            }

            if (pledgedSrcSize != (unchecked(0UL - 1)))
            {
                uint limitedSrcSize = (uint)((pledgedSrcSize) < (1U << 19) ? (pledgedSrcSize) : (1U << 19));
                uint limitedSrcLog = limitedSrcSize > 1 ? ZSTD_highbit32(limitedSrcSize - 1) + 1 : 1;

                cctxParams.cParams.windowLog = ((cctxParams.cParams.windowLog) > (limitedSrcLog) ? (cctxParams.cParams.windowLog) : (limitedSrcLog));
            }

            return ZSTD_compressBegin_internal(cctx, null, 0, ZSTD_dictContentType_e.ZSTD_dct_auto, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast, cdict, &cctxParams, pledgedSrcSize, ZSTD_buffered_policy_e.ZSTDb_not_buffered);
        }

        /* ZSTD_compressBegin_usingCDict_advanced() :
         * This function is DEPRECATED.
         * cdict must be != NULL */
        public static nuint ZSTD_compressBegin_usingCDict_advanced(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict, ZSTD_frameParameters fParams, ulong pledgedSrcSize)
        {
            return ZSTD_compressBegin_usingCDict_internal(cctx, cdict, fParams, pledgedSrcSize);
        }

        /* ZSTD_compressBegin_usingCDict() :
         * cdict must be != NULL */
        public static nuint ZSTD_compressBegin_usingCDict(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict)
        {
            ZSTD_frameParameters fParams = new ZSTD_frameParameters
            {
                contentSizeFlag = 0,
                checksumFlag = 0,
                noDictIDFlag = 0,
            };

            return ZSTD_compressBegin_usingCDict_internal(cctx, cdict, fParams, (unchecked(0UL - 1)));
        }

        /*! ZSTD_compress_usingCDict_internal():
         * Implementation of various ZSTD_compress_usingCDict* functions.
         */
        private static nuint ZSTD_compress_usingCDict_internal(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, ZSTD_CDict_s* cdict, ZSTD_frameParameters fParams)
        {

            {
                nuint err_code = (ZSTD_compressBegin_usingCDict_internal(cctx, cdict, fParams, (ulong)srcSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_compressEnd(cctx, dst, dstCapacity, src, srcSize);
        }

        /*! ZSTD_compress_usingCDict_advanced():
         * This function is DEPRECATED.
         */
        public static nuint ZSTD_compress_usingCDict_advanced(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, ZSTD_CDict_s* cdict, ZSTD_frameParameters fParams)
        {
            return ZSTD_compress_usingCDict_internal(cctx, dst, dstCapacity, src, srcSize, cdict, fParams);
        }

        /*! ZSTD_compress_usingCDict() :
         *  Compression using a digested Dictionary.
         *  Faster startup than ZSTD_compress_usingDict(), recommended when same dictionary is used multiple times.
         *  Note that compression parameters are decided at CDict creation time
         *  while frame parameters are hardcoded */
        public static nuint ZSTD_compress_usingCDict(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, ZSTD_CDict_s* cdict)
        {
            ZSTD_frameParameters fParams = new ZSTD_frameParameters
            {
                contentSizeFlag = 1,
                checksumFlag = 0,
                noDictIDFlag = 0,
            };

            return ZSTD_compress_usingCDict_internal(cctx, dst, dstCapacity, src, srcSize, cdict, fParams);
        }

        /* ******************************************************************
        *  Streaming
        ********************************************************************/
        public static ZSTD_CCtx_s* ZSTD_createCStream()
        {
            return ZSTD_createCStream_advanced(ZSTD_defaultCMem);
        }

        public static ZSTD_CCtx_s* ZSTD_initStaticCStream(void* workspace, nuint workspaceSize)
        {
            return ZSTD_initStaticCCtx(workspace, workspaceSize);
        }

        public static ZSTD_CCtx_s* ZSTD_createCStream_advanced(ZSTD_customMem customMem)
        {
            return ZSTD_createCCtx_advanced(customMem);
        }

        public static nuint ZSTD_freeCStream(ZSTD_CCtx_s* zcs)
        {
            return ZSTD_freeCCtx(zcs);
        }

        /*======   Initialization   ======*/
        public static nuint ZSTD_CStreamInSize()
        {
            return (nuint)((1 << 17));
        }

        public static nuint ZSTD_CStreamOutSize()
        {
            return ZSTD_compressBound((nuint)((1 << 17))) + ZSTD_blockHeaderSize + 4;
        }

        private static ZSTD_cParamMode_e ZSTD_getCParamMode(ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize)
        {
            if (cdict != null && (ZSTD_shouldAttachDict(cdict, @params, pledgedSrcSize)) != 0)
            {
                return ZSTD_cParamMode_e.ZSTD_cpm_attachDict;
            }
            else
            {
                return ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict;
            }
        }

        /* ZSTD_resetCStream():
         * pledgedSrcSize == 0 means "unknown" */
        public static nuint ZSTD_resetCStream(ZSTD_CCtx_s* zcs, ulong pss)
        {
            ulong pledgedSrcSize = (pss == 0) ? (unchecked(0UL - 1)) : pss;


            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setPledgedSrcSize(zcs, unchecked(pledgedSrcSize)));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /*! ZSTD_initCStream_internal() :
         *  Note : for lib/compress only. Used by zstdmt_compress.c.
         *  Assumption 1 : params are valid
         *  Assumption 2 : either dict, or cdict, is defined, not both */
        public static nuint ZSTD_initCStream_internal(ZSTD_CCtx_s* zcs, void* dict, nuint dictSize, ZSTD_CDict_s* cdict, ZSTD_CCtx_params_s* @params, ulong pledgedSrcSize)
        {

            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            assert((ERR_isError(ZSTD_checkCParams(@params->cParams))) == 0);
            zcs->requestedParams = *@params;
            assert(!((dict) != null && (cdict) != null));
            if (dict != null)
            {

                {
                    nuint err_code = (ZSTD_CCtx_loadDictionary(zcs, dict, dictSize));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

            }
            else
            {

                {
                    nuint err_code = (ZSTD_CCtx_refCDict(zcs, cdict));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

            }

            return 0;
        }

        /* ZSTD_initCStream_usingCDict_advanced() :
         * same as ZSTD_initCStream_usingCDict(), with control over frame parameters */
        public static nuint ZSTD_initCStream_usingCDict_advanced(ZSTD_CCtx_s* zcs, ZSTD_CDict_s* cdict, ZSTD_frameParameters fParams, ulong pledgedSrcSize)
        {

            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            zcs->requestedParams.fParams = fParams;

            {
                nuint err_code = (ZSTD_CCtx_refCDict(zcs, cdict));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /* note : cdict must outlive compression session */
        public static nuint ZSTD_initCStream_usingCDict(ZSTD_CCtx_s* zcs, ZSTD_CDict_s* cdict)
        {

            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_refCDict(zcs, cdict));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /* ZSTD_initCStream_advanced() :
         * pledgedSrcSize must be exact.
         * if srcSize is not known at init time, use value ZSTD_CONTENTSIZE_UNKNOWN.
         * dict is loaded with default parameters ZSTD_dct_auto and ZSTD_dlm_byCopy. */
        public static nuint ZSTD_initCStream_advanced(ZSTD_CCtx_s* zcs, void* dict, nuint dictSize, ZSTD_parameters @params, ulong pss)
        {
            ulong pledgedSrcSize = (pss == 0 && @params.fParams.contentSizeFlag == 0) ? (unchecked(0UL - 1)) : pss;


            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setPledgedSrcSize(zcs, unchecked(pledgedSrcSize)));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_checkCParams(@params.cParams));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            ZSTD_CCtxParams_setZstdParams(&zcs->requestedParams, &@params);

            {
                nuint err_code = (ZSTD_CCtx_loadDictionary(zcs, dict, dictSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /*! ZSTD_initCStream_usingDict() :
         * This function is DEPRECATED, and is equivalent to:
         *     ZSTD_CCtx_reset(zcs, ZSTD_reset_session_only);
         *     ZSTD_CCtx_setParameter(zcs, ZSTD_c_compressionLevel, compressionLevel);
         *     ZSTD_CCtx_loadDictionary(zcs, dict, dictSize);
         *
         * Creates of an internal CDict (incompatible with static CCtx), except if
         * dict == NULL or dictSize < 8, in which case no dict is used.
         * Note: dict is loaded with ZSTD_dct_auto (treated as a full zstd dictionary if
         * it begins with ZSTD_MAGIC_DICTIONARY, else as raw content) and ZSTD_dlm_byCopy.
         * This prototype will generate compilation warnings.
         */
        public static nuint ZSTD_initCStream_usingDict(ZSTD_CCtx_s* zcs, void* dict, nuint dictSize, int compressionLevel)
        {

            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setParameter(zcs, ZSTD_cParameter.ZSTD_c_compressionLevel, compressionLevel));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_loadDictionary(zcs, dict, dictSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /*! ZSTD_initCStream_srcSize() :
         * This function is DEPRECATED, and equivalent to:
         *     ZSTD_CCtx_reset(zcs, ZSTD_reset_session_only);
         *     ZSTD_CCtx_refCDict(zcs, NULL); // clear the dictionary (if any)
         *     ZSTD_CCtx_setParameter(zcs, ZSTD_c_compressionLevel, compressionLevel);
         *     ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize);
         *
         * pledgedSrcSize must be correct. If it is not known at init time, use
         * ZSTD_CONTENTSIZE_UNKNOWN. Note that, for compatibility with older programs,
         * "0" also disables frame content size field. It may be enabled in the future.
         * This prototype will generate compilation warnings.
         */
        public static nuint ZSTD_initCStream_srcSize(ZSTD_CCtx_s* zcs, int compressionLevel, ulong pss)
        {
            ulong pledgedSrcSize = (pss == 0) ? (unchecked(0UL - 1)) : pss;


            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_refCDict(zcs, (ZSTD_CDict_s*)null));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setParameter(zcs, ZSTD_cParameter.ZSTD_c_compressionLevel, compressionLevel));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setPledgedSrcSize(zcs, unchecked(pledgedSrcSize)));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /*!
         * Equivalent to:
         *
         *     ZSTD_CCtx_reset(zcs, ZSTD_reset_session_only);
         *     ZSTD_CCtx_refCDict(zcs, NULL); // clear the dictionary (if any)
         *     ZSTD_CCtx_setParameter(zcs, ZSTD_c_compressionLevel, compressionLevel);
         */
        public static nuint ZSTD_initCStream(ZSTD_CCtx_s* zcs, int compressionLevel)
        {

            {
                nuint err_code = (ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_refCDict(zcs, (ZSTD_CDict_s*)null));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_CCtx_setParameter(zcs, ZSTD_cParameter.ZSTD_c_compressionLevel, compressionLevel));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /*======   Compression   ======*/
        private static nuint ZSTD_nextInputSizeHint(ZSTD_CCtx_s* cctx)
        {
            nuint hintInSize = cctx->inBuffTarget - cctx->inBuffPos;

            if (hintInSize == 0)
            {
                hintInSize = cctx->blockSize;
            }

            return hintInSize;
        }

        /** ZSTD_compressStream_generic():
         *  internal function for all *compressStream*() variants
         *  non-static, because can be called from zstdmt_compress.c
         * @return : hint size for next input */
        private static nuint ZSTD_compressStream_generic(ZSTD_CCtx_s* zcs, ZSTD_outBuffer_s* output, ZSTD_inBuffer_s* input, ZSTD_EndDirective flushMode)
        {
            sbyte* istart = (sbyte*)(input->src);
            sbyte* iend = input->size != 0 ? istart + input->size : istart;
            sbyte* ip = input->pos != 0 ? istart + input->pos : istart;
            sbyte* ostart = (sbyte*)(output->dst);
            sbyte* oend = output->size != 0 ? ostart + output->size : ostart;
            sbyte* op = output->pos != 0 ? ostart + output->pos : ostart;
            uint someMoreWork = 1;

            if (zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
            {
                assert(zcs->inBuff != null);
                assert(zcs->inBuffSize > 0);
            }

            if (zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
            {
                assert(zcs->outBuff != null);
                assert(zcs->outBuffSize > 0);
            }

            assert(output->pos <= output->size);
            assert(input->pos <= input->size);
            assert((uint)(flushMode) <= (uint)(ZSTD_EndDirective.ZSTD_e_end));
            while (someMoreWork != 0)
            {
                switch (zcs->streamStage)
                {
                    case ZSTD_cStreamStage.zcss_init:
                    {

                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_init_missing)));
                        }
                    }

                    case ZSTD_cStreamStage.zcss_load:
                    {
                        if ((flushMode == ZSTD_EndDirective.ZSTD_e_end) && ((nuint)(oend - op) >= ZSTD_compressBound((nuint)(iend - ip)) || zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable) && (zcs->inBuffPos == 0))
                        {
                            nuint cSize = ZSTD_compressEnd(zcs, (void*)op, (nuint)(oend - op), (void*)ip, (nuint)(iend - ip));


                            {
                                nuint err_code = (cSize);

                                if ((ERR_isError(err_code)) != 0)
                                {
                                    return err_code;
                                }
                            }

                            ip = iend;
                            op += cSize;
                            zcs->frameEnded = 1;
                            ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
                            someMoreWork = 0;
                            break;
                        }
                    }

                    if (zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
                    {
                        nuint toLoad = zcs->inBuffTarget - zcs->inBuffPos;
                        nuint loaded = ZSTD_limitCopy((void*)(zcs->inBuff + zcs->inBuffPos), toLoad, (void*)ip, (nuint)(iend - ip));

                        zcs->inBuffPos += loaded;
                        if (loaded != 0)
                        {
                            ip += loaded;
                        }

                        if ((flushMode == ZSTD_EndDirective.ZSTD_e_continue) && (zcs->inBuffPos < zcs->inBuffTarget))
                        {
                            someMoreWork = 0;
                            break;
                        }

                        if ((flushMode == ZSTD_EndDirective.ZSTD_e_flush) && (zcs->inBuffPos == zcs->inToCompress))
                        {
                            someMoreWork = 0;
                            break;
                        }
                    }


                    {
                        int inputBuffered = ((zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered) ? 1 : 0);
                        void* cDst;
                        nuint cSize;
                        nuint oSize = (nuint)(oend - op);
                        nuint iSize = inputBuffered != 0 ? zcs->inBuffPos - zcs->inToCompress : (((nuint)(iend - ip)) < (zcs->blockSize) ? ((nuint)(iend - ip)) : (zcs->blockSize));

                        if (oSize >= ZSTD_compressBound(iSize) || zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
                        {
                            cDst = op;
                        }
                        else
                        {
                            cDst = zcs->outBuff; oSize = zcs->outBuffSize;
                        }

                        if (inputBuffered != 0)
                        {
                            uint lastBlock = (((flushMode == ZSTD_EndDirective.ZSTD_e_end) && (ip == iend)) ? 1U : 0U);

                            cSize = lastBlock != 0 ? ZSTD_compressEnd(zcs, cDst, oSize, (void*)(zcs->inBuff + zcs->inToCompress), iSize) : ZSTD_compressContinue(zcs, cDst, oSize, (void*)(zcs->inBuff + zcs->inToCompress), iSize);

                            {
                                nuint err_code = (cSize);

                                if ((ERR_isError(err_code)) != 0)
                                {
                                    return err_code;
                                }
                            }

                            zcs->frameEnded = lastBlock;
                            zcs->inBuffTarget = zcs->inBuffPos + zcs->blockSize;
                            if (zcs->inBuffTarget > zcs->inBuffSize)
                            {
                                zcs->inBuffPos = 0; zcs->inBuffTarget = zcs->blockSize;
                            }

                            if (lastBlock == 0)
                            {
                                assert(zcs->inBuffTarget <= zcs->inBuffSize);
                            }

                            zcs->inToCompress = zcs->inBuffPos;
                        }
                        else
                        {
                            uint lastBlock = (((ip + iSize == iend)) ? 1U : 0U);

                            assert(flushMode == ZSTD_EndDirective.ZSTD_e_end);
                            cSize = lastBlock != 0 ? ZSTD_compressEnd(zcs, cDst, oSize, (void*)ip, iSize) : ZSTD_compressContinue(zcs, cDst, oSize, (void*)ip, iSize);
                            if (iSize > 0)
                            {
                                ip += iSize;
                            }


                            {
                                nuint err_code = (cSize);

                                if ((ERR_isError(err_code)) != 0)
                                {
                                    return err_code;
                                }
                            }

                            zcs->frameEnded = lastBlock;
                            if (lastBlock != 0)
                            {
                                assert(ip == iend);
                            }
                        }

                        if (cDst == op)
                        {
                            op += cSize;
                            if (zcs->frameEnded != 0)
                            {
                                someMoreWork = 0;
                                ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
                            }

                            break;
                        }

                        zcs->outBuffContentSize = cSize;
                        zcs->outBuffFlushedSize = 0;
                        zcs->streamStage = ZSTD_cStreamStage.zcss_flush;
                    }


                    goto case ZSTD_cStreamStage.zcss_flush;
                    case ZSTD_cStreamStage.zcss_flush:
        ;
                    assert(zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered);

                    {
                        nuint toFlush = zcs->outBuffContentSize - zcs->outBuffFlushedSize;
                        nuint flushed = ZSTD_limitCopy((void*)op, (nuint)(oend - op), (void*)(zcs->outBuff + zcs->outBuffFlushedSize), toFlush);

                        if (flushed != 0)
                        {
                            op += flushed;
                        }

                        zcs->outBuffFlushedSize += flushed;
                        if (toFlush != flushed)
                        {
                            assert(op == oend);
                            someMoreWork = 0;
                            break;
                        }

                        zcs->outBuffContentSize = zcs->outBuffFlushedSize = 0;
                        if (zcs->frameEnded != 0)
                        {
                            someMoreWork = 0;
                            ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
                            break;
                        }

                        zcs->streamStage = ZSTD_cStreamStage.zcss_load;
                        break;
                    }

                    default:
                    {
                        assert(0 != 0);
                    }
                    break;
                }
            }

            input->pos = (nuint)(ip - istart);
            output->pos = (nuint)(op - ostart);
            if (zcs->frameEnded != 0)
            {
                return 0;
            }

            return ZSTD_nextInputSizeHint(zcs);
        }

        private static nuint ZSTD_nextInputSizeHint_MTorST(ZSTD_CCtx_s* cctx)
        {
            return ZSTD_nextInputSizeHint(cctx);
        }

        /*!
         * Alternative for ZSTD_compressStream2(zcs, output, input, ZSTD_e_continue).
         * NOTE: The return value is different. ZSTD_compressStream() returns a hint for
         * the next read size (if non-zero and not an error). ZSTD_compressStream2()
         * returns the minimum nb of bytes left to flush (if non-zero and not an error).
         */
        public static nuint ZSTD_compressStream(ZSTD_CCtx_s* zcs, ZSTD_outBuffer_s* output, ZSTD_inBuffer_s* input)
        {

            {
                nuint err_code = (ZSTD_compressStream2(zcs, output, input, ZSTD_EndDirective.ZSTD_e_continue));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_nextInputSizeHint_MTorST(zcs);
        }

        /* After a compression call set the expected input/output buffer.
         * This is validated at the start of the next compression call.
         */
        private static void ZSTD_setBufferExpectations(ZSTD_CCtx_s* cctx, ZSTD_outBuffer_s* output, ZSTD_inBuffer_s* input)
        {
            if (cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
            {
                cctx->expectedInBuffer = *input;
            }

            if (cctx->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
            {
                cctx->expectedOutBufferSize = output->size - output->pos;
            }
        }

        /* Validate that the input/output buffers match the expectations set by
         * ZSTD_setBufferExpectations.
         */
        private static nuint ZSTD_checkBufferStability(ZSTD_CCtx_s* cctx, ZSTD_outBuffer_s* output, ZSTD_inBuffer_s* input, ZSTD_EndDirective endOp)
        {
            if (cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
            {
                ZSTD_inBuffer_s expect = cctx->expectedInBuffer;

                if (expect.src != input->src || expect.pos != input->pos || expect.size != input->size)
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcBuffer_wrong)));
                    }
                }

                if (endOp != ZSTD_EndDirective.ZSTD_e_end)
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcBuffer_wrong)));
                    }
                }

            }

            if (cctx->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
            {
                nuint outBufferSize = output->size - output->pos;

                if (cctx->expectedOutBufferSize != outBufferSize)
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstBuffer_wrong)));
                    }
                }

            }

            return 0;
        }

        private static nuint ZSTD_CCtx_init_compressStream2(ZSTD_CCtx_s* cctx, ZSTD_EndDirective endOp, nuint inSize)
        {
            ZSTD_CCtx_params_s @params = cctx->requestedParams;
            ZSTD_prefixDict_s prefixDict = cctx->prefixDict;


            {
                nuint err_code = (ZSTD_initLocalDict(cctx));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            memset((void*)(&cctx->prefixDict), (0), ((nuint)(sizeof(ZSTD_prefixDict_s))));
            assert(prefixDict.dict == null || cctx->cdict == null);
            if (cctx->cdict != null && cctx->localDict.cdict == null)
            {
                @params.compressionLevel = cctx->cdict->compressionLevel;
            }

            if (endOp == ZSTD_EndDirective.ZSTD_e_end)
            {
                cctx->pledgedSrcSizePlusOne = (ulong)(inSize + 1);
            }


            {
                nuint dictSize = prefixDict.dict != null ? prefixDict.dictSize : (cctx->cdict != null ? cctx->cdict->dictContentSize : 0);
                ZSTD_cParamMode_e mode = ZSTD_getCParamMode(cctx->cdict, &@params, cctx->pledgedSrcSizePlusOne - 1);

                @params.cParams = ZSTD_getCParamsFromCCtxParams(&@params, cctx->pledgedSrcSizePlusOne - 1, dictSize, mode);
            }

            if ((ZSTD_CParams_shouldEnableLdm(&@params.cParams)) != 0)
            {
                @params.ldmParams.enableLdm = 1;
            }

            if ((ZSTD_CParams_useBlockSplitter(&@params.cParams)) != 0)
            {
                @params.splitBlocks = 1;
            }

            @params.useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(@params.useRowMatchFinder, &@params.cParams);

            {
                ulong pledgedSrcSize = cctx->pledgedSrcSizePlusOne - 1;

                assert((ERR_isError(ZSTD_checkCParams(@params.cParams))) == 0);

                {
                    nuint err_code = (ZSTD_compressBegin_internal(cctx, prefixDict.dict, prefixDict.dictSize, prefixDict.dictContentType, ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast, cctx->cdict, &@params, pledgedSrcSize, ZSTD_buffered_policy_e.ZSTDb_buffered));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(cctx->appliedParams.nbWorkers == 0);
                cctx->inToCompress = 0;
                cctx->inBuffPos = 0;
                if (cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
                {
                    cctx->inBuffTarget = cctx->blockSize + (uint)((((cctx->blockSize == pledgedSrcSize)) ? 1 : 0));
                }
                else
                {
                    cctx->inBuffTarget = 0;
                }

                cctx->outBuffContentSize = cctx->outBuffFlushedSize = 0;
                cctx->streamStage = ZSTD_cStreamStage.zcss_load;
                cctx->frameEnded = 0;
            }

            return 0;
        }

        /*! ZSTD_compressStream2() : Requires v1.4.0+
         *  Behaves about the same as ZSTD_compressStream, with additional control on end directive.
         *  - Compression parameters are pushed into CCtx before starting compression, using ZSTD_CCtx_set*()
         *  - Compression parameters cannot be changed once compression is started (save a list of exceptions in multi-threading mode)
         *  - output->pos must be <= dstCapacity, input->pos must be <= srcSize
         *  - output->pos and input->pos will be updated. They are guaranteed to remain below their respective limit.
         *  - endOp must be a valid directive
         *  - When nbWorkers==0 (default), function is blocking : it completes its job before returning to caller.
         *  - When nbWorkers>=1, function is non-blocking : it copies a portion of input, distributes jobs to internal worker threads, flush to output whatever is available,
         *                                                  and then immediately returns, just indicating that there is some data remaining to be flushed.
         *                                                  The function nonetheless guarantees forward progress : it will return only after it reads or write at least 1+ byte.
         *  - Exception : if the first call requests a ZSTD_e_end directive and provides enough dstCapacity, the function delegates to ZSTD_compress2() which is always blocking.
         *  - @return provides a minimum amount of data remaining to be flushed from internal buffers
         *            or an error code, which can be tested using ZSTD_isError().
         *            if @return != 0, flush is not fully completed, there is still some data left within internal buffers.
         *            This is useful for ZSTD_e_flush, since in this case more flushes are necessary to empty all buffers.
         *            For ZSTD_e_end, @return == 0 when internal buffers are fully flushed and frame is completed.
         *  - after a ZSTD_e_end directive, if internal buffer is not fully flushed (@return != 0),
         *            only ZSTD_e_end or ZSTD_e_flush operations are allowed.
         *            Before starting a new compression job, or changing compression parameters,
         *            it is required to fully flush internal buffers.
         */
        public static nuint ZSTD_compressStream2(ZSTD_CCtx_s* cctx, ZSTD_outBuffer_s* output, ZSTD_inBuffer_s* input, ZSTD_EndDirective endOp)
        {
            if (output->pos > output->size)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (input->pos > input->size)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if ((uint)(endOp) > (uint)(ZSTD_EndDirective.ZSTD_e_end))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
            }

            assert(cctx != null);
            if (cctx->streamStage == ZSTD_cStreamStage.zcss_init)
            {

                {
                    nuint err_code = (ZSTD_CCtx_init_compressStream2(cctx, endOp, input->size));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                ZSTD_setBufferExpectations(cctx, output, input);
            }


            {
                nuint err_code = (ZSTD_checkBufferStability(cctx, output, input, endOp));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_compressStream_generic(cctx, output, input, endOp));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            ZSTD_setBufferExpectations(cctx, output, input);
            return cctx->outBuffContentSize - cctx->outBuffFlushedSize;
        }

        /*! ZSTD_compressStream2_simpleArgs() :
         *  Same as ZSTD_compressStream2(),
         *  but using only integral types as arguments.
         *  This variant might be helpful for binders from dynamic languages
         *  which have troubles handling structures containing memory pointers.
         */
        public static nuint ZSTD_compressStream2_simpleArgs(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, nuint* dstPos, void* src, nuint srcSize, nuint* srcPos, ZSTD_EndDirective endOp)
        {
            ZSTD_outBuffer_s output = new ZSTD_outBuffer_s
            {
                dst = dst,
                size = dstCapacity,
                pos = *dstPos,
            };
            ZSTD_inBuffer_s input = new ZSTD_inBuffer_s
            {
                src = src,
                size = srcSize,
                pos = *srcPos,
            };
            nuint cErr = ZSTD_compressStream2(cctx, &output, &input, endOp);

            *dstPos = output.pos;
            *srcPos = input.pos;
            return cErr;
        }

        /*! ZSTD_compress2() :
         *  Behave the same as ZSTD_compressCCtx(), but compression parameters are set using the advanced API.
         *  ZSTD_compress2() always starts a new frame.
         *  Should cctx hold data from a previously unfinished frame, everything about it is forgotten.
         *  - Compression parameters are pushed into CCtx before starting compression, using ZSTD_CCtx_set*()
         *  - The function is always blocking, returns when compression is completed.
         *  Hint : compression runs faster if `dstCapacity` >=  `ZSTD_compressBound(srcSize)`.
         * @return : compressed size written into `dst` (<= `dstCapacity),
         *           or an error code if it fails (which can be tested using ZSTD_isError()).
         */
        public static nuint ZSTD_compress2(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            ZSTD_bufferMode_e originalInBufferMode = cctx->requestedParams.inBufferMode;
            ZSTD_bufferMode_e originalOutBufferMode = cctx->requestedParams.outBufferMode;

            ZSTD_CCtx_reset(cctx, ZSTD_ResetDirective.ZSTD_reset_session_only);
            cctx->requestedParams.inBufferMode = ZSTD_bufferMode_e.ZSTD_bm_stable;
            cctx->requestedParams.outBufferMode = ZSTD_bufferMode_e.ZSTD_bm_stable;

            {
                nuint oPos = 0;
                nuint iPos = 0;
                nuint result = ZSTD_compressStream2_simpleArgs(cctx, dst, dstCapacity, &oPos, src, srcSize, &iPos, ZSTD_EndDirective.ZSTD_e_end);

                cctx->requestedParams.inBufferMode = originalInBufferMode;
                cctx->requestedParams.outBufferMode = originalOutBufferMode;

                {
                    nuint err_code = (result);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (result != 0)
                {
                    assert(oPos == dstCapacity);

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                    }

                }

                assert(iPos == srcSize);
                return oPos;
            }
        }

        /* Returns a ZSTD error code if sequence is not valid */
        private static nuint ZSTD_validateSequence(uint offCode, uint matchLength, nuint posInSrc, uint windowLog, nuint dictSize, uint minMatch)
        {
            nuint offsetBound;
            uint windowSize = (uint)(1 << (int)windowLog);

            offsetBound = posInSrc > windowSize ? (nuint)(windowSize) : posInSrc + (nuint)(dictSize);
            if (offCode > offsetBound + (uint)((3 - 1)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            if (matchLength < minMatch)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            return 0;
        }

        /* Returns an offset code, given a sequence's raw offset, the ongoing repcode array, and whether litLength == 0 */
        private static uint ZSTD_finalizeOffCode(uint rawOffset, uint* rep, uint ll0)
        {
            uint offCode = rawOffset + (uint)((3 - 1));
            uint repCode = 0;

            if (ll0 == 0 && rawOffset == rep[0])
            {
                repCode = 1;
            }
            else if (rawOffset == rep[1])
            {
                repCode = 2 - ll0;
            }
            else if (rawOffset == rep[2])
            {
                repCode = 3 - ll0;
            }
            else if (ll0 != 0 && rawOffset == rep[0] - 1)
            {
                repCode = 3;
            }

            if (repCode != 0)
            {
                offCode = repCode - 1;
            }

            return offCode;
        }

        /* Returns 0 on success, and a ZSTD_error otherwise. This function scans through an array of
         * ZSTD_Sequence, storing the sequences it finds, until it reaches a block delimiter.
         */
        private static nuint ZSTD_copySequencesToSeqStoreExplicitBlockDelim(ZSTD_CCtx_s* cctx, ZSTD_sequencePosition* seqPos, ZSTD_Sequence* inSeqs, nuint inSeqsSize, void* src, nuint blockSize)
        {
            uint idx = seqPos->idx;
            byte* ip = (byte*)(src);
            byte* iend = ip + blockSize;
            repcodes_s updatedRepcodes;
            uint dictSize;
            uint litLength;
            uint matchLength;
            uint ll0;
            uint offCode;

            if (cctx->cdict != null)
            {
                dictSize = (uint)(cctx->cdict->dictContentSize);
            }
            else if (cctx->prefixDict.dict != null)
            {
                dictSize = (uint)(cctx->prefixDict.dictSize);
            }
            else
            {
                dictSize = 0;
            }

            memcpy((void*)(updatedRepcodes.rep), (void*)(cctx->blockState.prevCBlock->rep), ((nuint)(sizeof(repcodes_s))));
            for (; (inSeqs[idx].matchLength != 0 || inSeqs[idx].offset != 0) && idx < inSeqsSize; ++idx)
            {
                litLength = inSeqs[idx].litLength;
                matchLength = inSeqs[idx].matchLength;
                ll0 = ((litLength == 0) ? 1U : 0U);
                offCode = ZSTD_finalizeOffCode(inSeqs[idx].offset, updatedRepcodes.rep, ll0);
                updatedRepcodes = ZSTD_updateRep(updatedRepcodes.rep, offCode, ll0);
                if (cctx->appliedParams.validateSequences != 0)
                {
                    seqPos->posInSrc += litLength + matchLength;

                    {
                        nuint err_code = (ZSTD_validateSequence(offCode, matchLength, seqPos->posInSrc, cctx->appliedParams.cParams.windowLog, dictSize, cctx->appliedParams.cParams.minMatch));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }

                if (idx - seqPos->idx > cctx->seqStore.maxNbSeq)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                }

                ZSTD_storeSeq(&cctx->seqStore, litLength, ip, iend, offCode, matchLength - 3);
                ip += matchLength + litLength;
            }

            memcpy((void*)(cctx->blockState.nextCBlock->rep), (void*)(updatedRepcodes.rep), ((nuint)(sizeof(repcodes_s))));
            if ((inSeqs[idx].litLength) != 0)
            {
                ZSTD_storeLastLiterals(&cctx->seqStore, ip, inSeqs[idx].litLength);
                ip += inSeqs[idx].litLength;
                seqPos->posInSrc += inSeqs[idx].litLength;
            }

            if (ip != iend)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            seqPos->idx = idx + 1;
            return 0;
        }

        /* Returns the number of bytes to move the current read position back by. Only non-zero
         * if we ended up splitting a sequence. Otherwise, it may return a ZSTD error if something
         * went wrong.
         *
         * This function will attempt to scan through blockSize bytes represented by the sequences
         * in inSeqs, storing any (partial) sequences.
         *
         * Occasionally, we may want to change the actual number of bytes we consumed from inSeqs to
         * avoid splitting a match, or to avoid splitting a match such that it would produce a match
         * smaller than MINMATCH. In this case, we return the number of bytes that we didn't read from this block.
         */
        private static nuint ZSTD_copySequencesToSeqStoreNoBlockDelim(ZSTD_CCtx_s* cctx, ZSTD_sequencePosition* seqPos, ZSTD_Sequence* inSeqs, nuint inSeqsSize, void* src, nuint blockSize)
        {
            uint idx = seqPos->idx;
            uint startPosInSequence = seqPos->posInSequence;
            uint endPosInSequence = seqPos->posInSequence + (uint)(blockSize);
            nuint dictSize;
            byte* ip = (byte*)(src);
            byte* iend = ip + blockSize;
            repcodes_s updatedRepcodes;
            uint bytesAdjustment = 0;
            uint finalMatchSplit = 0;
            uint litLength;
            uint matchLength;
            uint rawOffset;
            uint offCode;

            if (cctx->cdict != null)
            {
                dictSize = cctx->cdict->dictContentSize;
            }
            else if (cctx->prefixDict.dict != null)
            {
                dictSize = cctx->prefixDict.dictSize;
            }
            else
            {
                dictSize = 0;
            }

            memcpy((void*)(updatedRepcodes.rep), (void*)(cctx->blockState.prevCBlock->rep), ((nuint)(sizeof(repcodes_s))));
            while (endPosInSequence != 0 && idx < inSeqsSize && finalMatchSplit == 0)
            {
                ZSTD_Sequence currSeq = inSeqs[idx];

                litLength = currSeq.litLength;
                matchLength = currSeq.matchLength;
                rawOffset = currSeq.offset;
                if (endPosInSequence >= currSeq.litLength + currSeq.matchLength)
                {
                    if (startPosInSequence >= litLength)
                    {
                        startPosInSequence -= litLength;
                        litLength = 0;
                        matchLength -= startPosInSequence;
                    }
                    else
                    {
                        litLength -= startPosInSequence;
                    }

                    endPosInSequence -= currSeq.litLength + currSeq.matchLength;
                    startPosInSequence = 0;
                    idx++;
                }
                else
                {
                    if (endPosInSequence > litLength)
                    {
                        uint firstHalfMatchLength;

                        litLength = (uint)(startPosInSequence >= litLength ? 0 : litLength - startPosInSequence);
                        firstHalfMatchLength = endPosInSequence - startPosInSequence - litLength;
                        if (matchLength > blockSize && firstHalfMatchLength >= cctx->appliedParams.cParams.minMatch)
                        {
                            uint secondHalfMatchLength = currSeq.matchLength + currSeq.litLength - endPosInSequence;

                            if (secondHalfMatchLength < cctx->appliedParams.cParams.minMatch)
                            {
                                endPosInSequence -= cctx->appliedParams.cParams.minMatch - secondHalfMatchLength;
                                bytesAdjustment = cctx->appliedParams.cParams.minMatch - secondHalfMatchLength;
                                firstHalfMatchLength -= bytesAdjustment;
                            }

                            matchLength = firstHalfMatchLength;
                            finalMatchSplit = 1;
                        }
                        else
                        {
                            bytesAdjustment = endPosInSequence - currSeq.litLength;
                            endPosInSequence = currSeq.litLength;
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }


                {
                    uint ll0 = (((litLength == 0)) ? 1U : 0U);

                    offCode = ZSTD_finalizeOffCode(rawOffset, updatedRepcodes.rep, ll0);
                    updatedRepcodes = ZSTD_updateRep(updatedRepcodes.rep, offCode, ll0);
                }

                if (cctx->appliedParams.validateSequences != 0)
                {
                    seqPos->posInSrc += litLength + matchLength;

                    {
                        nuint err_code = (ZSTD_validateSequence(offCode, matchLength, seqPos->posInSrc, cctx->appliedParams.cParams.windowLog, dictSize, cctx->appliedParams.cParams.minMatch));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }

                if (idx - seqPos->idx > cctx->seqStore.maxNbSeq)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                }

                ZSTD_storeSeq(&cctx->seqStore, litLength, ip, iend, offCode, matchLength - 3);
                ip += matchLength + litLength;
            }

            assert(idx == inSeqsSize || endPosInSequence <= inSeqs[idx].litLength + inSeqs[idx].matchLength);
            seqPos->idx = idx;
            seqPos->posInSequence = endPosInSequence;
            memcpy((void*)(cctx->blockState.nextCBlock->rep), (void*)(updatedRepcodes.rep), ((nuint)(sizeof(repcodes_s))));
            iend -= bytesAdjustment;
            if (ip != iend)
            {
                uint lastLLSize = (uint)(iend - ip);

                assert(ip <= iend);
                ZSTD_storeLastLiterals(&cctx->seqStore, ip, lastLLSize);
                seqPos->posInSrc += lastLLSize;
            }

            return bytesAdjustment;
        }

        private static ZSTD_sequenceCopier? ZSTD_selectSequenceCopier(ZSTD_sequenceFormat_e mode)
        {
            ZSTD_sequenceCopier? sequenceCopier = null;

            assert((ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam11, (int)mode)) != 0);
            if (mode == ZSTD_sequenceFormat_e.ZSTD_sf_explicitBlockDelimiters)
            {
                return (ZSTD_sequenceCopier)ZSTD_copySequencesToSeqStoreExplicitBlockDelim;
            }
            else if (mode == ZSTD_sequenceFormat_e.ZSTD_sf_noBlockDelimiters)
            {
                return (ZSTD_sequenceCopier)ZSTD_copySequencesToSeqStoreNoBlockDelim;
            }

            assert(sequenceCopier != null);
            return sequenceCopier;
        }

        /* Compress, block-by-block, all of the sequences given.
         *
         * Returns the cumulative size of all compressed blocks (including their headers), otherwise a ZSTD error.
         */
        private static nuint ZSTD_compressSequences_internal(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, ZSTD_Sequence* inSeqs, nuint inSeqsSize, void* src, nuint srcSize)
        {
            nuint cSize = 0;
            uint lastBlock;
            nuint blockSize;
            nuint compressedSeqsSize;
            nuint remaining = srcSize;
            ZSTD_sequencePosition seqPos = new ZSTD_sequencePosition
            {
                idx = 0,
                posInSequence = 0,
                posInSrc = 0,
            };
            byte* ip = (byte*)(src);
            byte* op = (byte*)(dst);
            ZSTD_sequenceCopier sequenceCopier = ZSTD_selectSequenceCopier(cctx->appliedParams.blockDelimiters) ?? throw new InvalidOperationException();

            if (remaining == 0)
            {
                uint cBlockHeader24 = 1 + (((uint)(blockType_e.bt_raw)) << 1);

                if (dstCapacity < 4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                MEM_writeLE32((void*)op, cBlockHeader24);
                op += ZSTD_blockHeaderSize;
                dstCapacity -= ZSTD_blockHeaderSize;
                cSize += ZSTD_blockHeaderSize;
            }

            while (remaining != 0)
            {
                nuint cBlockSize;
                nuint additionalByteAdjustment;

                lastBlock = ((remaining <= cctx->blockSize) ? 1U : 0U);
                blockSize = lastBlock != 0 ? (uint)(remaining) : (uint)(cctx->blockSize);
                ZSTD_resetSeqStore(&cctx->seqStore);
                additionalByteAdjustment = sequenceCopier(cctx, &seqPos, inSeqs, inSeqsSize, (void*)ip, blockSize);

                {
                    nuint err_code = (additionalByteAdjustment);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                blockSize -= additionalByteAdjustment;
                if (blockSize < (uint)((1 + 1 + 1)) + ZSTD_blockHeaderSize + 1)
                {
                    cBlockSize = ZSTD_noCompressBlock((void*)op, dstCapacity, (void*)ip, blockSize, lastBlock);

                    {
                        nuint err_code = (cBlockSize);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    cSize += cBlockSize;
                    ip += blockSize;
                    op += cBlockSize;
                    remaining -= blockSize;
                    dstCapacity -= cBlockSize;
                    continue;
                }

                compressedSeqsSize = ZSTD_entropyCompressSeqStore(&cctx->seqStore, &cctx->blockState.prevCBlock->entropy, &cctx->blockState.nextCBlock->entropy, &cctx->appliedParams, (void*)(op + ZSTD_blockHeaderSize), dstCapacity - ZSTD_blockHeaderSize, blockSize, (void*)cctx->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2)))), cctx->bmi2);

                {
                    nuint err_code = (compressedSeqsSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (cctx->isFirstBlock == 0 && (ZSTD_maybeRLE(&cctx->seqStore)) != 0 && (ZSTD_isRLE((byte*)(src), srcSize)) != 0)
                {
                    compressedSeqsSize = 1;
                }

                if (compressedSeqsSize == 0)
                {
                    cBlockSize = ZSTD_noCompressBlock((void*)op, dstCapacity, (void*)ip, blockSize, lastBlock);

                    {
                        nuint err_code = (cBlockSize);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }
                else if (compressedSeqsSize == 1)
                {
                    cBlockSize = ZSTD_rleCompressBlock((void*)op, dstCapacity, *ip, blockSize, lastBlock);

                    {
                        nuint err_code = (cBlockSize);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }
                else
                {
                    uint cBlockHeader;

                    ZSTD_blockState_confirmRepcodesAndEntropyTables(&cctx->blockState);
                    if (cctx->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid)
                    {
                        cctx->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
                    }

                    cBlockHeader = lastBlock + (((uint)(blockType_e.bt_compressed)) << 1) + (uint)(compressedSeqsSize << 3);
                    MEM_writeLE24((void*)op, cBlockHeader);
                    cBlockSize = ZSTD_blockHeaderSize + compressedSeqsSize;
                }

                cSize += cBlockSize;
                if (lastBlock != 0)
                {
                    break;
                }
                else
                {
                    ip += blockSize;
                    op += cBlockSize;
                    remaining -= blockSize;
                    dstCapacity -= cBlockSize;
                    cctx->isFirstBlock = 0;
                }
            }

            return cSize;
        }

        /*! ZSTD_compressSequences() :
         * Compress an array of ZSTD_Sequence, generated from the original source buffer, into dst.
         * If a dictionary is included, then the cctx should reference the dict. (see: ZSTD_CCtx_refCDict(), ZSTD_CCtx_loadDictionary(), etc.)
         * The entire source is compressed into a single frame.
         *
         * The compression behavior changes based on cctx params. In particular:
         *    If ZSTD_c_blockDelimiters == ZSTD_sf_noBlockDelimiters, the array of ZSTD_Sequence is expected to contain
         *    no block delimiters (defined in ZSTD_Sequence). Block boundaries are roughly determined based on
         *    the block size derived from the cctx, and sequences may be split. This is the default setting.
         *
         *    If ZSTD_c_blockDelimiters == ZSTD_sf_explicitBlockDelimiters, the array of ZSTD_Sequence is expected to contain
         *    block delimiters (defined in ZSTD_Sequence). Behavior is undefined if no block delimiters are provided.
         *
         *    If ZSTD_c_validateSequences == 0, this function will blindly accept the sequences provided. Invalid sequences cause undefined
         *    behavior. If ZSTD_c_validateSequences == 1, then if sequence is invalid (see doc/zstd_compression_format.md for
         *    specifics regarding offset/matchlength requirements) then the function will bail out and return an error.
         *
         *    In addition to the two adjustable experimental params, there are other important cctx params.
         *    - ZSTD_c_minMatch MUST be set as less than or equal to the smallest match generated by the match finder. It has a minimum value of ZSTD_MINMATCH_MIN.
         *    - ZSTD_c_compressionLevel accordingly adjusts the strength of the entropy coder, as it would in typical compression.
         *    - ZSTD_c_windowLog affects offset validation: this function will return an error at higher debug levels if a provided offset
         *      is larger than what the spec allows for a given window log and dictionary (if present). See: doc/zstd_compression_format.md
         *
         * Note: Repcodes are, as of now, always re-calculated within this function, so ZSTD_Sequence::rep is unused.
         * Note 2: Once we integrate ability to ingest repcodes, the explicit block delims mode must respect those repcodes exactly,
         *         and cannot emit an RLE block that disagrees with the repcode history
         * @return : final compressed size or a ZSTD error.
         */
        public static nuint ZSTD_compressSequences(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity, ZSTD_Sequence* inSeqs, nuint inSeqsSize, void* src, nuint srcSize)
        {
            byte* op = (byte*)(dst);
            nuint cSize = 0;
            nuint compressedBlocksSize = 0;
            nuint frameHeaderSize = 0;

            assert(cctx != null);

            {
                nuint err_code = (ZSTD_CCtx_init_compressStream2(cctx, ZSTD_EndDirective.ZSTD_e_end, srcSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            frameHeaderSize = ZSTD_writeFrameHeader((void*)op, dstCapacity, &cctx->appliedParams, (ulong)srcSize, cctx->dictID);
            op += frameHeaderSize;
            dstCapacity -= frameHeaderSize;
            cSize += frameHeaderSize;
            if (cctx->appliedParams.fParams.checksumFlag != 0 && srcSize != 0)
            {
                XXH64_update(&cctx->xxhState, src, srcSize);
            }

            compressedBlocksSize = ZSTD_compressSequences_internal(cctx, (void*)op, dstCapacity, inSeqs, inSeqsSize, src, srcSize);

            {
                nuint err_code = (compressedBlocksSize);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            cSize += compressedBlocksSize;
            dstCapacity -= compressedBlocksSize;
            if (cctx->appliedParams.fParams.checksumFlag != 0)
            {
                uint checksum = (uint)(XXH64_digest(&cctx->xxhState));

                if (dstCapacity < 4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                MEM_writeLE32((void*)((sbyte*)(dst) + cSize), checksum);
                cSize += 4;
            }

            return cSize;
        }

        /*! ZSTD_flushStream() :
         * @return : amount of data remaining to flush */
        public static nuint ZSTD_flushStream(ZSTD_CCtx_s* zcs, ZSTD_outBuffer_s* output)
        {
            ZSTD_inBuffer_s input = new ZSTD_inBuffer_s
            {
                src = null,
                size = 0,
                pos = 0,
            };

            return ZSTD_compressStream2(zcs, output, &input, ZSTD_EndDirective.ZSTD_e_flush);
        }

        /*! Equivalent to ZSTD_compressStream2(zcs, output, &emptyInput, ZSTD_e_end). */
        public static nuint ZSTD_endStream(ZSTD_CCtx_s* zcs, ZSTD_outBuffer_s* output)
        {
            ZSTD_inBuffer_s input = new ZSTD_inBuffer_s
            {
                src = null,
                size = 0,
                pos = 0,
            };
            nuint remainingToFlush = ZSTD_compressStream2(zcs, output, &input, ZSTD_EndDirective.ZSTD_e_end);


            {
                nuint err_code = (remainingToFlush);

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            if (zcs->appliedParams.nbWorkers > 0)
            {
                return remainingToFlush;
            }


            {
                nuint lastBlockSize = (nuint)(zcs->frameEnded != 0 ? 0 : 3);
                nuint checksumSize = (nuint)(zcs->frameEnded != 0 ? 0 : zcs->appliedParams.fParams.checksumFlag * 4);
                nuint toFlush = remainingToFlush + lastBlockSize + checksumSize;

                return toFlush;
            }
        }

        public static int ZSTD_maxCLevel()
        {
            return 22;
        }

        public static int ZSTD_minCLevel()
        {
            return (int)(-(1 << 17));
        }

        public static int ZSTD_defaultCLevel()
        {
            return 3;
        }

        public static ZSTD_compressionParameters[][] ZSTD_defaultCParameters = new ZSTD_compressionParameters[4][]
        {
            new ZSTD_compressionParameters[23]
            {
                new ZSTD_compressionParameters
                {
                    windowLog = 19,
                    chainLog = 12,
                    hashLog = 13,
                    searchLog = 1,
                    minMatch = 6,
                    targetLength = 1,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 19,
                    chainLog = 13,
                    hashLog = 14,
                    searchLog = 1,
                    minMatch = 7,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 20,
                    chainLog = 15,
                    hashLog = 16,
                    searchLog = 1,
                    minMatch = 6,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 16,
                    hashLog = 17,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 18,
                    hashLog = 18,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 2,
                    minMatch = 5,
                    targetLength = 2,
                    strategy = ZSTD_strategy.ZSTD_greedy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 3,
                    minMatch = 5,
                    targetLength = 4,
                    strategy = ZSTD_strategy.ZSTD_greedy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 3,
                    minMatch = 5,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 3,
                    minMatch = 5,
                    targetLength = 16,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 21,
                    chainLog = 19,
                    hashLog = 20,
                    searchLog = 4,
                    minMatch = 5,
                    targetLength = 16,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 20,
                    hashLog = 21,
                    searchLog = 4,
                    minMatch = 5,
                    targetLength = 16,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 21,
                    hashLog = 22,
                    searchLog = 4,
                    minMatch = 5,
                    targetLength = 16,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 21,
                    hashLog = 22,
                    searchLog = 5,
                    minMatch = 5,
                    targetLength = 16,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 21,
                    hashLog = 22,
                    searchLog = 5,
                    minMatch = 5,
                    targetLength = 32,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 22,
                    hashLog = 23,
                    searchLog = 5,
                    minMatch = 5,
                    targetLength = 32,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 23,
                    hashLog = 23,
                    searchLog = 6,
                    minMatch = 5,
                    targetLength = 32,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 22,
                    chainLog = 22,
                    hashLog = 22,
                    searchLog = 5,
                    minMatch = 5,
                    targetLength = 48,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 23,
                    chainLog = 23,
                    hashLog = 22,
                    searchLog = 5,
                    minMatch = 4,
                    targetLength = 64,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 23,
                    chainLog = 23,
                    hashLog = 22,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 64,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 23,
                    chainLog = 24,
                    hashLog = 22,
                    searchLog = 7,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 25,
                    chainLog = 25,
                    hashLog = 23,
                    searchLog = 7,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 26,
                    chainLog = 26,
                    hashLog = 24,
                    searchLog = 7,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 27,
                    chainLog = 27,
                    hashLog = 25,
                    searchLog = 9,
                    minMatch = 3,
                    targetLength = 999,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
            },
            new ZSTD_compressionParameters[23]
            {
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 12,
                    hashLog = 13,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 1,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 13,
                    hashLog = 14,
                    searchLog = 1,
                    minMatch = 6,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 14,
                    hashLog = 14,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 16,
                    hashLog = 16,
                    searchLog = 1,
                    minMatch = 4,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 16,
                    hashLog = 17,
                    searchLog = 2,
                    minMatch = 5,
                    targetLength = 2,
                    strategy = ZSTD_strategy.ZSTD_greedy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 18,
                    searchLog = 3,
                    minMatch = 5,
                    targetLength = 2,
                    strategy = ZSTD_strategy.ZSTD_greedy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 3,
                    minMatch = 5,
                    targetLength = 4,
                    strategy = ZSTD_strategy.ZSTD_lazy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 4,
                    minMatch = 4,
                    targetLength = 4,
                    strategy = ZSTD_strategy.ZSTD_lazy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 4,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 5,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 6,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 5,
                    minMatch = 4,
                    targetLength = 12,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 7,
                    minMatch = 4,
                    targetLength = 12,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 4,
                    minMatch = 4,
                    targetLength = 16,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 4,
                    minMatch = 3,
                    targetLength = 32,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 18,
                    hashLog = 19,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 128,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 128,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 8,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 128,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 8,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 10,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 12,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 18,
                    chainLog = 19,
                    hashLog = 19,
                    searchLog = 13,
                    minMatch = 3,
                    targetLength = 999,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
            },
            new ZSTD_compressionParameters[23]
            {
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 12,
                    hashLog = 12,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 1,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 12,
                    hashLog = 13,
                    searchLog = 1,
                    minMatch = 6,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 13,
                    hashLog = 15,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 15,
                    hashLog = 16,
                    searchLog = 2,
                    minMatch = 5,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 2,
                    minMatch = 4,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 16,
                    hashLog = 17,
                    searchLog = 3,
                    minMatch = 4,
                    targetLength = 2,
                    strategy = ZSTD_strategy.ZSTD_greedy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 3,
                    minMatch = 4,
                    targetLength = 4,
                    strategy = ZSTD_strategy.ZSTD_lazy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 3,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 4,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 5,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 6,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 17,
                    hashLog = 17,
                    searchLog = 5,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 7,
                    minMatch = 4,
                    targetLength = 12,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 3,
                    minMatch = 4,
                    targetLength = 12,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 4,
                    minMatch = 3,
                    targetLength = 32,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 128,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 8,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 10,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 5,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 7,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 9,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 17,
                    chainLog = 18,
                    hashLog = 17,
                    searchLog = 11,
                    minMatch = 3,
                    targetLength = 999,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
            },
            new ZSTD_compressionParameters[23]
            {
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 12,
                    hashLog = 13,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 1,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 15,
                    searchLog = 1,
                    minMatch = 5,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 15,
                    searchLog = 1,
                    minMatch = 4,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_fast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 15,
                    searchLog = 2,
                    minMatch = 4,
                    targetLength = 0,
                    strategy = ZSTD_strategy.ZSTD_dfast,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 14,
                    searchLog = 4,
                    minMatch = 4,
                    targetLength = 2,
                    strategy = ZSTD_strategy.ZSTD_greedy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 14,
                    searchLog = 3,
                    minMatch = 4,
                    targetLength = 4,
                    strategy = ZSTD_strategy.ZSTD_lazy,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 14,
                    searchLog = 4,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 14,
                    searchLog = 6,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 14,
                    hashLog = 14,
                    searchLog = 8,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_lazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 14,
                    searchLog = 5,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 14,
                    searchLog = 9,
                    minMatch = 4,
                    targetLength = 8,
                    strategy = ZSTD_strategy.ZSTD_btlazy2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 14,
                    searchLog = 3,
                    minMatch = 4,
                    targetLength = 12,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 14,
                    searchLog = 4,
                    minMatch = 3,
                    targetLength = 24,
                    strategy = ZSTD_strategy.ZSTD_btopt,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 14,
                    searchLog = 5,
                    minMatch = 3,
                    targetLength = 32,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 64,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 7,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 5,
                    minMatch = 3,
                    targetLength = 48,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 6,
                    minMatch = 3,
                    targetLength = 128,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 7,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 8,
                    minMatch = 3,
                    targetLength = 256,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 8,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 9,
                    minMatch = 3,
                    targetLength = 512,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
                new ZSTD_compressionParameters
                {
                    windowLog = 14,
                    chainLog = 15,
                    hashLog = 15,
                    searchLog = 10,
                    minMatch = 3,
                    targetLength = 999,
                    strategy = ZSTD_strategy.ZSTD_btultra2,
                },
            },
        };

        private static ZSTD_compressionParameters ZSTD_dedicatedDictSearch_getCParams(int compressionLevel, nuint dictSize)
        {
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, 0, dictSize, ZSTD_cParamMode_e.ZSTD_cpm_createCDict);

            switch (cParams.strategy)
            {
                case ZSTD_strategy.ZSTD_fast:
                case ZSTD_strategy.ZSTD_dfast:
                {
                    break;
                }

                case ZSTD_strategy.ZSTD_greedy:
                case ZSTD_strategy.ZSTD_lazy:
                case ZSTD_strategy.ZSTD_lazy2:
                {
                    cParams.hashLog += 2;
                }

                break;
                case ZSTD_strategy.ZSTD_btlazy2:
                case ZSTD_strategy.ZSTD_btopt:
                case ZSTD_strategy.ZSTD_btultra:
                case ZSTD_strategy.ZSTD_btultra2:
                {
                    break;
                }
            }

            return cParams;
        }

        private static int ZSTD_dedicatedDictSearch_isSupported(ZSTD_compressionParameters* cParams)
        {
            return (((cParams->strategy >= ZSTD_strategy.ZSTD_greedy) && (cParams->strategy <= ZSTD_strategy.ZSTD_lazy2) && (cParams->hashLog > cParams->chainLog) && (cParams->chainLog <= 24)) ? 1 : 0);
        }

        /**
         * Reverses the adjustment applied to cparams when enabling dedicated dict
         * search. This is used to recover the params set to be used in the working
         * context. (Otherwise, those tables would also grow.)
         */
        private static void ZSTD_dedicatedDictSearch_revertCParams(ZSTD_compressionParameters* cParams)
        {
            switch (cParams->strategy)
            {
                case ZSTD_strategy.ZSTD_fast:
                case ZSTD_strategy.ZSTD_dfast:
                {
                    break;
                }

                case ZSTD_strategy.ZSTD_greedy:
                case ZSTD_strategy.ZSTD_lazy:
                case ZSTD_strategy.ZSTD_lazy2:
                {
                    cParams->hashLog -= 2;
                }

                if (cParams->hashLog < 6)
                {
                    cParams->hashLog = 6;
                }

                break;
                case ZSTD_strategy.ZSTD_btlazy2:
                case ZSTD_strategy.ZSTD_btopt:
                case ZSTD_strategy.ZSTD_btultra:
                case ZSTD_strategy.ZSTD_btultra2:
                {
                    break;
                }
            }
        }

        private static ulong ZSTD_getCParamRowSize(ulong srcSizeHint, nuint dictSize, ZSTD_cParamMode_e mode)
        {
            switch (mode)
            {
                case ZSTD_cParamMode_e.ZSTD_cpm_unknown:
                case ZSTD_cParamMode_e.ZSTD_cpm_noAttachDict:
                case ZSTD_cParamMode_e.ZSTD_cpm_createCDict:
                {
                    break;
                }

                case ZSTD_cParamMode_e.ZSTD_cpm_attachDict:
                {
                    dictSize = 0;
                }

                break;
                default:
                {
                    assert(0 != 0);
                }

                break;
            }


            {
                int unknown = ((srcSizeHint == (unchecked(0UL - 1))) ? 1 : 0);
                nuint addedSize = (nuint)(unchecked(unknown) != 0 && dictSize > 0 ? 500 : 0);

                return unchecked(unknown) != 0 && dictSize == 0 ? (unchecked(0UL - 1)) : srcSizeHint + dictSize + addedSize;
            }
        }

        /*! ZSTD_getCParams_internal() :
         * @return ZSTD_compressionParameters structure for a selected compression level, srcSize and dictSize.
         *  Note: srcSizeHint 0 means 0, use ZSTD_CONTENTSIZE_UNKNOWN for unknown.
         *        Use dictSize == 0 for unknown or unused.
         *  Note: `mode` controls how we treat the `dictSize`. See docs for `ZSTD_cParamMode_e`. */
        private static ZSTD_compressionParameters ZSTD_getCParams_internal(int compressionLevel, ulong srcSizeHint, nuint dictSize, ZSTD_cParamMode_e mode)
        {
            ulong rSize = ZSTD_getCParamRowSize(srcSizeHint, dictSize, mode);
            uint tableID = (uint)(((rSize <= (uint)(256 * (1 << 10))) ? 1 : 0) + ((rSize <= (uint)(128 * (1 << 10))) ? 1 : 0) + ((rSize <= (uint)(16 * (1 << 10))) ? 1 : 0));
            int row;

            if (compressionLevel == 0)
            {
                row = 3;
            }
            else if (compressionLevel < 0)
            {
                row = 0;
            }
            else if (compressionLevel > 22)
            {
                row = 22;
            }
            else
            {
                row = compressionLevel;
            }


            {
                ZSTD_compressionParameters cp = ZSTD_defaultCParameters[tableID][row];

                if (compressionLevel < 0)
                {
                    int clampedCompressionLevel = ((ZSTD_minCLevel()) > (compressionLevel) ? (ZSTD_minCLevel()) : (compressionLevel));

                    cp.targetLength = unchecked((uint)(-clampedCompressionLevel));
                }

                return ZSTD_adjustCParams_internal(cp, srcSizeHint, dictSize, mode);
            }
        }

        /*! ZSTD_getCParams() :
         * @return ZSTD_compressionParameters structure for a selected compression level, srcSize and dictSize.
         *  Size values are optional, provide 0 if not known or unused */
        public static ZSTD_compressionParameters ZSTD_getCParams(int compressionLevel, ulong srcSizeHint, nuint dictSize)
        {
            if (srcSizeHint == 0)
            {
                srcSizeHint = (unchecked(0UL - 1));
            }

            return ZSTD_getCParams_internal(compressionLevel, srcSizeHint, dictSize, ZSTD_cParamMode_e.ZSTD_cpm_unknown);
        }

        /*! ZSTD_getParams() :
         *  same idea as ZSTD_getCParams()
         * @return a `ZSTD_parameters` structure (instead of `ZSTD_compressionParameters`).
         *  Fields of `ZSTD_frameParameters` are set to default values */
        private static ZSTD_parameters ZSTD_getParams_internal(int compressionLevel, ulong srcSizeHint, nuint dictSize, ZSTD_cParamMode_e mode)
        {
            ZSTD_parameters @params;
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(compressionLevel, srcSizeHint, dictSize, mode);

            memset((void*)(&@params), (0), ((nuint)(sizeof(ZSTD_parameters))));
            @params.cParams = cParams;
            @params.fParams.contentSizeFlag = 1;
            return @params;
        }

        /*! ZSTD_getParams() :
         *  same idea as ZSTD_getCParams()
         * @return a `ZSTD_parameters` structure (instead of `ZSTD_compressionParameters`).
         *  Fields of `ZSTD_frameParameters` are set to default values */
        public static ZSTD_parameters ZSTD_getParams(int compressionLevel, ulong srcSizeHint, nuint dictSize)
        {
            if (srcSizeHint == 0)
            {
                srcSizeHint = (unchecked(0UL - 1));
            }

            return ZSTD_getParams_internal(compressionLevel, srcSizeHint, dictSize, ZSTD_cParamMode_e.ZSTD_cpm_unknown);
        }
    }
}
