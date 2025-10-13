using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*-*************************************
     *  Helper functions
     ***************************************/
    /* ZSTD_compressBound()
     * Note that the result from this function is only valid for
     * the one-pass compression functions.
     * When employing the streaming mode,
     * if flushes are frequently altering the size of blocks,
     * the overhead from block headers can make the compressed data larger
     * than the return value of ZSTD_compressBound().
     */
    public static nuint ZSTD_compressBound(nuint srcSize)
    {
        nuint r =
            srcSize >= (sizeof(nuint) == 8 ? 0xFF00FF00FF00FF00UL : 0xFF00FF00U)
                ? 0
                : srcSize
                    + (srcSize >> 8)
                    + (srcSize < 128 << 10 ? (128 << 10) - srcSize >> 11 : 0);
        if (r == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        return r;
    }

    public static ZSTD_CCtx_s* ZSTD_createCCtx()
    {
        return ZSTD_createCCtx_advanced(ZSTD_defaultCMem);
    }

    private static void ZSTD_initCCtx(ZSTD_CCtx_s* cctx, ZSTD_customMem memManager)
    {
        assert(cctx != null);
        *cctx = new ZSTD_CCtx_s { customMem = memManager, bmi2 = 0 };
        {
            nuint err = ZSTD_CCtx_reset(cctx, ZSTD_ResetDirective.ZSTD_reset_parameters);
            assert(!ERR_isError(err));
        }
    }

    public static ZSTD_CCtx_s* ZSTD_createCCtx_advanced(ZSTD_customMem customMem)
    {
        if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            return null;
        {
            ZSTD_CCtx_s* cctx = (ZSTD_CCtx_s*)ZSTD_customMalloc(
                (nuint)sizeof(ZSTD_CCtx_s),
                customMem
            );
            if (cctx == null)
                return null;
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
        if (workspaceSize <= (nuint)sizeof(ZSTD_CCtx_s))
            return null;
        if (((nuint)workspace & 7) != 0)
            return null;
        ZSTD_cwksp_init(
            &ws,
            workspace,
            workspaceSize,
            ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_static_alloc
        );
        cctx = (ZSTD_CCtx_s*)ZSTD_cwksp_reserve_object(&ws, (nuint)sizeof(ZSTD_CCtx_s));
        if (cctx == null)
            return null;
        *cctx = new ZSTD_CCtx_s();
        ZSTD_cwksp_move(&cctx->workspace, &ws);
        cctx->staticSize = workspaceSize;
        if (
            ZSTD_cwksp_check_available(
                &cctx->workspace,
                (nuint)(
                    (
                        (8 << 10) + 512 + sizeof(uint) * (52 + 2) > 8208
                            ? (8 << 10) + 512 + sizeof(uint) * (52 + 2)
                            : 8208
                    )
                    + 2 * sizeof(ZSTD_compressedBlockState_t)
                )
            ) == 0
        )
            return null;
        cctx->blockState.prevCBlock = (ZSTD_compressedBlockState_t*)ZSTD_cwksp_reserve_object(
            &cctx->workspace,
            (nuint)sizeof(ZSTD_compressedBlockState_t)
        );
        cctx->blockState.nextCBlock = (ZSTD_compressedBlockState_t*)ZSTD_cwksp_reserve_object(
            &cctx->workspace,
            (nuint)sizeof(ZSTD_compressedBlockState_t)
        );
        cctx->tmpWorkspace = ZSTD_cwksp_reserve_object(
            &cctx->workspace,
            (8 << 10) + 512 + sizeof(uint) * (52 + 2) > 8208
                ? (8 << 10) + 512 + sizeof(uint) * (52 + 2)
                : 8208
        );
        cctx->tmpWkspSize =
            (8 << 10) + 512 + sizeof(uint) * (52 + 2) > 8208
                ? (8 << 10) + 512 + sizeof(uint) * (52 + 2)
                : 8208;
        cctx->bmi2 = 0;
        return cctx;
    }

    /**
     * Clears and frees all of the dictionaries in the CCtx.
     */
    private static void ZSTD_clearAllDicts(ZSTD_CCtx_s* cctx)
    {
        ZSTD_customFree(cctx->localDict.dictBuffer, cctx->customMem);
        ZSTD_freeCDict(cctx->localDict.cdict);
        cctx->localDict = new ZSTD_localDict();
        cctx->prefixDict = new ZSTD_prefixDict_s();
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
        ZSTDMT_freeCCtx(cctx->mtctx);
        cctx->mtctx = null;
        ZSTD_cwksp_free(&cctx->workspace, cctx->customMem);
    }

    public static nuint ZSTD_freeCCtx(ZSTD_CCtx_s* cctx)
    {
        if (cctx == null)
            return 0;
        if (cctx->staticSize != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }

        {
            int cctxInWorkspace = ZSTD_cwksp_owns_buffer(&cctx->workspace, cctx);
            ZSTD_freeCCtxContent(cctx);
            if (cctxInWorkspace == 0)
                ZSTD_customFree(cctx, cctx->customMem);
        }

        return 0;
    }

    private static nuint ZSTD_sizeof_mtctx(ZSTD_CCtx_s* cctx)
    {
        return ZSTDMT_sizeof_CCtx(cctx->mtctx);
    }

    /*! ZSTD_sizeof_*() : Requires v1.4.0+
     *  These functions give the _current_ memory usage of selected object.
     *  Note that object memory usage can evolve (increase or decrease) over time. */
    public static nuint ZSTD_sizeof_CCtx(ZSTD_CCtx_s* cctx)
    {
        if (cctx == null)
            return 0;
        return (nuint)(cctx->workspace.workspace == cctx ? 0 : sizeof(ZSTD_CCtx_s))
            + ZSTD_cwksp_sizeof(&cctx->workspace)
            + ZSTD_sizeof_localDict(cctx->localDict)
            + ZSTD_sizeof_mtctx(cctx);
    }

    public static nuint ZSTD_sizeof_CStream(ZSTD_CCtx_s* zcs)
    {
        return ZSTD_sizeof_CCtx(zcs);
    }

    /* private API call, for dictBuilder only */
    private static SeqStore_t* ZSTD_getSeqStore(ZSTD_CCtx_s* ctx)
    {
        return &ctx->seqStore;
    }

    /* Returns true if the strategy supports using a row based matchfinder */
    private static int ZSTD_rowMatchFinderSupported(ZSTD_strategy strategy)
    {
        return strategy >= ZSTD_strategy.ZSTD_greedy && strategy <= ZSTD_strategy.ZSTD_lazy2
            ? 1
            : 0;
    }

    /* Returns true if the strategy and useRowMatchFinder mode indicate that we will use the row based matchfinder
     * for this compression.
     */
    private static int ZSTD_rowMatchFinderUsed(ZSTD_strategy strategy, ZSTD_paramSwitch_e mode)
    {
        assert(mode != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        return
            ZSTD_rowMatchFinderSupported(strategy) != 0 && mode == ZSTD_paramSwitch_e.ZSTD_ps_enable
            ? 1
            : 0;
    }

    /* Returns row matchfinder usage given an initial mode and cParams */
    private static ZSTD_paramSwitch_e ZSTD_resolveRowMatchFinderMode(
        ZSTD_paramSwitch_e mode,
        ZSTD_compressionParameters* cParams
    )
    {
        if (mode != ZSTD_paramSwitch_e.ZSTD_ps_auto)
            return mode;
        mode = ZSTD_paramSwitch_e.ZSTD_ps_disable;
        if (ZSTD_rowMatchFinderSupported(cParams->strategy) == 0)
            return mode;
        if (cParams->windowLog > 14)
            mode = ZSTD_paramSwitch_e.ZSTD_ps_enable;
        return mode;
    }

    /* Returns block splitter usage (generally speaking, when using slower/stronger compression modes) */
    private static ZSTD_paramSwitch_e ZSTD_resolveBlockSplitterMode(
        ZSTD_paramSwitch_e mode,
        ZSTD_compressionParameters* cParams
    )
    {
        if (mode != ZSTD_paramSwitch_e.ZSTD_ps_auto)
            return mode;
        return cParams->strategy >= ZSTD_strategy.ZSTD_btopt && cParams->windowLog >= 17
            ? ZSTD_paramSwitch_e.ZSTD_ps_enable
            : ZSTD_paramSwitch_e.ZSTD_ps_disable;
    }

    /* Returns 1 if the arguments indicate that we should allocate a chainTable, 0 otherwise */
    private static int ZSTD_allocateChainTable(
        ZSTD_strategy strategy,
        ZSTD_paramSwitch_e useRowMatchFinder,
        uint forDDSDict
    )
    {
        assert(useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        return
            forDDSDict != 0
            || strategy != ZSTD_strategy.ZSTD_fast
                && ZSTD_rowMatchFinderUsed(strategy, useRowMatchFinder) == 0
            ? 1
            : 0;
    }

    /* Returns ZSTD_ps_enable if compression parameters are such that we should
     * enable long distance matching (wlog >= 27, strategy >= btopt).
     * Returns ZSTD_ps_disable otherwise.
     */
    private static ZSTD_paramSwitch_e ZSTD_resolveEnableLdm(
        ZSTD_paramSwitch_e mode,
        ZSTD_compressionParameters* cParams
    )
    {
        if (mode != ZSTD_paramSwitch_e.ZSTD_ps_auto)
            return mode;
        return cParams->strategy >= ZSTD_strategy.ZSTD_btopt && cParams->windowLog >= 27
            ? ZSTD_paramSwitch_e.ZSTD_ps_enable
            : ZSTD_paramSwitch_e.ZSTD_ps_disable;
    }

    private static int ZSTD_resolveExternalSequenceValidation(int mode)
    {
        return mode;
    }

    /* Resolves maxBlockSize to the default if no value is present. */
    private static nuint ZSTD_resolveMaxBlockSize(nuint maxBlockSize)
    {
        if (maxBlockSize == 0)
        {
            return 1 << 17;
        }
        else
        {
            return maxBlockSize;
        }
    }

    private static ZSTD_paramSwitch_e ZSTD_resolveExternalRepcodeSearch(
        ZSTD_paramSwitch_e value,
        int cLevel
    )
    {
        if (value != ZSTD_paramSwitch_e.ZSTD_ps_auto)
            return value;
        if (cLevel < 10)
        {
            return ZSTD_paramSwitch_e.ZSTD_ps_disable;
        }
        else
        {
            return ZSTD_paramSwitch_e.ZSTD_ps_enable;
        }
    }

    /* Returns 1 if compression parameters are such that CDict hashtable and chaintable indices are tagged.
     * If so, the tags need to be removed in ZSTD_resetCCtx_byCopyingCDict. */
    private static int ZSTD_CDictIndicesAreTagged(ZSTD_compressionParameters* cParams)
    {
        return
            cParams->strategy == ZSTD_strategy.ZSTD_fast
            || cParams->strategy == ZSTD_strategy.ZSTD_dfast
            ? 1
            : 0;
    }

    private static ZSTD_CCtx_params_s ZSTD_makeCCtxParamsFromCParams(
        ZSTD_compressionParameters cParams
    )
    {
        ZSTD_CCtx_params_s cctxParams;
        ZSTD_CCtxParams_init(&cctxParams, 3);
        cctxParams.cParams = cParams;
        cctxParams.ldmParams.enableLdm = ZSTD_resolveEnableLdm(
            cctxParams.ldmParams.enableLdm,
            &cParams
        );
        if (cctxParams.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            ZSTD_ldm_adjustParameters(&cctxParams.ldmParams, &cParams);
            assert(cctxParams.ldmParams.hashLog >= cctxParams.ldmParams.bucketSizeLog);
            assert(cctxParams.ldmParams.hashRateLog < 32);
        }

        cctxParams.postBlockSplitter = ZSTD_resolveBlockSplitterMode(
            cctxParams.postBlockSplitter,
            &cParams
        );
        cctxParams.useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
            cctxParams.useRowMatchFinder,
            &cParams
        );
        cctxParams.validateSequences = ZSTD_resolveExternalSequenceValidation(
            cctxParams.validateSequences
        );
        cctxParams.maxBlockSize = ZSTD_resolveMaxBlockSize(cctxParams.maxBlockSize);
        cctxParams.searchForExternalRepcodes = ZSTD_resolveExternalRepcodeSearch(
            cctxParams.searchForExternalRepcodes,
            cctxParams.compressionLevel
        );
        assert(ZSTD_checkCParams(cParams) == 0);
        return cctxParams;
    }

    private static ZSTD_CCtx_params_s* ZSTD_createCCtxParams_advanced(ZSTD_customMem customMem)
    {
        ZSTD_CCtx_params_s* @params;
        if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            return null;
        @params = (ZSTD_CCtx_params_s*)ZSTD_customCalloc(
            (nuint)sizeof(ZSTD_CCtx_params_s),
            customMem
        );
        if (@params == null)
        {
            return null;
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

        ZSTD_customFree(@params, @params->customMem);
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
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }

        *cctxParams = new ZSTD_CCtx_params_s { compressionLevel = compressionLevel };
        cctxParams->fParams.contentSizeFlag = 1;
        return 0;
    }

    /**
     * Initializes `cctxParams` from `params` and `compressionLevel`.
     * @param compressionLevel If params are derived from a compression level then that compression level, otherwise ZSTD_NO_CLEVEL.
     */
    private static void ZSTD_CCtxParams_init_internal(
        ZSTD_CCtx_params_s* cctxParams,
        ZSTD_parameters* @params,
        int compressionLevel
    )
    {
        assert(ZSTD_checkCParams(@params->cParams) == 0);
        *cctxParams = new ZSTD_CCtx_params_s
        {
            cParams = @params->cParams,
            fParams = @params->fParams,
            compressionLevel = compressionLevel,
            useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
                cctxParams->useRowMatchFinder,
                &@params->cParams
            ),
            postBlockSplitter = ZSTD_resolveBlockSplitterMode(
                cctxParams->postBlockSplitter,
                &@params->cParams
            ),
        };
        cctxParams->ldmParams.enableLdm = ZSTD_resolveEnableLdm(
            cctxParams->ldmParams.enableLdm,
            &@params->cParams
        );
        cctxParams->validateSequences = ZSTD_resolveExternalSequenceValidation(
            cctxParams->validateSequences
        );
        cctxParams->maxBlockSize = ZSTD_resolveMaxBlockSize(cctxParams->maxBlockSize);
        cctxParams->searchForExternalRepcodes = ZSTD_resolveExternalRepcodeSearch(
            cctxParams->searchForExternalRepcodes,
            compressionLevel
        );
    }

    /*! ZSTD_CCtxParams_init_advanced() :
     *  Initializes the compression and frame parameters of cctxParams according to
     *  params. All other parameters are reset to their default values.
     */
    public static nuint ZSTD_CCtxParams_init_advanced(
        ZSTD_CCtx_params_s* cctxParams,
        ZSTD_parameters @params
    )
    {
        if (cctxParams == null)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }

        {
            nuint err_code = ZSTD_checkCParams(@params.cParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        ZSTD_CCtxParams_init_internal(cctxParams, &@params, 0);
        return 0;
    }

    /**
     * Sets cctxParams' cParams and fParams from params, but otherwise leaves them alone.
     * @param params Validated zstd parameters.
     */
    private static void ZSTD_CCtxParams_setZstdParams(
        ZSTD_CCtx_params_s* cctxParams,
        ZSTD_parameters* @params
    )
    {
        assert(ZSTD_checkCParams(@params->cParams) == 0);
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
                bounds.lowerBound = ZSTD_minCLevel();
                bounds.upperBound = ZSTD_maxCLevel();
                return bounds;
            case ZSTD_cParameter.ZSTD_c_windowLog:
                bounds.lowerBound = 10;
                bounds.upperBound = sizeof(nuint) == 4 ? 30 : 31;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_hashLog:
                bounds.lowerBound = 6;
                bounds.upperBound =
                    (sizeof(nuint) == 4 ? 30 : 31) < 30
                        ? sizeof(nuint) == 4
                            ? 30
                            : 31
                        : 30;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_chainLog:
                bounds.lowerBound = 6;
                bounds.upperBound = sizeof(nuint) == 4 ? 29 : 30;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_searchLog:
                bounds.lowerBound = 1;
                bounds.upperBound = (sizeof(nuint) == 4 ? 30 : 31) - 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_minMatch:
                bounds.lowerBound = 3;
                bounds.upperBound = 7;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_targetLength:
                bounds.lowerBound = 0;
                bounds.upperBound = 1 << 17;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_strategy:
                bounds.lowerBound = (int)ZSTD_strategy.ZSTD_fast;
                bounds.upperBound = (int)ZSTD_strategy.ZSTD_btultra2;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_checksumFlag:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_nbWorkers:
                bounds.lowerBound = 0;
                bounds.upperBound = sizeof(void*) == 4 ? 64 : 256;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_jobSize:
                bounds.lowerBound = 0;
                bounds.upperBound = MEM_32bits ? 512 * (1 << 20) : 1024 * (1 << 20);
                return bounds;
            case ZSTD_cParameter.ZSTD_c_overlapLog:
                bounds.lowerBound = 0;
                bounds.upperBound = 9;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                bounds.lowerBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_auto;
                bounds.upperBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_disable;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                bounds.lowerBound = 6;
                bounds.upperBound =
                    (sizeof(nuint) == 4 ? 30 : 31) < 30
                        ? sizeof(nuint) == 4
                            ? 30
                            : 31
                        : 30;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                bounds.lowerBound = 4;
                bounds.upperBound = 4096;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                bounds.lowerBound = 1;
                bounds.upperBound = 8;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                bounds.lowerBound = 0;
                bounds.upperBound = (sizeof(nuint) == 4 ? 30 : 31) - 6;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                bounds.lowerBound = (int)ZSTD_format_e.ZSTD_f_zstd1;
                bounds.upperBound = (int)ZSTD_format_e.ZSTD_f_zstd1_magicless;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                bounds.lowerBound = (int)ZSTD_dictAttachPref_e.ZSTD_dictDefaultAttach;
                bounds.upperBound = (int)ZSTD_dictAttachPref_e.ZSTD_dictForceLoad;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                bounds.lowerBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_auto;
                bounds.upperBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_disable;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_targetCBlockSize:
                bounds.lowerBound = 1340;
                bounds.upperBound = 1 << 17;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                bounds.lowerBound = 0;
                bounds.upperBound = 2147483647;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam9:
            case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                bounds.lowerBound = (int)ZSTD_bufferMode_e.ZSTD_bm_buffered;
                bounds.upperBound = (int)ZSTD_bufferMode_e.ZSTD_bm_stable;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                bounds.lowerBound = (int)ZSTD_sequenceFormat_e.ZSTD_sf_noBlockDelimiters;
                bounds.upperBound = (int)ZSTD_sequenceFormat_e.ZSTD_sf_explicitBlockDelimiters;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                bounds.lowerBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_auto;
                bounds.upperBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_disable;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam20:
                bounds.lowerBound = 0;
                bounds.upperBound = 6;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                bounds.lowerBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_auto;
                bounds.upperBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_disable;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam16:
                bounds.lowerBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_auto;
                bounds.upperBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_disable;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam17:
                bounds.lowerBound = 0;
                bounds.upperBound = 1;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam18:
                bounds.lowerBound = 1 << 10;
                bounds.upperBound = 1 << 17;
                return bounds;
            case ZSTD_cParameter.ZSTD_c_experimentalParam19:
                bounds.lowerBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_auto;
                bounds.upperBound = (int)ZSTD_paramSwitch_e.ZSTD_ps_disable;
                return bounds;
            default:
                bounds.error = unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)
                );
                return bounds;
        }
    }

    /* ZSTD_cParam_clampBounds:
     * Clamps the value into the bounded range.
     */
    private static nuint ZSTD_cParam_clampBounds(ZSTD_cParameter cParam, int* value)
    {
        ZSTD_bounds bounds = ZSTD_cParam_getBounds(cParam);
        if (ERR_isError(bounds.error))
            return bounds.error;
        if (*value < bounds.lowerBound)
            *value = bounds.lowerBound;
        if (*value > bounds.upperBound)
            *value = bounds.upperBound;
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
            case ZSTD_cParameter.ZSTD_c_experimentalParam20:
                return 1;
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
            case ZSTD_cParameter.ZSTD_c_targetCBlockSize:
            case ZSTD_cParameter.ZSTD_c_experimentalParam7:
            case ZSTD_cParameter.ZSTD_c_experimentalParam9:
            case ZSTD_cParameter.ZSTD_c_experimentalParam10:
            case ZSTD_cParameter.ZSTD_c_experimentalParam11:
            case ZSTD_cParameter.ZSTD_c_experimentalParam12:
            case ZSTD_cParameter.ZSTD_c_experimentalParam13:
            case ZSTD_cParameter.ZSTD_c_experimentalParam14:
            case ZSTD_cParameter.ZSTD_c_experimentalParam15:
            case ZSTD_cParameter.ZSTD_c_experimentalParam16:
            case ZSTD_cParameter.ZSTD_c_experimentalParam17:
            case ZSTD_cParameter.ZSTD_c_experimentalParam18:
            case ZSTD_cParameter.ZSTD_c_experimentalParam19:
            default:
                return 0;
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
            if (ZSTD_isUpdateAuthorized(param) != 0)
            {
                cctx->cParamsChanged = 1;
            }
            else
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
            }
        }

        switch (param)
        {
            case ZSTD_cParameter.ZSTD_c_nbWorkers:
                if (value != 0 && cctx->staticSize != 0)
                {
                    return unchecked(
                        (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)
                    );
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
            case ZSTD_cParameter.ZSTD_c_targetCBlockSize:
            case ZSTD_cParameter.ZSTD_c_experimentalParam7:
            case ZSTD_cParameter.ZSTD_c_experimentalParam9:
            case ZSTD_cParameter.ZSTD_c_experimentalParam10:
            case ZSTD_cParameter.ZSTD_c_experimentalParam11:
            case ZSTD_cParameter.ZSTD_c_experimentalParam12:
            case ZSTD_cParameter.ZSTD_c_experimentalParam13:
            case ZSTD_cParameter.ZSTD_c_experimentalParam20:
            case ZSTD_cParameter.ZSTD_c_experimentalParam14:
            case ZSTD_cParameter.ZSTD_c_experimentalParam15:
            case ZSTD_cParameter.ZSTD_c_experimentalParam16:
            case ZSTD_cParameter.ZSTD_c_experimentalParam17:
            case ZSTD_cParameter.ZSTD_c_experimentalParam18:
            case ZSTD_cParameter.ZSTD_c_experimentalParam19:
                break;
            default:
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported));
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
    public static nuint ZSTD_CCtxParams_setParameter(
        ZSTD_CCtx_params_s* CCtxParams,
        ZSTD_cParameter param,
        int value
    )
    {
        switch (param)
        {
            case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam2, value) == 0)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->format = (ZSTD_format_e)value;
                return (nuint)CCtxParams->format;
            case ZSTD_cParameter.ZSTD_c_compressionLevel:
            {
                {
                    nuint err_code = ZSTD_cParam_clampBounds(param, &value);
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                if (value == 0)
                    CCtxParams->compressionLevel = 3;
                else
                    CCtxParams->compressionLevel = value;
                if (CCtxParams->compressionLevel >= 0)
                    return (nuint)CCtxParams->compressionLevel;
                return 0;
            }

            case ZSTD_cParameter.ZSTD_c_windowLog:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_windowLog, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->cParams.windowLog = (uint)value;
                return CCtxParams->cParams.windowLog;
            case ZSTD_cParameter.ZSTD_c_hashLog:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_hashLog, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->cParams.hashLog = (uint)value;
                return CCtxParams->cParams.hashLog;
            case ZSTD_cParameter.ZSTD_c_chainLog:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_chainLog, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->cParams.chainLog = (uint)value;
                return CCtxParams->cParams.chainLog;
            case ZSTD_cParameter.ZSTD_c_searchLog:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_searchLog, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->cParams.searchLog = (uint)value;
                return (nuint)value;
            case ZSTD_cParameter.ZSTD_c_minMatch:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_minMatch, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->cParams.minMatch = (uint)value;
                return CCtxParams->cParams.minMatch;
            case ZSTD_cParameter.ZSTD_c_targetLength:
                if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_targetLength, value) == 0)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->cParams.targetLength = (uint)value;
                return CCtxParams->cParams.targetLength;
            case ZSTD_cParameter.ZSTD_c_strategy:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->cParams.strategy = (ZSTD_strategy)value;
                return (nuint)CCtxParams->cParams.strategy;
            case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                CCtxParams->fParams.contentSizeFlag = value != 0 ? 1 : 0;
                return (nuint)CCtxParams->fParams.contentSizeFlag;
            case ZSTD_cParameter.ZSTD_c_checksumFlag:
                CCtxParams->fParams.checksumFlag = value != 0 ? 1 : 0;
                return (nuint)CCtxParams->fParams.checksumFlag;
            case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                CCtxParams->fParams.noDictIDFlag = value == 0 ? 1 : 0;
                return CCtxParams->fParams.noDictIDFlag == 0 ? 1U : 0U;
            case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                CCtxParams->forceWindow = value != 0 ? 1 : 0;
                return (nuint)CCtxParams->forceWindow;
            case ZSTD_cParameter.ZSTD_c_experimentalParam4:
            {
                ZSTD_dictAttachPref_e pref = (ZSTD_dictAttachPref_e)value;
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam4, (int)pref)
                    == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->attachDictPref = pref;
                return (nuint)CCtxParams->attachDictPref;
            }

            case ZSTD_cParameter.ZSTD_c_experimentalParam5:
            {
                ZSTD_paramSwitch_e lcm = (ZSTD_paramSwitch_e)value;
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam5, (int)lcm)
                    == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->literalCompressionMode = lcm;
                return (nuint)CCtxParams->literalCompressionMode;
            }

            case ZSTD_cParameter.ZSTD_c_nbWorkers:
                {
                    nuint err_code = ZSTD_cParam_clampBounds(param, &value);
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                CCtxParams->nbWorkers = value;
                return (nuint)CCtxParams->nbWorkers;
            case ZSTD_cParameter.ZSTD_c_jobSize:
                if (value != 0 && value < 512 * (1 << 10))
                    value = 512 * (1 << 10);

                {
                    nuint err_code = ZSTD_cParam_clampBounds(param, &value);
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                assert(value >= 0);
                CCtxParams->jobSize = (nuint)value;
                return CCtxParams->jobSize;
            case ZSTD_cParameter.ZSTD_c_overlapLog:
                {
                    nuint err_code = ZSTD_cParam_clampBounds(
                        ZSTD_cParameter.ZSTD_c_overlapLog,
                        &value
                    );
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                CCtxParams->overlapLog = value;
                return (nuint)CCtxParams->overlapLog;
            case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                {
                    nuint err_code = ZSTD_cParam_clampBounds(
                        ZSTD_cParameter.ZSTD_c_overlapLog,
                        &value
                    );
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                CCtxParams->rsyncable = value;
                return (nuint)CCtxParams->rsyncable;
            case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                CCtxParams->enableDedicatedDictSearch = value != 0 ? 1 : 0;
                return (nuint)CCtxParams->enableDedicatedDictSearch;
            case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                if (
                    ZSTD_cParam_withinBounds(
                        ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching,
                        value
                    ) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->ldmParams.enableLdm = (ZSTD_paramSwitch_e)value;
                return (nuint)CCtxParams->ldmParams.enableLdm;
            case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmHashLog, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->ldmParams.hashLog = (uint)value;
                return CCtxParams->ldmParams.hashLog;
            case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmMinMatch, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->ldmParams.minMatchLength = (uint)value;
                return CCtxParams->ldmParams.minMatchLength;
            case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                if (value != 0)
                    if (
                        ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog, value)
                        == 0
                    )
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->ldmParams.bucketSizeLog = (uint)value;
                return CCtxParams->ldmParams.bucketSizeLog;
            case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                if (value != 0)
                    if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_ldmHashRateLog, value) == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->ldmParams.hashRateLog = (uint)value;
                return CCtxParams->ldmParams.hashRateLog;
            case ZSTD_cParameter.ZSTD_c_targetCBlockSize:
                if (value != 0)
                {
                    value = value > 1340 ? value : 1340;
                    if (
                        ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_targetCBlockSize, value)
                        == 0
                    )
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }
                }

                CCtxParams->targetCBlockSize = (uint)value;
                return CCtxParams->targetCBlockSize;
            case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                if (value != 0)
                    if (
                        ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam7, value)
                        == 0
                    )
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                CCtxParams->srcSizeHint = value;
                return (nuint)CCtxParams->srcSizeHint;
            case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam9, value) == 0)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->inBufferMode = (ZSTD_bufferMode_e)value;
                return (nuint)CCtxParams->inBufferMode;
            case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam10, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->outBufferMode = (ZSTD_bufferMode_e)value;
                return (nuint)CCtxParams->outBufferMode;
            case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam11, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->blockDelimiters = (ZSTD_sequenceFormat_e)value;
                return (nuint)CCtxParams->blockDelimiters;
            case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam12, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->validateSequences = value;
                return (nuint)CCtxParams->validateSequences;
            case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam13, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->postBlockSplitter = (ZSTD_paramSwitch_e)value;
                return (nuint)CCtxParams->postBlockSplitter;
            case ZSTD_cParameter.ZSTD_c_experimentalParam20:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam20, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->preBlockSplitter_level = value;
                return (nuint)CCtxParams->preBlockSplitter_level;
            case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam14, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->useRowMatchFinder = (ZSTD_paramSwitch_e)value;
                return (nuint)CCtxParams->useRowMatchFinder;
            case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam15, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->deterministicRefPrefix = !(value == 0) ? 1 : 0;
                return (nuint)CCtxParams->deterministicRefPrefix;
            case ZSTD_cParameter.ZSTD_c_experimentalParam16:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam16, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->prefetchCDictTables = (ZSTD_paramSwitch_e)value;
                return (nuint)CCtxParams->prefetchCDictTables;
            case ZSTD_cParameter.ZSTD_c_experimentalParam17:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam17, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->enableMatchFinderFallback = value;
                return (nuint)CCtxParams->enableMatchFinderFallback;
            case ZSTD_cParameter.ZSTD_c_experimentalParam18:
                if (value != 0)
                    if (
                        ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam18, value)
                        == 0
                    )
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)
                        );
                    }

                assert(value >= 0);
                CCtxParams->maxBlockSize = (nuint)value;
                return CCtxParams->maxBlockSize;
            case ZSTD_cParameter.ZSTD_c_experimentalParam19:
                if (
                    ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam19, value) == 0
                )
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
                }

                CCtxParams->searchForExternalRepcodes = (ZSTD_paramSwitch_e)value;
                return (nuint)CCtxParams->searchForExternalRepcodes;
            default:
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported));
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
    public static nuint ZSTD_CCtxParams_getParameter(
        ZSTD_CCtx_params_s* CCtxParams,
        ZSTD_cParameter param,
        int* value
    )
    {
        switch (param)
        {
            case ZSTD_cParameter.ZSTD_c_experimentalParam2:
                *value = (int)CCtxParams->format;
                break;
            case ZSTD_cParameter.ZSTD_c_compressionLevel:
                *value = CCtxParams->compressionLevel;
                break;
            case ZSTD_cParameter.ZSTD_c_windowLog:
                *value = (int)CCtxParams->cParams.windowLog;
                break;
            case ZSTD_cParameter.ZSTD_c_hashLog:
                *value = (int)CCtxParams->cParams.hashLog;
                break;
            case ZSTD_cParameter.ZSTD_c_chainLog:
                *value = (int)CCtxParams->cParams.chainLog;
                break;
            case ZSTD_cParameter.ZSTD_c_searchLog:
                *value = (int)CCtxParams->cParams.searchLog;
                break;
            case ZSTD_cParameter.ZSTD_c_minMatch:
                *value = (int)CCtxParams->cParams.minMatch;
                break;
            case ZSTD_cParameter.ZSTD_c_targetLength:
                *value = (int)CCtxParams->cParams.targetLength;
                break;
            case ZSTD_cParameter.ZSTD_c_strategy:
                *value = (int)CCtxParams->cParams.strategy;
                break;
            case ZSTD_cParameter.ZSTD_c_contentSizeFlag:
                *value = CCtxParams->fParams.contentSizeFlag;
                break;
            case ZSTD_cParameter.ZSTD_c_checksumFlag:
                *value = CCtxParams->fParams.checksumFlag;
                break;
            case ZSTD_cParameter.ZSTD_c_dictIDFlag:
                *value = CCtxParams->fParams.noDictIDFlag == 0 ? 1 : 0;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam3:
                *value = CCtxParams->forceWindow;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam4:
                *value = (int)CCtxParams->attachDictPref;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam5:
                *value = (int)CCtxParams->literalCompressionMode;
                break;
            case ZSTD_cParameter.ZSTD_c_nbWorkers:
                *value = CCtxParams->nbWorkers;
                break;
            case ZSTD_cParameter.ZSTD_c_jobSize:
                assert(CCtxParams->jobSize <= 2147483647);
                *value = (int)CCtxParams->jobSize;
                break;
            case ZSTD_cParameter.ZSTD_c_overlapLog:
                *value = CCtxParams->overlapLog;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam1:
                *value = CCtxParams->rsyncable;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam8:
                *value = CCtxParams->enableDedicatedDictSearch;
                break;
            case ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching:
                *value = (int)CCtxParams->ldmParams.enableLdm;
                break;
            case ZSTD_cParameter.ZSTD_c_ldmHashLog:
                *value = (int)CCtxParams->ldmParams.hashLog;
                break;
            case ZSTD_cParameter.ZSTD_c_ldmMinMatch:
                *value = (int)CCtxParams->ldmParams.minMatchLength;
                break;
            case ZSTD_cParameter.ZSTD_c_ldmBucketSizeLog:
                *value = (int)CCtxParams->ldmParams.bucketSizeLog;
                break;
            case ZSTD_cParameter.ZSTD_c_ldmHashRateLog:
                *value = (int)CCtxParams->ldmParams.hashRateLog;
                break;
            case ZSTD_cParameter.ZSTD_c_targetCBlockSize:
                *value = (int)CCtxParams->targetCBlockSize;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam7:
                *value = CCtxParams->srcSizeHint;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam9:
                *value = (int)CCtxParams->inBufferMode;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam10:
                *value = (int)CCtxParams->outBufferMode;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam11:
                *value = (int)CCtxParams->blockDelimiters;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam12:
                *value = CCtxParams->validateSequences;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam13:
                *value = (int)CCtxParams->postBlockSplitter;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam20:
                *value = CCtxParams->preBlockSplitter_level;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam14:
                *value = (int)CCtxParams->useRowMatchFinder;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam15:
                *value = CCtxParams->deterministicRefPrefix;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam16:
                *value = (int)CCtxParams->prefetchCDictTables;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam17:
                *value = CCtxParams->enableMatchFinderFallback;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam18:
                *value = (int)CCtxParams->maxBlockSize;
                break;
            case ZSTD_cParameter.ZSTD_c_experimentalParam19:
                *value = (int)CCtxParams->searchForExternalRepcodes;
                break;
            default:
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported));
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
    public static nuint ZSTD_CCtx_setParametersUsingCCtxParams(
        ZSTD_CCtx_s* cctx,
        ZSTD_CCtx_params_s* @params
    )
    {
        if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        if (cctx->cdict != null)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        cctx->requestedParams = *@params;
        return 0;
    }

    /*! ZSTD_CCtx_setCParams() :
     *  Set all parameters provided within @p cparams into the working @p cctx.
     *  Note : if modifying parameters during compression (MT mode only),
     *         note that changes to the .windowLog parameter will be ignored.
     * @return 0 on success, or an error code (can be checked with ZSTD_isError()).
     *         On failure, no parameters are updated.
     */
    public static nuint ZSTD_CCtx_setCParams(ZSTD_CCtx_s* cctx, ZSTD_compressionParameters cparams)
    {
        {
            /* only update if all parameters are valid */
            nuint err_code = ZSTD_checkCParams(cparams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_windowLog,
                (int)cparams.windowLog
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_chainLog,
                (int)cparams.chainLog
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_hashLog,
                (int)cparams.hashLog
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_searchLog,
                (int)cparams.searchLog
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_minMatch,
                (int)cparams.minMatch
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_targetLength,
                (int)cparams.targetLength
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_strategy,
                (int)cparams.strategy
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return 0;
    }

    /*! ZSTD_CCtx_setFParams() :
     *  Set all parameters provided within @p fparams into the working @p cctx.
     * @return 0 on success, or an error code (can be checked with ZSTD_isError()).
     */
    public static nuint ZSTD_CCtx_setFParams(ZSTD_CCtx_s* cctx, ZSTD_frameParameters fparams)
    {
        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_contentSizeFlag,
                fparams.contentSizeFlag != 0 ? 1 : 0
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_checksumFlag,
                fparams.checksumFlag != 0 ? 1 : 0
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                cctx,
                ZSTD_cParameter.ZSTD_c_dictIDFlag,
                fparams.noDictIDFlag == 0 ? 1 : 0
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return 0;
    }

    /*! ZSTD_CCtx_setParams() :
     *  Set all parameters provided within @p params into the working @p cctx.
     * @return 0 on success, or an error code (can be checked with ZSTD_isError()).
     */
    public static nuint ZSTD_CCtx_setParams(ZSTD_CCtx_s* cctx, ZSTD_parameters @params)
    {
        {
            /* First check cParams, because we want to update all or none. */
            nuint err_code = ZSTD_checkCParams(@params.cParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            /* Next set fParams, because this could fail if the cctx isn't in init stage. */
            nuint err_code = ZSTD_CCtx_setFParams(cctx, @params.fParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            /* Finally set cParams, which should succeed. */
            nuint err_code = ZSTD_CCtx_setCParams(cctx, @params.cParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

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
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        cctx->pledgedSrcSizePlusOne = pledgedSrcSize + 1;
        return 0;
    }

    /**
     * Initializes the local dictionary using requested parameters.
     * NOTE: Initialization does not employ the pledged src size,
     * because the dictionary may be used for multiple compressions.
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
        dl->cdict = ZSTD_createCDict_advanced2(
            dl->dict,
            dl->dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
            dl->dictContentType,
            &cctx->requestedParams,
            cctx->customMem
        );
        if (dl->cdict == null)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }

        cctx->cdict = dl->cdict;
        return 0;
    }

    /*! ZSTD_CCtx_loadDictionary_advanced() :
     *  Same as ZSTD_CCtx_loadDictionary(), but gives finer control over
     *  how to load the dictionary (by copy ? by reference ?)
     *  and how to interpret it (automatic ? force raw mode ? full mode only ?) */
    public static nuint ZSTD_CCtx_loadDictionary_advanced(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType
    )
    {
        if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        ZSTD_clearAllDicts(cctx);
        if (dict == null || dictSize == 0)
            return 0;
        if (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef)
        {
            cctx->localDict.dict = dict;
        }
        else
        {
            /* copy dictionary content inside CCtx to own its lifetime */
            void* dictBuffer;
            if (cctx->staticSize != 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            }

            dictBuffer = ZSTD_customMalloc(dictSize, cctx->customMem);
            if (dictBuffer == null)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            }

            memcpy(dictBuffer, dict, (uint)dictSize);
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
    public static nuint ZSTD_CCtx_loadDictionary_byReference(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize
    )
    {
        return ZSTD_CCtx_loadDictionary_advanced(
            cctx,
            dict,
            dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
            ZSTD_dictContentType_e.ZSTD_dct_auto
        );
    }

    /*! ZSTD_CCtx_loadDictionary() : Requires v1.4.0+
     *  Create an internal CDict from `dict` buffer.
     *  Decompression will have to use same dictionary.
     * @result : 0, or an error code (which can be tested with ZSTD_isError()).
     *  Special: Loading a NULL (or 0-size) dictionary invalidates previous dictionary,
     *           meaning "return to no-dictionary mode".
     *  Note 1 : Dictionary is sticky, it will be used for all future compressed frames,
     *           until parameters are reset, a new dictionary is loaded, or the dictionary
     *           is explicitly invalidated by loading a NULL dictionary.
     *  Note 2 : Loading a dictionary involves building tables.
     *           It's also a CPU consuming operation, with non-negligible impact on latency.
     *           Tables are dependent on compression parameters, and for this reason,
     *           compression parameters can no longer be changed after loading a dictionary.
     *  Note 3 :`dict` content will be copied internally.
     *           Use experimental ZSTD_CCtx_loadDictionary_byReference() to reference content instead.
     *           In such a case, dictionary buffer must outlive its users.
     *  Note 4 : Use ZSTD_CCtx_loadDictionary_advanced()
     *           to precisely select how dictionary content must be interpreted.
     *  Note 5 : This method does not benefit from LDM (long distance mode).
     *           If you want to employ LDM on some large dictionary content,
     *           prefer employing ZSTD_CCtx_refPrefix() described below.
     */
    public static nuint ZSTD_CCtx_loadDictionary(ZSTD_CCtx_s* cctx, void* dict, nuint dictSize)
    {
        return ZSTD_CCtx_loadDictionary_advanced(
            cctx,
            dict,
            dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy,
            ZSTD_dictContentType_e.ZSTD_dct_auto
        );
    }

    /*! ZSTD_CCtx_refCDict() : Requires v1.4.0+
     *  Reference a prepared dictionary, to be used for all future compressed frames.
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
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        ZSTD_clearAllDicts(cctx);
        cctx->cdict = cdict;
        return 0;
    }

    public static nuint ZSTD_CCtx_refThreadPool(ZSTD_CCtx_s* cctx, void* pool)
    {
        if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
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
     *  This method is compatible with LDM (long distance mode).
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
        return ZSTD_CCtx_refPrefix_advanced(
            cctx,
            prefix,
            prefixSize,
            ZSTD_dictContentType_e.ZSTD_dct_rawContent
        );
    }

    /*! ZSTD_CCtx_refPrefix_advanced() :
     *  Same as ZSTD_CCtx_refPrefix(), but gives finer control over
     *  how to interpret prefix content (automatic ? force raw mode (default) ? full mode only ?) */
    public static nuint ZSTD_CCtx_refPrefix_advanced(
        ZSTD_CCtx_s* cctx,
        void* prefix,
        nuint prefixSize,
        ZSTD_dictContentType_e dictContentType
    )
    {
        if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
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
        if (
            reset == ZSTD_ResetDirective.ZSTD_reset_session_only
            || reset == ZSTD_ResetDirective.ZSTD_reset_session_and_parameters
        )
        {
            cctx->streamStage = ZSTD_cStreamStage.zcss_init;
            cctx->pledgedSrcSizePlusOne = 0;
        }

        if (
            reset == ZSTD_ResetDirective.ZSTD_reset_parameters
            || reset == ZSTD_ResetDirective.ZSTD_reset_session_and_parameters
        )
        {
            if (cctx->streamStage != ZSTD_cStreamStage.zcss_init)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
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
        if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_windowLog, (int)cParams.windowLog) == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_chainLog, (int)cParams.chainLog) == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_hashLog, (int)cParams.hashLog) == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_searchLog, (int)cParams.searchLog) == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_minMatch, (int)cParams.minMatch) == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        if (
            ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_targetLength, (int)cParams.targetLength)
            == 0
        )
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        if (ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, (int)cParams.strategy) == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
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
            if ((int)cParams.windowLog < bounds.lowerBound)
                cParams.windowLog = (uint)bounds.lowerBound;
            else if ((int)cParams.windowLog > bounds.upperBound)
                cParams.windowLog = (uint)bounds.upperBound;
        }

        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_chainLog);
            if ((int)cParams.chainLog < bounds.lowerBound)
                cParams.chainLog = (uint)bounds.lowerBound;
            else if ((int)cParams.chainLog > bounds.upperBound)
                cParams.chainLog = (uint)bounds.upperBound;
        }

        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_hashLog);
            if ((int)cParams.hashLog < bounds.lowerBound)
                cParams.hashLog = (uint)bounds.lowerBound;
            else if ((int)cParams.hashLog > bounds.upperBound)
                cParams.hashLog = (uint)bounds.upperBound;
        }

        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_searchLog);
            if ((int)cParams.searchLog < bounds.lowerBound)
                cParams.searchLog = (uint)bounds.lowerBound;
            else if ((int)cParams.searchLog > bounds.upperBound)
                cParams.searchLog = (uint)bounds.upperBound;
        }

        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_minMatch);
            if ((int)cParams.minMatch < bounds.lowerBound)
                cParams.minMatch = (uint)bounds.lowerBound;
            else if ((int)cParams.minMatch > bounds.upperBound)
                cParams.minMatch = (uint)bounds.upperBound;
        }

        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_targetLength);
            if ((int)cParams.targetLength < bounds.lowerBound)
                cParams.targetLength = (uint)bounds.lowerBound;
            else if ((int)cParams.targetLength > bounds.upperBound)
                cParams.targetLength = (uint)bounds.upperBound;
        }

        {
            ZSTD_bounds bounds = ZSTD_cParam_getBounds(ZSTD_cParameter.ZSTD_c_strategy);
            if ((int)cParams.strategy < bounds.lowerBound)
                cParams.strategy = (ZSTD_strategy)bounds.lowerBound;
            else if ((int)cParams.strategy > bounds.upperBound)
                cParams.strategy = (ZSTD_strategy)bounds.upperBound;
        }

        return cParams;
    }

    /** ZSTD_cycleLog() :
     *  condition for correct operation : hashLog > 1 */
    private static uint ZSTD_cycleLog(uint hashLog, ZSTD_strategy strat)
    {
        uint btScale = (uint)strat >= (uint)ZSTD_strategy.ZSTD_btlazy2 ? 1U : 0U;
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
        ulong maxWindowSize = 1UL << (sizeof(nuint) == 4 ? 30 : 31);
        if (dictSize == 0)
        {
            return windowLog;
        }

        assert(windowLog <= (uint)(sizeof(nuint) == 4 ? 30 : 31));
        assert(srcSize != unchecked(0UL - 1));
        {
            ulong windowSize = 1UL << (int)windowLog;
            ulong dictAndWindowSize = dictSize + windowSize;
            if (windowSize >= dictSize + srcSize)
            {
                return windowLog;
            }
            else if (dictAndWindowSize >= maxWindowSize)
            {
                return (uint)(sizeof(nuint) == 4 ? 30 : 31);
            }
            else
            {
                return ZSTD_highbit32((uint)dictAndWindowSize - 1) + 1;
            }
        }
    }

    /** ZSTD_adjustCParams_internal() :
     *  optimize `cPar` for a specified input (`srcSize` and `dictSize`).
     *  mostly downsize to reduce memory consumption and initialization latency.
     * `srcSize` can be ZSTD_CONTENTSIZE_UNKNOWN when not known.
     * `mode` is the mode for parameter adjustment. See docs for `ZSTD_CParamMode_e`.
     *  note : `srcSize==0` means 0!
     *  condition : cPar is presumed validated (can be checked using ZSTD_checkCParams()). */
    private static ZSTD_compressionParameters ZSTD_adjustCParams_internal(
        ZSTD_compressionParameters cPar,
        ulong srcSize,
        nuint dictSize,
        ZSTD_CParamMode_e mode,
        ZSTD_paramSwitch_e useRowMatchFinder
    )
    {
        /* (1<<9) + 1 */
        const ulong minSrcSize = 513;
        ulong maxWindowResize = 1UL << (sizeof(nuint) == 4 ? 30 : 31) - 1;
        assert(ZSTD_checkCParams(cPar) == 0);
        switch (mode)
        {
            case ZSTD_CParamMode_e.ZSTD_cpm_unknown:
            case ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict:
                break;
            case ZSTD_CParamMode_e.ZSTD_cpm_createCDict:
                if (dictSize != 0 && srcSize == unchecked(0UL - 1))
                    srcSize = minSrcSize;
                break;
            case ZSTD_CParamMode_e.ZSTD_cpm_attachDict:
                dictSize = 0;
                break;
            default:
                assert(0 != 0);
                break;
        }

        if (srcSize <= maxWindowResize && dictSize <= maxWindowResize)
        {
            uint tSize = (uint)(srcSize + dictSize);
            const uint hashSizeMin = 1 << 6;
            uint srcLog = tSize < hashSizeMin ? 6 : ZSTD_highbit32(tSize - 1) + 1;
            if (cPar.windowLog > srcLog)
                cPar.windowLog = srcLog;
        }

        if (srcSize != unchecked(0UL - 1))
        {
            uint dictAndWindowLog = ZSTD_dictAndWindowLog(cPar.windowLog, srcSize, dictSize);
            uint cycleLog = ZSTD_cycleLog(cPar.chainLog, cPar.strategy);
            if (cPar.hashLog > dictAndWindowLog + 1)
                cPar.hashLog = dictAndWindowLog + 1;
            if (cycleLog > dictAndWindowLog)
                cPar.chainLog -= cycleLog - dictAndWindowLog;
        }

        if (cPar.windowLog < 10)
            cPar.windowLog = 10;
        if (
            mode == ZSTD_CParamMode_e.ZSTD_cpm_createCDict
            && ZSTD_CDictIndicesAreTagged(&cPar) != 0
        )
        {
            const uint maxShortCacheHashLog = 32 - 8;
            if (cPar.hashLog > maxShortCacheHashLog)
            {
                cPar.hashLog = maxShortCacheHashLog;
            }

            if (cPar.chainLog > maxShortCacheHashLog)
            {
                cPar.chainLog = maxShortCacheHashLog;
            }
        }

        if (useRowMatchFinder == ZSTD_paramSwitch_e.ZSTD_ps_auto)
            useRowMatchFinder = ZSTD_paramSwitch_e.ZSTD_ps_enable;
        if (ZSTD_rowMatchFinderUsed(cPar.strategy, useRowMatchFinder) != 0)
        {
            /* Switch to 32-entry rows if searchLog is 5 (or more) */
            uint rowLog =
                cPar.searchLog <= 4 ? 4
                : cPar.searchLog <= 6 ? cPar.searchLog
                : 6;
            const uint maxRowHashLog = 32 - 8;
            uint maxHashLog = maxRowHashLog + rowLog;
            assert(cPar.hashLog >= rowLog);
            if (cPar.hashLog > maxHashLog)
            {
                cPar.hashLog = maxHashLog;
            }
        }

        return cPar;
    }

    /*! ZSTD_adjustCParams() :
     *  optimize params for a given `srcSize` and `dictSize`.
     * `srcSize` can be unknown, in which case use ZSTD_CONTENTSIZE_UNKNOWN.
     * `dictSize` must be `0` when there is no dictionary.
     *  cPar can be invalid : all parameters will be clamped within valid range in the @return struct.
     *  This function never fails (wide contract) */
    public static ZSTD_compressionParameters ZSTD_adjustCParams(
        ZSTD_compressionParameters cPar,
        ulong srcSize,
        nuint dictSize
    )
    {
        cPar = ZSTD_clampCParams(cPar);
        if (srcSize == 0)
            srcSize = unchecked(0UL - 1);
        return ZSTD_adjustCParams_internal(
            cPar,
            srcSize,
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_unknown,
            ZSTD_paramSwitch_e.ZSTD_ps_auto
        );
    }

    private static void ZSTD_overrideCParams(
        ZSTD_compressionParameters* cParams,
        ZSTD_compressionParameters* overrides
    )
    {
        if (overrides->windowLog != 0)
            cParams->windowLog = overrides->windowLog;
        if (overrides->hashLog != 0)
            cParams->hashLog = overrides->hashLog;
        if (overrides->chainLog != 0)
            cParams->chainLog = overrides->chainLog;
        if (overrides->searchLog != 0)
            cParams->searchLog = overrides->searchLog;
        if (overrides->minMatch != 0)
            cParams->minMatch = overrides->minMatch;
        if (overrides->targetLength != 0)
            cParams->targetLength = overrides->targetLength;
        if (overrides->strategy != default)
            cParams->strategy = overrides->strategy;
    }

    /* ZSTD_getCParamsFromCCtxParams() :
     * cParams are built depending on compressionLevel, src size hints,
     * LDM and manually set compression parameters.
     * Note: srcSizeHint == 0 means 0!
     */
    private static ZSTD_compressionParameters ZSTD_getCParamsFromCCtxParams(
        ZSTD_CCtx_params_s* CCtxParams,
        ulong srcSizeHint,
        nuint dictSize,
        ZSTD_CParamMode_e mode
    )
    {
        ZSTD_compressionParameters cParams;
        if (srcSizeHint == unchecked(0UL - 1) && CCtxParams->srcSizeHint > 0)
        {
            assert(CCtxParams->srcSizeHint >= 0);
            srcSizeHint = (ulong)CCtxParams->srcSizeHint;
        }

        cParams = ZSTD_getCParams_internal(
            CCtxParams->compressionLevel,
            srcSizeHint,
            dictSize,
            mode
        );
        if (CCtxParams->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
            cParams.windowLog = 27;
        ZSTD_overrideCParams(&cParams, &CCtxParams->cParams);
        assert(ZSTD_checkCParams(cParams) == 0);
        return ZSTD_adjustCParams_internal(
            cParams,
            srcSizeHint,
            dictSize,
            mode,
            CCtxParams->useRowMatchFinder
        );
    }

    private static nuint ZSTD_sizeof_matchState(
        ZSTD_compressionParameters* cParams,
        ZSTD_paramSwitch_e useRowMatchFinder,
        int enableDedicatedDictSearch,
        uint forCCtx
    )
    {
        /* chain table size should be 0 for fast or row-hash strategies */
        nuint chainSize =
            ZSTD_allocateChainTable(
                cParams->strategy,
                useRowMatchFinder,
                enableDedicatedDictSearch != 0 && forCCtx == 0 ? 1U : 0U
            ) != 0
                ? (nuint)1 << (int)cParams->chainLog
                : 0;
        nuint hSize = (nuint)1 << (int)cParams->hashLog;
        uint hashLog3 =
            forCCtx != 0 && cParams->minMatch == 3
                ? 17 < cParams->windowLog
                    ? 17
                    : cParams->windowLog
                : 0;
        nuint h3Size = hashLog3 != 0 ? (nuint)1 << (int)hashLog3 : 0;
        /* We don't use ZSTD_cwksp_alloc_size() here because the tables aren't
         * surrounded by redzones in ASAN. */
        nuint tableSpace = chainSize * sizeof(uint) + hSize * sizeof(uint) + h3Size * sizeof(uint);
        nuint optPotentialSpace =
            ZSTD_cwksp_aligned64_alloc_size((52 + 1) * sizeof(uint))
            + ZSTD_cwksp_aligned64_alloc_size((35 + 1) * sizeof(uint))
            + ZSTD_cwksp_aligned64_alloc_size((31 + 1) * sizeof(uint))
            + ZSTD_cwksp_aligned64_alloc_size((1 << 8) * sizeof(uint))
            + ZSTD_cwksp_aligned64_alloc_size((nuint)(((1 << 12) + 3) * sizeof(ZSTD_match_t)))
            + ZSTD_cwksp_aligned64_alloc_size((nuint)(((1 << 12) + 3) * sizeof(ZSTD_optimal_t)));
        nuint lazyAdditionalSpace =
            ZSTD_rowMatchFinderUsed(cParams->strategy, useRowMatchFinder) != 0
                ? ZSTD_cwksp_aligned64_alloc_size(hSize)
                : 0;
        nuint optSpace =
            forCCtx != 0 && cParams->strategy >= ZSTD_strategy.ZSTD_btopt ? optPotentialSpace : 0;
        nuint slackSpace = ZSTD_cwksp_slack_space_required();
        assert(useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        return tableSpace + optSpace + slackSpace + lazyAdditionalSpace;
    }

    /* Helper function for calculating memory requirements.
     * Gives a tighter bound than ZSTD_sequenceBound() by taking minMatch into account. */
    private static nuint ZSTD_maxNbSeq(nuint blockSize, uint minMatch, int useSequenceProducer)
    {
        uint divider = (uint)(minMatch == 3 || useSequenceProducer != 0 ? 3 : 4);
        return blockSize / divider;
    }

    private static nuint ZSTD_estimateCCtxSize_usingCCtxParams_internal(
        ZSTD_compressionParameters* cParams,
        ldmParams_t* ldmParams,
        int isStatic,
        ZSTD_paramSwitch_e useRowMatchFinder,
        nuint buffInSize,
        nuint buffOutSize,
        ulong pledgedSrcSize,
        int useSequenceProducer,
        nuint maxBlockSize
    )
    {
        nuint windowSize = (nuint)(
            1UL << (int)cParams->windowLog <= 1UL ? 1UL
            : 1UL << (int)cParams->windowLog <= pledgedSrcSize ? 1UL << (int)cParams->windowLog
            : pledgedSrcSize
        );
        nuint blockSize =
            ZSTD_resolveMaxBlockSize(maxBlockSize) < windowSize
                ? ZSTD_resolveMaxBlockSize(maxBlockSize)
                : windowSize;
        nuint maxNbSeq = ZSTD_maxNbSeq(blockSize, cParams->minMatch, useSequenceProducer);
        nuint tokenSpace =
            ZSTD_cwksp_alloc_size(32 + blockSize)
            + ZSTD_cwksp_aligned64_alloc_size(maxNbSeq * (nuint)sizeof(SeqDef_s))
            + 3 * ZSTD_cwksp_alloc_size(maxNbSeq * sizeof(byte));
        nuint tmpWorkSpace = ZSTD_cwksp_alloc_size(
            (8 << 10) + 512 + sizeof(uint) * (52 + 2) > 8208
                ? (8 << 10) + 512 + sizeof(uint) * (52 + 2)
                : 8208
        );
        nuint blockStateSpace =
            2 * ZSTD_cwksp_alloc_size((nuint)sizeof(ZSTD_compressedBlockState_t));
        /* enableDedicatedDictSearch */
        nuint matchStateSize = ZSTD_sizeof_matchState(cParams, useRowMatchFinder, 0, 1);
        nuint ldmSpace = ZSTD_ldm_getTableSize(*ldmParams);
        nuint maxNbLdmSeq = ZSTD_ldm_getMaxNbSeq(*ldmParams, blockSize);
        nuint ldmSeqSpace =
            ldmParams->enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable
                ? ZSTD_cwksp_aligned64_alloc_size(maxNbLdmSeq * (nuint)sizeof(rawSeq))
                : 0;
        nuint bufferSpace = ZSTD_cwksp_alloc_size(buffInSize) + ZSTD_cwksp_alloc_size(buffOutSize);
        nuint cctxSpace = isStatic != 0 ? ZSTD_cwksp_alloc_size((nuint)sizeof(ZSTD_CCtx_s)) : 0;
        nuint maxNbExternalSeq = ZSTD_sequenceBound(blockSize);
        nuint externalSeqSpace =
            useSequenceProducer != 0
                ? ZSTD_cwksp_aligned64_alloc_size(maxNbExternalSeq * (nuint)sizeof(ZSTD_Sequence))
                : 0;
        nuint neededSpace =
            cctxSpace
            + tmpWorkSpace
            + blockStateSpace
            + ldmSpace
            + ldmSeqSpace
            + matchStateSize
            + tokenSpace
            + bufferSpace
            + externalSeqSpace;
        return neededSpace;
    }

    public static nuint ZSTD_estimateCCtxSize_usingCCtxParams(ZSTD_CCtx_params_s* @params)
    {
        ZSTD_compressionParameters cParams = ZSTD_getCParamsFromCCtxParams(
            @params,
            unchecked(0UL - 1),
            0,
            ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
        );
        ZSTD_paramSwitch_e useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
            @params->useRowMatchFinder,
            &cParams
        );
        if (@params->nbWorkers > 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }

        return ZSTD_estimateCCtxSize_usingCCtxParams_internal(
            &cParams,
            &@params->ldmParams,
            1,
            useRowMatchFinder,
            0,
            0,
            unchecked(0UL - 1),
            ZSTD_hasExtSeqProd(@params),
            @params->maxBlockSize
        );
    }

    public static nuint ZSTD_estimateCCtxSize_usingCParams(ZSTD_compressionParameters cParams)
    {
        ZSTD_CCtx_params_s initialParams = ZSTD_makeCCtxParamsFromCParams(cParams);
        if (ZSTD_rowMatchFinderSupported(cParams.strategy) != 0)
        {
            /* Pick bigger of not using and using row-based matchfinder for greedy and lazy strategies */
            nuint noRowCCtxSize;
            nuint rowCCtxSize;
            initialParams.useRowMatchFinder = ZSTD_paramSwitch_e.ZSTD_ps_disable;
            noRowCCtxSize = ZSTD_estimateCCtxSize_usingCCtxParams(&initialParams);
            initialParams.useRowMatchFinder = ZSTD_paramSwitch_e.ZSTD_ps_enable;
            rowCCtxSize = ZSTD_estimateCCtxSize_usingCCtxParams(&initialParams);
            return noRowCCtxSize > rowCCtxSize ? noRowCCtxSize : rowCCtxSize;
        }
        else
        {
            return ZSTD_estimateCCtxSize_usingCCtxParams(&initialParams);
        }
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<ulong> Span_srcSizeTiers =>
        new ulong[4] { 16 * (1 << 10), 128 * (1 << 10), 256 * (1 << 10), unchecked(0UL - 1) };
    private static ulong* srcSizeTiers =>
        (ulong*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_srcSizeTiers)
            );
#else

    private static readonly ulong* srcSizeTiers = GetArrayPointer(
        new ulong[4]
        {
            (ulong)(16 * (1 << 10)),
            (ulong)(128 * (1 << 10)),
            (ulong)(256 * (1 << 10)),
            (unchecked(0UL - 1)),
        }
    );
#endif

    private static nuint ZSTD_estimateCCtxSize_internal(int compressionLevel)
    {
        int tier = 0;
        nuint largestSize = 0;
        for (; tier < 4; ++tier)
        {
            /* Choose the set of cParams for a given level across all srcSizes that give the largest cctxSize */
            ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
                compressionLevel,
                srcSizeTiers[tier],
                0,
                ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
            );
            largestSize =
                ZSTD_estimateCCtxSize_usingCParams(cParams) > largestSize
                    ? ZSTD_estimateCCtxSize_usingCParams(cParams)
                    : largestSize;
        }

        return largestSize;
    }

    /*! ZSTD_estimate*() :
     *  These functions make it possible to estimate memory usage
     *  of a future {D,C}Ctx, before its creation.
     *  This is useful in combination with ZSTD_initStatic(),
     *  which makes it possible to employ a static buffer for ZSTD_CCtx* state.
     *
     *  ZSTD_estimateCCtxSize() will provide a memory budget large enough
     *  to compress data of any size using one-shot compression ZSTD_compressCCtx() or ZSTD_compress2()
     *  associated with any compression level up to max specified one.
     *  The estimate will assume the input may be arbitrarily large,
     *  which is the worst case.
     *
     *  Note that the size estimation is specific for one-shot compression,
     *  it is not valid for streaming (see ZSTD_estimateCStreamSize*())
     *  nor other potential ways of using a ZSTD_CCtx* state.
     *
     *  When srcSize can be bound by a known and rather "small" value,
     *  this knowledge can be used to provide a tighter budget estimation
     *  because the ZSTD_CCtx* state will need less memory for small inputs.
     *  This tighter estimation can be provided by employing more advanced functions
     *  ZSTD_estimateCCtxSize_usingCParams(), which can be used in tandem with ZSTD_getCParams(),
     *  and ZSTD_estimateCCtxSize_usingCCtxParams(), which can be used in tandem with ZSTD_CCtxParams_setParameter().
     *  Both can be used to estimate memory using custom compression parameters and arbitrary srcSize limits.
     *
     *  Note : only single-threaded compression is supported.
     *  ZSTD_estimateCCtxSize_usingCCtxParams() will return an error code if ZSTD_c_nbWorkers is >= 1.
     */
    public static nuint ZSTD_estimateCCtxSize(int compressionLevel)
    {
        int level;
        nuint memBudget = 0;
        for (
            level = compressionLevel < 1 ? compressionLevel : 1;
            level <= compressionLevel;
            level++
        )
        {
            /* Ensure monotonically increasing memory usage as compression level increases */
            nuint newMB = ZSTD_estimateCCtxSize_internal(level);
            if (newMB > memBudget)
                memBudget = newMB;
        }

        return memBudget;
    }

    public static nuint ZSTD_estimateCStreamSize_usingCCtxParams(ZSTD_CCtx_params_s* @params)
    {
        if (@params->nbWorkers > 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }

        {
            ZSTD_compressionParameters cParams = ZSTD_getCParamsFromCCtxParams(
                @params,
                unchecked(0UL - 1),
                0,
                ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
            );
            nuint blockSize =
                ZSTD_resolveMaxBlockSize(@params->maxBlockSize) < (nuint)1 << (int)cParams.windowLog
                    ? ZSTD_resolveMaxBlockSize(@params->maxBlockSize)
                    : (nuint)1 << (int)cParams.windowLog;
            nuint inBuffSize =
                @params->inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered
                    ? ((nuint)1 << (int)cParams.windowLog) + blockSize
                    : 0;
            nuint outBuffSize =
                @params->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered
                    ? ZSTD_compressBound(blockSize) + 1
                    : 0;
            ZSTD_paramSwitch_e useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
                @params->useRowMatchFinder,
                &@params->cParams
            );
            return ZSTD_estimateCCtxSize_usingCCtxParams_internal(
                &cParams,
                &@params->ldmParams,
                1,
                useRowMatchFinder,
                inBuffSize,
                outBuffSize,
                unchecked(0UL - 1),
                ZSTD_hasExtSeqProd(@params),
                @params->maxBlockSize
            );
        }
    }

    public static nuint ZSTD_estimateCStreamSize_usingCParams(ZSTD_compressionParameters cParams)
    {
        ZSTD_CCtx_params_s initialParams = ZSTD_makeCCtxParamsFromCParams(cParams);
        if (ZSTD_rowMatchFinderSupported(cParams.strategy) != 0)
        {
            /* Pick bigger of not using and using row-based matchfinder for greedy and lazy strategies */
            nuint noRowCCtxSize;
            nuint rowCCtxSize;
            initialParams.useRowMatchFinder = ZSTD_paramSwitch_e.ZSTD_ps_disable;
            noRowCCtxSize = ZSTD_estimateCStreamSize_usingCCtxParams(&initialParams);
            initialParams.useRowMatchFinder = ZSTD_paramSwitch_e.ZSTD_ps_enable;
            rowCCtxSize = ZSTD_estimateCStreamSize_usingCCtxParams(&initialParams);
            return noRowCCtxSize > rowCCtxSize ? noRowCCtxSize : rowCCtxSize;
        }
        else
        {
            return ZSTD_estimateCStreamSize_usingCCtxParams(&initialParams);
        }
    }

    private static nuint ZSTD_estimateCStreamSize_internal(int compressionLevel)
    {
        ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
            compressionLevel,
            unchecked(0UL - 1),
            0,
            ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
        );
        return ZSTD_estimateCStreamSize_usingCParams(cParams);
    }

    /*! ZSTD_estimateCStreamSize() :
     *  ZSTD_estimateCStreamSize() will provide a memory budget large enough for streaming compression
     *  using any compression level up to the max specified one.
     *  It will also consider src size to be arbitrarily "large", which is a worst case scenario.
     *  If srcSize is known to always be small, ZSTD_estimateCStreamSize_usingCParams() can provide a tighter estimation.
     *  ZSTD_estimateCStreamSize_usingCParams() can be used in tandem with ZSTD_getCParams() to create cParams from compressionLevel.
     *  ZSTD_estimateCStreamSize_usingCCtxParams() can be used in tandem with ZSTD_CCtxParams_setParameter(). Only single-threaded compression is supported. This function will return an error code if ZSTD_c_nbWorkers is >= 1.
     *  Note : CStream size estimation is only correct for single-threaded compression.
     *  ZSTD_estimateCStreamSize_usingCCtxParams() will return an error code if ZSTD_c_nbWorkers is >= 1.
     *  Note 2 : ZSTD_estimateCStreamSize* functions are not compatible with the Block-Level Sequence Producer API at this time.
     *  Size estimates assume that no external sequence producer is registered.
     *
     *  ZSTD_DStream memory budget depends on frame's window Size.
     *  This information can be passed manually, using ZSTD_estimateDStreamSize,
     *  or deducted from a valid frame Header, using ZSTD_estimateDStreamSize_fromFrame();
     *  Any frame requesting a window size larger than max specified one will be rejected.
     *  Note : if streaming is init with function ZSTD_init?Stream_usingDict(),
     *         an internal ?Dict will be created, which additional size is not estimated here.
     *         In this case, get total size by adding ZSTD_estimate?DictSize
     */
    public static nuint ZSTD_estimateCStreamSize(int compressionLevel)
    {
        int level;
        nuint memBudget = 0;
        for (
            level = compressionLevel < 1 ? compressionLevel : 1;
            level <= compressionLevel;
            level++
        )
        {
            nuint newMB = ZSTD_estimateCStreamSize_internal(level);
            if (newMB > memBudget)
                memBudget = newMB;
        }

        return memBudget;
    }

    /* ZSTD_getFrameProgression():
     * tells how much data has been consumed (input) and produced (output) for current frame.
     * able to count progression inside worker threads (non-blocking mode).
     */
    public static ZSTD_frameProgression ZSTD_getFrameProgression(ZSTD_CCtx_s* cctx)
    {
        if (cctx->appliedParams.nbWorkers > 0)
        {
            return ZSTDMT_getFrameProgression(cctx->mtctx);
        }

        {
            ZSTD_frameProgression fp;
            nuint buffered = cctx->inBuff == null ? 0 : cctx->inBuffPos - cctx->inToCompress;
#if DEBUG
            if (buffered != 0)
                assert(cctx->inBuffPos >= cctx->inToCompress);
#endif
            assert(buffered <= 1 << 17);
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
        if (cctx->appliedParams.nbWorkers > 0)
        {
            return ZSTDMT_toFlushNow(cctx->mtctx);
        }

        return 0;
    }

    [Conditional("DEBUG")]
    private static void ZSTD_assertEqualCParams(
        ZSTD_compressionParameters cParams1,
        ZSTD_compressionParameters cParams2
    )
    {
        assert(cParams1.windowLog == cParams2.windowLog);
        assert(cParams1.chainLog == cParams2.chainLog);
        assert(cParams1.hashLog == cParams2.hashLog);
        assert(cParams1.searchLog == cParams2.searchLog);
        assert(cParams1.minMatch == cParams2.minMatch);
        assert(cParams1.targetLength == cParams2.targetLength);
        assert(cParams1.strategy == cParams2.strategy);
    }

    private static void ZSTD_reset_compressedBlockState(ZSTD_compressedBlockState_t* bs)
    {
        int i;
        for (i = 0; i < 3; ++i)
            bs->rep[i] = repStartValue[i];
        bs->entropy.huf.repeatMode = HUF_repeat.HUF_repeat_none;
        bs->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_none;
        bs->entropy.fse.matchlength_repeatMode = FSE_repeat.FSE_repeat_none;
        bs->entropy.fse.litlength_repeatMode = FSE_repeat.FSE_repeat_none;
    }

    /*! ZSTD_invalidateMatchState()
     *  Invalidate all the matches in the match finder tables.
     *  Requires nextSrc and base to be set (can be NULL).
     */
    private static void ZSTD_invalidateMatchState(ZSTD_MatchState_t* ms)
    {
        ZSTD_window_clear(&ms->window);
        ms->nextToUpdate = ms->window.dictLimit;
        ms->loadedDictEnd = 0;
        ms->opt.litLengthSum = 0;
        ms->dictMatchState = null;
    }

    /* Mixes bits in a 64 bits in a value, based on XXH3_rrmxmx */
    private static ulong ZSTD_bitmix(ulong val, ulong len)
    {
        val ^= BitOperations.RotateRight(val, 49) ^ BitOperations.RotateRight(val, 24);
        val *= 0x9FB21C651E98DF25UL;
        val ^= (val >> 35) + len;
        val *= 0x9FB21C651E98DF25UL;
        return val ^ val >> 28;
    }

    /* Mixes in the hashSalt and hashSaltEntropy to create a new hashSalt */
    private static void ZSTD_advanceHashSalt(ZSTD_MatchState_t* ms)
    {
        ms->hashSalt = ZSTD_bitmix(ms->hashSalt, 8) ^ ZSTD_bitmix(ms->hashSaltEntropy, 4);
    }

    private static nuint ZSTD_reset_matchState(
        ZSTD_MatchState_t* ms,
        ZSTD_cwksp* ws,
        ZSTD_compressionParameters* cParams,
        ZSTD_paramSwitch_e useRowMatchFinder,
        ZSTD_compResetPolicy_e crp,
        ZSTD_indexResetPolicy_e forceResetIndex,
        ZSTD_resetTarget_e forWho
    )
    {
        /* disable chain table allocation for fast or row-based strategies */
        nuint chainSize =
            ZSTD_allocateChainTable(
                cParams->strategy,
                useRowMatchFinder,
                ms->dedicatedDictSearch != 0 && forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CDict
                    ? 1U
                    : 0U
            ) != 0
                ? (nuint)1 << (int)cParams->chainLog
                : 0;
        nuint hSize = (nuint)1 << (int)cParams->hashLog;
        uint hashLog3 =
            forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx && cParams->minMatch == 3
                ? 17 < cParams->windowLog
                    ? 17
                    : cParams->windowLog
                : 0;
        nuint h3Size = hashLog3 != 0 ? (nuint)1 << (int)hashLog3 : 0;
        assert(useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        if (forceResetIndex == ZSTD_indexResetPolicy_e.ZSTDirp_reset)
        {
            ZSTD_window_init(&ms->window);
            ZSTD_cwksp_mark_tables_dirty(ws);
        }

        ms->hashLog3 = hashLog3;
        ms->lazySkipping = 0;
        ZSTD_invalidateMatchState(ms);
        assert(ZSTD_cwksp_reserve_failed(ws) == 0);
        ZSTD_cwksp_clear_tables(ws);
        ms->hashTable = (uint*)ZSTD_cwksp_reserve_table(ws, hSize * sizeof(uint));
        ms->chainTable = (uint*)ZSTD_cwksp_reserve_table(ws, chainSize * sizeof(uint));
        ms->hashTable3 = (uint*)ZSTD_cwksp_reserve_table(ws, h3Size * sizeof(uint));
        if (ZSTD_cwksp_reserve_failed(ws) != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }

        if (crp != ZSTD_compResetPolicy_e.ZSTDcrp_leaveDirty)
        {
            ZSTD_cwksp_clean_tables(ws);
        }

        if (ZSTD_rowMatchFinderUsed(cParams->strategy, useRowMatchFinder) != 0)
        {
            /* Row match finder needs an additional table of hashes ("tags") */
            nuint tagTableSize = hSize;
            if (forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx)
            {
                ms->tagTable = (byte*)ZSTD_cwksp_reserve_aligned_init_once(ws, tagTableSize);
                ZSTD_advanceHashSalt(ms);
            }
            else
            {
                ms->tagTable = (byte*)ZSTD_cwksp_reserve_aligned64(ws, tagTableSize);
                memset(ms->tagTable, 0, (uint)tagTableSize);
                ms->hashSalt = 0;
            }

            {
                uint rowLog =
                    cParams->searchLog <= 4 ? 4
                    : cParams->searchLog <= 6 ? cParams->searchLog
                    : 6;
                assert(cParams->hashLog >= rowLog);
                ms->rowHashLog = cParams->hashLog - rowLog;
            }
        }

        if (
            forWho == ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx
            && cParams->strategy >= ZSTD_strategy.ZSTD_btopt
        )
        {
            ms->opt.litFreq = (uint*)ZSTD_cwksp_reserve_aligned64(ws, (1 << 8) * sizeof(uint));
            ms->opt.litLengthFreq = (uint*)ZSTD_cwksp_reserve_aligned64(
                ws,
                (35 + 1) * sizeof(uint)
            );
            ms->opt.matchLengthFreq = (uint*)ZSTD_cwksp_reserve_aligned64(
                ws,
                (52 + 1) * sizeof(uint)
            );
            ms->opt.offCodeFreq = (uint*)ZSTD_cwksp_reserve_aligned64(ws, (31 + 1) * sizeof(uint));
            ms->opt.matchTable = (ZSTD_match_t*)ZSTD_cwksp_reserve_aligned64(
                ws,
                (nuint)(((1 << 12) + 3) * sizeof(ZSTD_match_t))
            );
            ms->opt.priceTable = (ZSTD_optimal_t*)ZSTD_cwksp_reserve_aligned64(
                ws,
                (nuint)(((1 << 12) + 3) * sizeof(ZSTD_optimal_t))
            );
        }

        ms->cParams = *cParams;
        if (ZSTD_cwksp_reserve_failed(ws) != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }

        return 0;
    }

    private static int ZSTD_indexTooCloseToMax(ZSTD_window_t w)
    {
        return
            (nuint)(w.nextSrc - w.@base)
            > (MEM_64bits ? 3500U * (1 << 20) : 2000U * (1 << 20)) - 16 * (1 << 20)
            ? 1
            : 0;
    }

    /** ZSTD_dictTooBig():
     * When dictionaries are larger than ZSTD_CHUNKSIZE_MAX they can't be loaded in
     * one go generically. So we ensure that in that case we reset the tables to zero,
     * so that we can load as much of the dictionary as possible.
     */
    private static int ZSTD_dictTooBig(nuint loadedDictSize)
    {
        return
            loadedDictSize
            > unchecked((uint)-1) - (MEM_64bits ? 3500U * (1 << 20) : 2000U * (1 << 20))
            ? 1
            : 0;
    }

    /*! ZSTD_resetCCtx_internal() :
     * @param loadedDictSize The size of the dictionary to be loaded
     * into the context, if any. If no dictionary is used, or the
     * dictionary is being attached / copied, then pass 0.
     * note : `params` are assumed fully validated at this stage.
     */
    private static nuint ZSTD_resetCCtx_internal(
        ZSTD_CCtx_s* zc,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize,
        nuint loadedDictSize,
        ZSTD_compResetPolicy_e crp,
        ZSTD_buffered_policy_e zbuff
    )
    {
        ZSTD_cwksp* ws = &zc->workspace;
        assert(!ERR_isError(ZSTD_checkCParams(@params->cParams)));
        zc->isFirstBlock = 1;
        zc->appliedParams = *@params;
        @params = &zc->appliedParams;
        assert(@params->useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        assert(@params->postBlockSplitter != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        assert(@params->ldmParams.enableLdm != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        assert(@params->maxBlockSize != 0);
        if (@params->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            ZSTD_ldm_adjustParameters(&zc->appliedParams.ldmParams, &@params->cParams);
            assert(@params->ldmParams.hashLog >= @params->ldmParams.bucketSizeLog);
            assert(@params->ldmParams.hashRateLog < 32);
        }

        {
            nuint windowSize =
                1
                > (nuint)(
                    (ulong)1 << (int)@params->cParams.windowLog < pledgedSrcSize
                        ? (ulong)1 << (int)@params->cParams.windowLog
                        : pledgedSrcSize
                )
                    ? 1
                    : (nuint)(
                        (ulong)1 << (int)@params->cParams.windowLog < pledgedSrcSize
                            ? (ulong)1 << (int)@params->cParams.windowLog
                            : pledgedSrcSize
                    );
            nuint blockSize =
                @params->maxBlockSize < windowSize ? @params->maxBlockSize : windowSize;
            nuint maxNbSeq = ZSTD_maxNbSeq(
                blockSize,
                @params->cParams.minMatch,
                ZSTD_hasExtSeqProd(@params)
            );
            nuint buffOutSize =
                zbuff == ZSTD_buffered_policy_e.ZSTDb_buffered
                && @params->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered
                    ? ZSTD_compressBound(blockSize) + 1
                    : 0;
            nuint buffInSize =
                zbuff == ZSTD_buffered_policy_e.ZSTDb_buffered
                && @params->inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered
                    ? windowSize + blockSize
                    : 0;
            nuint maxNbLdmSeq = ZSTD_ldm_getMaxNbSeq(@params->ldmParams, blockSize);
            int indexTooClose = ZSTD_indexTooCloseToMax(zc->blockState.matchState.window);
            int dictTooBig = ZSTD_dictTooBig(loadedDictSize);
            ZSTD_indexResetPolicy_e needsIndexReset =
                indexTooClose != 0 || dictTooBig != 0 || zc->initialized == 0
                    ? ZSTD_indexResetPolicy_e.ZSTDirp_reset
                    : ZSTD_indexResetPolicy_e.ZSTDirp_continue;
            nuint neededSpace = ZSTD_estimateCCtxSize_usingCCtxParams_internal(
                &@params->cParams,
                &@params->ldmParams,
                zc->staticSize != 0 ? 1 : 0,
                @params->useRowMatchFinder,
                buffInSize,
                buffOutSize,
                pledgedSrcSize,
                ZSTD_hasExtSeqProd(@params),
                @params->maxBlockSize
            );
            {
                nuint err_code = neededSpace;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (zc->staticSize == 0)
                ZSTD_cwksp_bump_oversized_duration(ws, 0);
            {
                int workspaceTooSmall = ZSTD_cwksp_sizeof(ws) < neededSpace ? 1 : 0;
                int workspaceWasteful = ZSTD_cwksp_check_wasteful(ws, neededSpace);
                int resizeWorkspace = workspaceTooSmall != 0 || workspaceWasteful != 0 ? 1 : 0;
                if (resizeWorkspace != 0)
                {
                    if (zc->staticSize != 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)
                        );
                    }

                    needsIndexReset = ZSTD_indexResetPolicy_e.ZSTDirp_reset;
                    ZSTD_cwksp_free(ws, zc->customMem);
                    {
                        nuint err_code = ZSTD_cwksp_create(ws, neededSpace, zc->customMem);
                        if (ERR_isError(err_code))
                        {
                            return err_code;
                        }
                    }

                    assert(
                        ZSTD_cwksp_check_available(
                            ws,
                            (nuint)(2 * sizeof(ZSTD_compressedBlockState_t))
                        ) != 0
                    );
                    zc->blockState.prevCBlock =
                        (ZSTD_compressedBlockState_t*)ZSTD_cwksp_reserve_object(
                            ws,
                            (nuint)sizeof(ZSTD_compressedBlockState_t)
                        );
                    if (zc->blockState.prevCBlock == null)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)
                        );
                    }

                    zc->blockState.nextCBlock =
                        (ZSTD_compressedBlockState_t*)ZSTD_cwksp_reserve_object(
                            ws,
                            (nuint)sizeof(ZSTD_compressedBlockState_t)
                        );
                    if (zc->blockState.nextCBlock == null)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)
                        );
                    }

                    zc->tmpWorkspace = ZSTD_cwksp_reserve_object(
                        ws,
                        (8 << 10) + 512 + sizeof(uint) * (52 + 2) > 8208
                            ? (8 << 10) + 512 + sizeof(uint) * (52 + 2)
                            : 8208
                    );
                    if (zc->tmpWorkspace == null)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)
                        );
                    }

                    zc->tmpWkspSize =
                        (8 << 10) + 512 + sizeof(uint) * (52 + 2) > 8208
                            ? (8 << 10) + 512 + sizeof(uint) * (52 + 2)
                            : 8208;
                }
            }

            ZSTD_cwksp_clear(ws);
            zc->blockState.matchState.cParams = @params->cParams;
            zc->blockState.matchState.prefetchCDictTables =
                @params->prefetchCDictTables == ZSTD_paramSwitch_e.ZSTD_ps_enable ? 1 : 0;
            zc->pledgedSrcSizePlusOne = pledgedSrcSize + 1;
            zc->consumedSrcSize = 0;
            zc->producedCSize = 0;
            if (pledgedSrcSize == unchecked(0UL - 1))
                zc->appliedParams.fParams.contentSizeFlag = 0;
            zc->blockSizeMax = blockSize;
            ZSTD_XXH64_reset(&zc->xxhState, 0);
            zc->stage = ZSTD_compressionStage_e.ZSTDcs_init;
            zc->dictID = 0;
            zc->dictContentSize = 0;
            ZSTD_reset_compressedBlockState(zc->blockState.prevCBlock);
            {
                nuint err_code = ZSTD_reset_matchState(
                    &zc->blockState.matchState,
                    ws,
                    &@params->cParams,
                    @params->useRowMatchFinder,
                    crp,
                    needsIndexReset,
                    ZSTD_resetTarget_e.ZSTD_resetTarget_CCtx
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            zc->seqStore.sequencesStart = (SeqDef_s*)ZSTD_cwksp_reserve_aligned64(
                ws,
                maxNbSeq * (nuint)sizeof(SeqDef_s)
            );
            if (@params->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
            {
                /* TODO: avoid memset? */
                nuint ldmHSize = (nuint)1 << (int)@params->ldmParams.hashLog;
                zc->ldmState.hashTable = (ldmEntry_t*)ZSTD_cwksp_reserve_aligned64(
                    ws,
                    ldmHSize * (nuint)sizeof(ldmEntry_t)
                );
                memset(zc->ldmState.hashTable, 0, (uint)(ldmHSize * (nuint)sizeof(ldmEntry_t)));
                zc->ldmSequences = (rawSeq*)ZSTD_cwksp_reserve_aligned64(
                    ws,
                    maxNbLdmSeq * (nuint)sizeof(rawSeq)
                );
                zc->maxNbLdmSequences = maxNbLdmSeq;
                ZSTD_window_init(&zc->ldmState.window);
                zc->ldmState.loadedDictEnd = 0;
            }

            if (ZSTD_hasExtSeqProd(@params) != 0)
            {
                nuint maxNbExternalSeq = ZSTD_sequenceBound(blockSize);
                zc->extSeqBufCapacity = maxNbExternalSeq;
                zc->extSeqBuf = (ZSTD_Sequence*)ZSTD_cwksp_reserve_aligned64(
                    ws,
                    maxNbExternalSeq * (nuint)sizeof(ZSTD_Sequence)
                );
            }

            zc->seqStore.litStart = ZSTD_cwksp_reserve_buffer(ws, blockSize + 32);
            zc->seqStore.maxNbLit = blockSize;
            zc->bufferedPolicy = zbuff;
            zc->inBuffSize = buffInSize;
            zc->inBuff = (sbyte*)ZSTD_cwksp_reserve_buffer(ws, buffInSize);
            zc->outBuffSize = buffOutSize;
            zc->outBuff = (sbyte*)ZSTD_cwksp_reserve_buffer(ws, buffOutSize);
            if (@params->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
            {
                /* TODO: avoid memset? */
                nuint numBuckets =
                    (nuint)1
                    << (int)(@params->ldmParams.hashLog - @params->ldmParams.bucketSizeLog);
                zc->ldmState.bucketOffsets = ZSTD_cwksp_reserve_buffer(ws, numBuckets);
                memset(zc->ldmState.bucketOffsets, 0, (uint)numBuckets);
            }

            ZSTD_referenceExternalSequences(zc, null, 0);
            zc->seqStore.maxNbSeq = maxNbSeq;
            zc->seqStore.llCode = ZSTD_cwksp_reserve_buffer(ws, maxNbSeq * sizeof(byte));
            zc->seqStore.mlCode = ZSTD_cwksp_reserve_buffer(ws, maxNbSeq * sizeof(byte));
            zc->seqStore.ofCode = ZSTD_cwksp_reserve_buffer(ws, maxNbSeq * sizeof(byte));
            assert(ZSTD_cwksp_estimated_space_within_bounds(ws, neededSpace) != 0);
            zc->initialized = 1;
            return 0;
        }
    }

    /* ZSTD_invalidateRepCodes() :
     * ensures next compression will not use repcodes from previous block.
     * Note : only works with regular variant;
     *        do not use with extDict variant ! */
    private static void ZSTD_invalidateRepCodes(ZSTD_CCtx_s* cctx)
    {
        int i;
        for (i = 0; i < 3; i++)
            cctx->blockState.prevCBlock->rep[i] = 0;
        assert(ZSTD_window_hasExtDict(cctx->blockState.matchState.window) == 0);
    }

    private static readonly nuint* attachDictSizeCutoffs = GetArrayPointer(
        new nuint[10]
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
        }
    );

    private static int ZSTD_shouldAttachDict(
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize
    )
    {
        nuint cutoff = attachDictSizeCutoffs[(int)cdict->matchState.cParams.strategy];
        int dedicatedDictSearch = cdict->matchState.dedicatedDictSearch;
        return
            dedicatedDictSearch != 0
            || (
                pledgedSrcSize <= cutoff
                || pledgedSrcSize == unchecked(0UL - 1)
                || @params->attachDictPref == ZSTD_dictAttachPref_e.ZSTD_dictForceAttach
            )
                && @params->attachDictPref != ZSTD_dictAttachPref_e.ZSTD_dictForceCopy
                && @params->forceWindow == 0
            ? 1
            : 0;
    }

    private static nuint ZSTD_resetCCtx_byAttachingCDict(
        ZSTD_CCtx_s* cctx,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s @params,
        ulong pledgedSrcSize,
        ZSTD_buffered_policy_e zbuff
    )
    {
        {
            ZSTD_compressionParameters adjusted_cdict_cParams = cdict->matchState.cParams;
            uint windowLog = @params.cParams.windowLog;
            assert(windowLog != 0);
            if (cdict->matchState.dedicatedDictSearch != 0)
            {
                ZSTD_dedicatedDictSearch_revertCParams(&adjusted_cdict_cParams);
            }

            @params.cParams = ZSTD_adjustCParams_internal(
                adjusted_cdict_cParams,
                pledgedSrcSize,
                cdict->dictContentSize,
                ZSTD_CParamMode_e.ZSTD_cpm_attachDict,
                @params.useRowMatchFinder
            );
            @params.cParams.windowLog = windowLog;
            @params.useRowMatchFinder = cdict->useRowMatchFinder;
            {
                nuint err_code = ZSTD_resetCCtx_internal(
                    cctx,
                    &@params,
                    pledgedSrcSize,
                    0,
                    ZSTD_compResetPolicy_e.ZSTDcrp_makeClean,
                    zbuff
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(cctx->appliedParams.cParams.strategy == adjusted_cdict_cParams.strategy);
        }

        {
            uint cdictEnd = (uint)(
                cdict->matchState.window.nextSrc - cdict->matchState.window.@base
            );
            uint cdictLen = cdictEnd - cdict->matchState.window.dictLimit;
            if (cdictLen != 0)
            {
                cctx->blockState.matchState.dictMatchState = &cdict->matchState;
                if (cctx->blockState.matchState.window.dictLimit < cdictEnd)
                {
                    cctx->blockState.matchState.window.nextSrc =
                        cctx->blockState.matchState.window.@base + cdictEnd;
                    ZSTD_window_clear(&cctx->blockState.matchState.window);
                }

                cctx->blockState.matchState.loadedDictEnd = cctx->blockState
                    .matchState
                    .window
                    .dictLimit;
            }
        }

        cctx->dictID = cdict->dictID;
        cctx->dictContentSize = cdict->dictContentSize;
        memcpy(
            cctx->blockState.prevCBlock,
            &cdict->cBlockState,
            (uint)sizeof(ZSTD_compressedBlockState_t)
        );
        return 0;
    }

    private static void ZSTD_copyCDictTableIntoCCtx(
        uint* dst,
        uint* src,
        nuint tableSize,
        ZSTD_compressionParameters* cParams
    )
    {
        if (ZSTD_CDictIndicesAreTagged(cParams) != 0)
        {
            /* Remove tags from the CDict table if they are present.
             * See docs on "short cache" in zstd_compress_internal.h for context. */
            nuint i;
            for (i = 0; i < tableSize; i++)
            {
                uint taggedIndex = src[i];
                uint index = taggedIndex >> 8;
                dst[i] = index;
            }
        }
        else
        {
            memcpy(dst, src, (uint)(tableSize * sizeof(uint)));
        }
    }

    private static nuint ZSTD_resetCCtx_byCopyingCDict(
        ZSTD_CCtx_s* cctx,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s @params,
        ulong pledgedSrcSize,
        ZSTD_buffered_policy_e zbuff
    )
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
                nuint err_code = ZSTD_resetCCtx_internal(
                    cctx,
                    &@params,
                    pledgedSrcSize,
                    0,
                    ZSTD_compResetPolicy_e.ZSTDcrp_leaveDirty,
                    zbuff
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(cctx->appliedParams.cParams.strategy == cdict_cParams->strategy);
            assert(cctx->appliedParams.cParams.hashLog == cdict_cParams->hashLog);
            assert(cctx->appliedParams.cParams.chainLog == cdict_cParams->chainLog);
        }

        ZSTD_cwksp_mark_tables_dirty(&cctx->workspace);
        assert(@params.useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        {
            /* DDS guaranteed disabled */
            nuint chainSize =
                ZSTD_allocateChainTable(cdict_cParams->strategy, cdict->useRowMatchFinder, 0) != 0
                    ? (nuint)1 << (int)cdict_cParams->chainLog
                    : 0;
            nuint hSize = (nuint)1 << (int)cdict_cParams->hashLog;
            ZSTD_copyCDictTableIntoCCtx(
                cctx->blockState.matchState.hashTable,
                cdict->matchState.hashTable,
                hSize,
                cdict_cParams
            );
            if (
                ZSTD_allocateChainTable(
                    cctx->appliedParams.cParams.strategy,
                    cctx->appliedParams.useRowMatchFinder,
                    0
                ) != 0
            )
            {
                ZSTD_copyCDictTableIntoCCtx(
                    cctx->blockState.matchState.chainTable,
                    cdict->matchState.chainTable,
                    chainSize,
                    cdict_cParams
                );
            }

            if (ZSTD_rowMatchFinderUsed(cdict_cParams->strategy, cdict->useRowMatchFinder) != 0)
            {
                nuint tagTableSize = hSize;
                memcpy(
                    cctx->blockState.matchState.tagTable,
                    cdict->matchState.tagTable,
                    (uint)tagTableSize
                );
                cctx->blockState.matchState.hashSalt = cdict->matchState.hashSalt;
            }
        }

        assert(cctx->blockState.matchState.hashLog3 <= 31);
        {
            uint h3log = cctx->blockState.matchState.hashLog3;
            nuint h3Size = h3log != 0 ? (nuint)1 << (int)h3log : 0;
            assert(cdict->matchState.hashLog3 == 0);
            memset(cctx->blockState.matchState.hashTable3, 0, (uint)(h3Size * sizeof(uint)));
        }

        ZSTD_cwksp_mark_tables_clean(&cctx->workspace);
        {
            ZSTD_MatchState_t* srcMatchState = &cdict->matchState;
            ZSTD_MatchState_t* dstMatchState = &cctx->blockState.matchState;
            dstMatchState->window = srcMatchState->window;
            dstMatchState->nextToUpdate = srcMatchState->nextToUpdate;
            dstMatchState->loadedDictEnd = srcMatchState->loadedDictEnd;
        }

        cctx->dictID = cdict->dictID;
        cctx->dictContentSize = cdict->dictContentSize;
        memcpy(
            cctx->blockState.prevCBlock,
            &cdict->cBlockState,
            (uint)sizeof(ZSTD_compressedBlockState_t)
        );
        return 0;
    }

    /* We have a choice between copying the dictionary context into the working
     * context, or referencing the dictionary context from the working context
     * in-place. We decide here which strategy to use. */
    private static nuint ZSTD_resetCCtx_usingCDict(
        ZSTD_CCtx_s* cctx,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize,
        ZSTD_buffered_policy_e zbuff
    )
    {
        if (ZSTD_shouldAttachDict(cdict, @params, pledgedSrcSize) != 0)
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
    private static nuint ZSTD_copyCCtx_internal(
        ZSTD_CCtx_s* dstCCtx,
        ZSTD_CCtx_s* srcCCtx,
        ZSTD_frameParameters fParams,
        ulong pledgedSrcSize,
        ZSTD_buffered_policy_e zbuff
    )
    {
        if (srcCCtx->stage != ZSTD_compressionStage_e.ZSTDcs_init)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        memcpy(&dstCCtx->customMem, &srcCCtx->customMem, (uint)sizeof(ZSTD_customMem));
        {
            ZSTD_CCtx_params_s @params = dstCCtx->requestedParams;
            @params.cParams = srcCCtx->appliedParams.cParams;
            assert(srcCCtx->appliedParams.useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
            assert(srcCCtx->appliedParams.postBlockSplitter != ZSTD_paramSwitch_e.ZSTD_ps_auto);
            assert(srcCCtx->appliedParams.ldmParams.enableLdm != ZSTD_paramSwitch_e.ZSTD_ps_auto);
            @params.useRowMatchFinder = srcCCtx->appliedParams.useRowMatchFinder;
            @params.postBlockSplitter = srcCCtx->appliedParams.postBlockSplitter;
            @params.ldmParams = srcCCtx->appliedParams.ldmParams;
            @params.fParams = fParams;
            @params.maxBlockSize = srcCCtx->appliedParams.maxBlockSize;
            ZSTD_resetCCtx_internal(
                dstCCtx,
                &@params,
                pledgedSrcSize,
                0,
                ZSTD_compResetPolicy_e.ZSTDcrp_leaveDirty,
                zbuff
            );
            assert(
                dstCCtx->appliedParams.cParams.windowLog == srcCCtx->appliedParams.cParams.windowLog
            );
            assert(
                dstCCtx->appliedParams.cParams.strategy == srcCCtx->appliedParams.cParams.strategy
            );
            assert(
                dstCCtx->appliedParams.cParams.hashLog == srcCCtx->appliedParams.cParams.hashLog
            );
            assert(
                dstCCtx->appliedParams.cParams.chainLog == srcCCtx->appliedParams.cParams.chainLog
            );
            assert(
                dstCCtx->blockState.matchState.hashLog3 == srcCCtx->blockState.matchState.hashLog3
            );
        }

        ZSTD_cwksp_mark_tables_dirty(&dstCCtx->workspace);
        {
            nuint chainSize =
                ZSTD_allocateChainTable(
                    srcCCtx->appliedParams.cParams.strategy,
                    srcCCtx->appliedParams.useRowMatchFinder,
                    0
                ) != 0
                    ? (nuint)1 << (int)srcCCtx->appliedParams.cParams.chainLog
                    : 0;
            nuint hSize = (nuint)1 << (int)srcCCtx->appliedParams.cParams.hashLog;
            uint h3log = srcCCtx->blockState.matchState.hashLog3;
            nuint h3Size = h3log != 0 ? (nuint)1 << (int)h3log : 0;
            memcpy(
                dstCCtx->blockState.matchState.hashTable,
                srcCCtx->blockState.matchState.hashTable,
                (uint)(hSize * sizeof(uint))
            );
            memcpy(
                dstCCtx->blockState.matchState.chainTable,
                srcCCtx->blockState.matchState.chainTable,
                (uint)(chainSize * sizeof(uint))
            );
            memcpy(
                dstCCtx->blockState.matchState.hashTable3,
                srcCCtx->blockState.matchState.hashTable3,
                (uint)(h3Size * sizeof(uint))
            );
        }

        ZSTD_cwksp_mark_tables_clean(&dstCCtx->workspace);
        {
            ZSTD_MatchState_t* srcMatchState = &srcCCtx->blockState.matchState;
            ZSTD_MatchState_t* dstMatchState = &dstCCtx->blockState.matchState;
            dstMatchState->window = srcMatchState->window;
            dstMatchState->nextToUpdate = srcMatchState->nextToUpdate;
            dstMatchState->loadedDictEnd = srcMatchState->loadedDictEnd;
        }

        dstCCtx->dictID = srcCCtx->dictID;
        dstCCtx->dictContentSize = srcCCtx->dictContentSize;
        memcpy(
            dstCCtx->blockState.prevCBlock,
            srcCCtx->blockState.prevCBlock,
            (uint)sizeof(ZSTD_compressedBlockState_t)
        );
        return 0;
    }

    /*! ZSTD_copyCCtx() :
     *  Duplicate an existing context `srcCCtx` into another one `dstCCtx`.
     *  Only works during stage ZSTDcs_init (i.e. after creation, but before first call to ZSTD_compressContinue()).
     *  pledgedSrcSize==0 means "unknown".
     *   @return : 0, or an error code */
    public static nuint ZSTD_copyCCtx(
        ZSTD_CCtx_s* dstCCtx,
        ZSTD_CCtx_s* srcCCtx,
        ulong pledgedSrcSize
    )
    {
        /*content*/
        ZSTD_frameParameters fParams = new ZSTD_frameParameters
        {
            contentSizeFlag = 1,
            checksumFlag = 0,
            noDictIDFlag = 0,
        };
        ZSTD_buffered_policy_e zbuff = srcCCtx->bufferedPolicy;
        if (pledgedSrcSize == 0)
            pledgedSrcSize = unchecked(0UL - 1);
        fParams.contentSizeFlag = pledgedSrcSize != unchecked(0UL - 1) ? 1 : 0;
        return ZSTD_copyCCtx_internal(dstCCtx, srcCCtx, fParams, pledgedSrcSize, zbuff);
    }

    /*! ZSTD_reduceTable() :
     *  reduce table indexes by `reducerValue`, or squash to zero.
     *  PreserveMark preserves "unsorted mark" for btlazy2 strategy.
     *  It must be set to a clear 0/1 value, to remove branch during inlining.
     *  Presume table size is a multiple of ZSTD_ROWSIZE
     *  to help auto-vectorization */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_reduceTable_internal(
        uint* table,
        uint size,
        uint reducerValue,
        int preserveMark
    )
    {
        int nbRows = (int)size / 16;
        int cellNb = 0;
        int rowNb;
        /* Protect special index values < ZSTD_WINDOW_START_INDEX. */
        uint reducerThreshold = reducerValue + 2;
        assert((size & 16 - 1) == 0);
        assert(size < 1U << 31);
        for (rowNb = 0; rowNb < nbRows; rowNb++)
        {
            int column;
            for (column = 0; column < 16; column++)
            {
                uint newVal;
                if (preserveMark != 0 && table[cellNb] == 1)
                {
                    newVal = 1;
                }
                else if (table[cellNb] < reducerThreshold)
                {
                    newVal = 0;
                }
                else
                {
                    newVal = table[cellNb] - reducerValue;
                }

                table[cellNb] = newVal;
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
    private static void ZSTD_reduceIndex(
        ZSTD_MatchState_t* ms,
        ZSTD_CCtx_params_s* @params,
        uint reducerValue
    )
    {
        {
            uint hSize = (uint)1 << (int)@params->cParams.hashLog;
            ZSTD_reduceTable(ms->hashTable, hSize, reducerValue);
        }

        if (
            ZSTD_allocateChainTable(
                @params->cParams.strategy,
                @params->useRowMatchFinder,
                (uint)ms->dedicatedDictSearch
            ) != 0
        )
        {
            uint chainSize = (uint)1 << (int)@params->cParams.chainLog;
            if (@params->cParams.strategy == ZSTD_strategy.ZSTD_btlazy2)
                ZSTD_reduceTable_btlazy2(ms->chainTable, chainSize, reducerValue);
            else
                ZSTD_reduceTable(ms->chainTable, chainSize, reducerValue);
        }

        if (ms->hashLog3 != 0)
        {
            uint h3Size = (uint)1 << (int)ms->hashLog3;
            ZSTD_reduceTable(ms->hashTable3, h3Size, reducerValue);
        }
    }

    /* See doc/zstd_compression_format.md for detailed format description */
    private static int ZSTD_seqToCodes(SeqStore_t* seqStorePtr)
    {
        SeqDef_s* sequences = seqStorePtr->sequencesStart;
        byte* llCodeTable = seqStorePtr->llCode;
        byte* ofCodeTable = seqStorePtr->ofCode;
        byte* mlCodeTable = seqStorePtr->mlCode;
        uint nbSeq = (uint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
        uint u;
        int longOffsets = 0;
        assert(nbSeq <= seqStorePtr->maxNbSeq);
        for (u = 0; u < nbSeq; u++)
        {
            uint llv = sequences[u].litLength;
            uint ofCode = ZSTD_highbit32(sequences[u].offBase);
            uint mlv = sequences[u].mlBase;
            llCodeTable[u] = (byte)ZSTD_LLcode(llv);
            ofCodeTable[u] = (byte)ofCode;
            mlCodeTable[u] = (byte)ZSTD_MLcode(mlv);
            assert(!(MEM_64bits && ofCode >= (uint)(MEM_32bits ? 25 : 57)));
            if (MEM_32bits && ofCode >= (uint)(MEM_32bits ? 25 : 57))
                longOffsets = 1;
        }

        if (seqStorePtr->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_literalLength)
            llCodeTable[seqStorePtr->longLengthPos] = 35;
        if (seqStorePtr->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_matchLength)
            mlCodeTable[seqStorePtr->longLengthPos] = 52;
        return longOffsets;
    }

    /* ZSTD_useTargetCBlockSize():
     * Returns if target compressed block size param is being used.
     * If used, compression will do best effort to make a compressed block size to be around targetCBlockSize.
     * Returns 1 if true, 0 otherwise. */
    private static int ZSTD_useTargetCBlockSize(ZSTD_CCtx_params_s* cctxParams)
    {
        return cctxParams->targetCBlockSize != 0 ? 1 : 0;
    }

    /* ZSTD_blockSplitterEnabled():
     * Returns if block splitting param is being used
     * If used, compression will do best effort to split a block in order to improve compression ratio.
     * At the time this function is called, the parameter must be finalized.
     * Returns 1 if true, 0 otherwise. */
    private static int ZSTD_blockSplitterEnabled(ZSTD_CCtx_params_s* cctxParams)
    {
        assert(cctxParams->postBlockSplitter != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        return cctxParams->postBlockSplitter == ZSTD_paramSwitch_e.ZSTD_ps_enable ? 1 : 0;
    }

    /* ZSTD_buildSequencesStatistics():
     * Returns a ZSTD_symbolEncodingTypeStats_t, or a zstd error code in the `size` field.
     * Modifies `nextEntropy` to have the appropriate values as a side effect.
     * nbSeq must be greater than 0.
     *
     * entropyWkspSize must be of size at least ENTROPY_WORKSPACE_SIZE - (MaxSeq + 1)*sizeof(U32)
     */
    private static ZSTD_symbolEncodingTypeStats_t ZSTD_buildSequencesStatistics(
        SeqStore_t* seqStorePtr,
        nuint nbSeq,
        ZSTD_fseCTables_t* prevEntropy,
        ZSTD_fseCTables_t* nextEntropy,
        byte* dst,
        byte* dstEnd,
        ZSTD_strategy strategy,
        uint* countWorkspace,
        void* entropyWorkspace,
        nuint entropyWkspSize
    )
    {
        byte* ostart = dst;
        byte* oend = dstEnd;
        byte* op = ostart;
        uint* CTable_LitLength = nextEntropy->litlengthCTable;
        uint* CTable_OffsetBits = nextEntropy->offcodeCTable;
        uint* CTable_MatchLength = nextEntropy->matchlengthCTable;
        byte* ofCodeTable = seqStorePtr->ofCode;
        byte* llCodeTable = seqStorePtr->llCode;
        byte* mlCodeTable = seqStorePtr->mlCode;
        ZSTD_symbolEncodingTypeStats_t stats;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out stats);
        stats.lastCountSize = 0;
        stats.longOffsets = ZSTD_seqToCodes(seqStorePtr);
        assert(op <= oend);
        assert(nbSeq != 0);
        {
            uint max = 35;
            /* can't fail */
            nuint mostFrequent = HIST_countFast_wksp(
                countWorkspace,
                &max,
                llCodeTable,
                nbSeq,
                entropyWorkspace,
                entropyWkspSize
            );
            nextEntropy->litlength_repeatMode = prevEntropy->litlength_repeatMode;
            stats.LLtype = (uint)ZSTD_selectEncodingType(
                &nextEntropy->litlength_repeatMode,
                countWorkspace,
                max,
                mostFrequent,
                nbSeq,
                9,
                prevEntropy->litlengthCTable,
                LL_defaultNorm,
                LL_defaultNormLog,
                ZSTD_DefaultPolicy_e.ZSTD_defaultAllowed,
                strategy
            );
            assert(
                SymbolEncodingType_e.set_basic < SymbolEncodingType_e.set_compressed
                    && SymbolEncodingType_e.set_rle < SymbolEncodingType_e.set_compressed
            );
            assert(
                !(
                    stats.LLtype < (uint)SymbolEncodingType_e.set_compressed
                    && nextEntropy->litlength_repeatMode != FSE_repeat.FSE_repeat_none
                )
            );
            {
                nuint countSize = ZSTD_buildCTable(
                    op,
                    (nuint)(oend - op),
                    CTable_LitLength,
                    9,
                    (SymbolEncodingType_e)stats.LLtype,
                    countWorkspace,
                    max,
                    llCodeTable,
                    nbSeq,
                    LL_defaultNorm,
                    LL_defaultNormLog,
                    35,
                    prevEntropy->litlengthCTable,
                    sizeof(uint) * 329,
                    entropyWorkspace,
                    entropyWkspSize
                );
                if (ERR_isError(countSize))
                {
                    stats.size = countSize;
                    return stats;
                }

                if (stats.LLtype == (uint)SymbolEncodingType_e.set_compressed)
                    stats.lastCountSize = countSize;
                op += countSize;
                assert(op <= oend);
            }
        }

        {
            uint max = 31;
            nuint mostFrequent = HIST_countFast_wksp(
                countWorkspace,
                &max,
                ofCodeTable,
                nbSeq,
                entropyWorkspace,
                entropyWkspSize
            );
            /* We can only use the basic table if max <= DefaultMaxOff, otherwise the offsets are too large */
            ZSTD_DefaultPolicy_e defaultPolicy =
                max <= 28
                    ? ZSTD_DefaultPolicy_e.ZSTD_defaultAllowed
                    : ZSTD_DefaultPolicy_e.ZSTD_defaultDisallowed;
            nextEntropy->offcode_repeatMode = prevEntropy->offcode_repeatMode;
            stats.Offtype = (uint)ZSTD_selectEncodingType(
                &nextEntropy->offcode_repeatMode,
                countWorkspace,
                max,
                mostFrequent,
                nbSeq,
                8,
                prevEntropy->offcodeCTable,
                OF_defaultNorm,
                OF_defaultNormLog,
                defaultPolicy,
                strategy
            );
            assert(
                !(
                    stats.Offtype < (uint)SymbolEncodingType_e.set_compressed
                    && nextEntropy->offcode_repeatMode != FSE_repeat.FSE_repeat_none
                )
            );
            {
                nuint countSize = ZSTD_buildCTable(
                    op,
                    (nuint)(oend - op),
                    CTable_OffsetBits,
                    8,
                    (SymbolEncodingType_e)stats.Offtype,
                    countWorkspace,
                    max,
                    ofCodeTable,
                    nbSeq,
                    OF_defaultNorm,
                    OF_defaultNormLog,
                    28,
                    prevEntropy->offcodeCTable,
                    sizeof(uint) * 193,
                    entropyWorkspace,
                    entropyWkspSize
                );
                if (ERR_isError(countSize))
                {
                    stats.size = countSize;
                    return stats;
                }

                if (stats.Offtype == (uint)SymbolEncodingType_e.set_compressed)
                    stats.lastCountSize = countSize;
                op += countSize;
                assert(op <= oend);
            }
        }

        {
            uint max = 52;
            nuint mostFrequent = HIST_countFast_wksp(
                countWorkspace,
                &max,
                mlCodeTable,
                nbSeq,
                entropyWorkspace,
                entropyWkspSize
            );
            nextEntropy->matchlength_repeatMode = prevEntropy->matchlength_repeatMode;
            stats.MLtype = (uint)ZSTD_selectEncodingType(
                &nextEntropy->matchlength_repeatMode,
                countWorkspace,
                max,
                mostFrequent,
                nbSeq,
                9,
                prevEntropy->matchlengthCTable,
                ML_defaultNorm,
                ML_defaultNormLog,
                ZSTD_DefaultPolicy_e.ZSTD_defaultAllowed,
                strategy
            );
            assert(
                !(
                    stats.MLtype < (uint)SymbolEncodingType_e.set_compressed
                    && nextEntropy->matchlength_repeatMode != FSE_repeat.FSE_repeat_none
                )
            );
            {
                nuint countSize = ZSTD_buildCTable(
                    op,
                    (nuint)(oend - op),
                    CTable_MatchLength,
                    9,
                    (SymbolEncodingType_e)stats.MLtype,
                    countWorkspace,
                    max,
                    mlCodeTable,
                    nbSeq,
                    ML_defaultNorm,
                    ML_defaultNormLog,
                    52,
                    prevEntropy->matchlengthCTable,
                    sizeof(uint) * 363,
                    entropyWorkspace,
                    entropyWkspSize
                );
                if (ERR_isError(countSize))
                {
                    stats.size = countSize;
                    return stats;
                }

                if (stats.MLtype == (uint)SymbolEncodingType_e.set_compressed)
                    stats.lastCountSize = countSize;
                op += countSize;
                assert(op <= oend);
            }
        }

        stats.size = (nuint)(op - ostart);
        return stats;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_entropyCompressSeqStore_internal(
        void* dst,
        nuint dstCapacity,
        void* literals,
        nuint litSize,
        SeqStore_t* seqStorePtr,
        ZSTD_entropyCTables_t* prevEntropy,
        ZSTD_entropyCTables_t* nextEntropy,
        ZSTD_CCtx_params_s* cctxParams,
        void* entropyWorkspace,
        nuint entropyWkspSize,
        int bmi2
    )
    {
        ZSTD_strategy strategy = cctxParams->cParams.strategy;
        uint* count = (uint*)entropyWorkspace;
        uint* CTable_LitLength = nextEntropy->fse.litlengthCTable;
        uint* CTable_OffsetBits = nextEntropy->fse.offcodeCTable;
        uint* CTable_MatchLength = nextEntropy->fse.matchlengthCTable;
        SeqDef_s* sequences = seqStorePtr->sequencesStart;
        nuint nbSeq = (nuint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
        byte* ofCodeTable = seqStorePtr->ofCode;
        byte* llCodeTable = seqStorePtr->llCode;
        byte* mlCodeTable = seqStorePtr->mlCode;
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstCapacity;
        byte* op = ostart;
        nuint lastCountSize;
        int longOffsets = 0;
        entropyWorkspace = count + (52 + 1);
        entropyWkspSize -= (52 + 1) * sizeof(uint);
        assert(entropyWkspSize >= (8 << 10) + 512);
        {
            nuint numSequences = (nuint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
            /* Base suspicion of uncompressibility on ratio of literals to sequences */
            int suspectUncompressible = numSequences == 0 || litSize / numSequences >= 20 ? 1 : 0;
            nuint cSize = ZSTD_compressLiterals(
                op,
                dstCapacity,
                literals,
                litSize,
                entropyWorkspace,
                entropyWkspSize,
                &prevEntropy->huf,
                &nextEntropy->huf,
                cctxParams->cParams.strategy,
                ZSTD_literalsCompressionIsDisabled(cctxParams),
                suspectUncompressible,
                bmi2
            );
            {
                nuint err_code = cSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(cSize <= dstCapacity);
            op += cSize;
        }

        if (oend - op < 3 + 1)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (nbSeq < 128)
        {
            *op++ = (byte)nbSeq;
        }
        else if (nbSeq < 0x7F00)
        {
            op[0] = (byte)((nbSeq >> 8) + 0x80);
            op[1] = (byte)nbSeq;
            op += 2;
        }
        else
        {
            op[0] = 0xFF;
            MEM_writeLE16(op + 1, (ushort)(nbSeq - 0x7F00));
            op += 3;
        }

        assert(op <= oend);
        if (nbSeq == 0)
        {
            memcpy(&nextEntropy->fse, &prevEntropy->fse, (uint)sizeof(ZSTD_fseCTables_t));
            return (nuint)(op - ostart);
        }

        {
            byte* seqHead = op++;
            /* build stats for sequences */
            ZSTD_symbolEncodingTypeStats_t stats = ZSTD_buildSequencesStatistics(
                seqStorePtr,
                nbSeq,
                &prevEntropy->fse,
                &nextEntropy->fse,
                op,
                oend,
                strategy,
                count,
                entropyWorkspace,
                entropyWkspSize
            );
            {
                nuint err_code = stats.size;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            *seqHead = (byte)((stats.LLtype << 6) + (stats.Offtype << 4) + (stats.MLtype << 2));
            lastCountSize = stats.lastCountSize;
            op += stats.size;
            longOffsets = stats.longOffsets;
        }

        {
            nuint bitstreamSize = ZSTD_encodeSequences(
                op,
                (nuint)(oend - op),
                CTable_MatchLength,
                mlCodeTable,
                CTable_OffsetBits,
                ofCodeTable,
                CTable_LitLength,
                llCodeTable,
                sequences,
                nbSeq,
                longOffsets,
                bmi2
            );
            {
                nuint err_code = bitstreamSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            op += bitstreamSize;
            assert(op <= oend);
            if (lastCountSize != 0 && lastCountSize + bitstreamSize < 4)
            {
                assert(lastCountSize + bitstreamSize == 3);
                return 0;
            }
        }

        return (nuint)(op - ostart);
    }

    private static nuint ZSTD_entropyCompressSeqStore_wExtLitBuffer(
        void* dst,
        nuint dstCapacity,
        void* literals,
        nuint litSize,
        nuint blockSize,
        SeqStore_t* seqStorePtr,
        ZSTD_entropyCTables_t* prevEntropy,
        ZSTD_entropyCTables_t* nextEntropy,
        ZSTD_CCtx_params_s* cctxParams,
        void* entropyWorkspace,
        nuint entropyWkspSize,
        int bmi2
    )
    {
        nuint cSize = ZSTD_entropyCompressSeqStore_internal(
            dst,
            dstCapacity,
            literals,
            litSize,
            seqStorePtr,
            prevEntropy,
            nextEntropy,
            cctxParams,
            entropyWorkspace,
            entropyWkspSize,
            bmi2
        );
        if (cSize == 0)
            return 0;
        if (
            cSize == unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall))
            && blockSize <= dstCapacity
        )
        {
            return 0;
        }

        {
            nuint err_code = cSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint maxCSize = blockSize - ZSTD_minGain(blockSize, cctxParams->cParams.strategy);
            if (cSize >= maxCSize)
                return 0;
        }

        assert(cSize < 1 << 17);
        return cSize;
    }

    private static nuint ZSTD_entropyCompressSeqStore(
        SeqStore_t* seqStorePtr,
        ZSTD_entropyCTables_t* prevEntropy,
        ZSTD_entropyCTables_t* nextEntropy,
        ZSTD_CCtx_params_s* cctxParams,
        void* dst,
        nuint dstCapacity,
        nuint srcSize,
        void* entropyWorkspace,
        nuint entropyWkspSize,
        int bmi2
    )
    {
        return ZSTD_entropyCompressSeqStore_wExtLitBuffer(
            dst,
            dstCapacity,
            seqStorePtr->litStart,
            (nuint)(seqStorePtr->lit - seqStorePtr->litStart),
            srcSize,
            seqStorePtr,
            prevEntropy,
            nextEntropy,
            cctxParams,
            entropyWorkspace,
            entropyWkspSize,
            bmi2
        );
    }

    private static readonly ZSTD_BlockCompressor_f?[][] blockCompressor =
        new ZSTD_BlockCompressor_f?[4][]
        {
            new ZSTD_BlockCompressor_f[10]
            {
                ZSTD_compressBlock_fast,
                ZSTD_compressBlock_fast,
                ZSTD_compressBlock_doubleFast,
                ZSTD_compressBlock_greedy,
                ZSTD_compressBlock_lazy,
                ZSTD_compressBlock_lazy2,
                ZSTD_compressBlock_btlazy2,
                ZSTD_compressBlock_btopt,
                ZSTD_compressBlock_btultra,
                ZSTD_compressBlock_btultra2,
            },
            new ZSTD_BlockCompressor_f[10]
            {
                ZSTD_compressBlock_fast_extDict,
                ZSTD_compressBlock_fast_extDict,
                ZSTD_compressBlock_doubleFast_extDict,
                ZSTD_compressBlock_greedy_extDict,
                ZSTD_compressBlock_lazy_extDict,
                ZSTD_compressBlock_lazy2_extDict,
                ZSTD_compressBlock_btlazy2_extDict,
                ZSTD_compressBlock_btopt_extDict,
                ZSTD_compressBlock_btultra_extDict,
                ZSTD_compressBlock_btultra_extDict,
            },
            new ZSTD_BlockCompressor_f[10]
            {
                ZSTD_compressBlock_fast_dictMatchState,
                ZSTD_compressBlock_fast_dictMatchState,
                ZSTD_compressBlock_doubleFast_dictMatchState,
                ZSTD_compressBlock_greedy_dictMatchState,
                ZSTD_compressBlock_lazy_dictMatchState,
                ZSTD_compressBlock_lazy2_dictMatchState,
                ZSTD_compressBlock_btlazy2_dictMatchState,
                ZSTD_compressBlock_btopt_dictMatchState,
                ZSTD_compressBlock_btultra_dictMatchState,
                ZSTD_compressBlock_btultra_dictMatchState,
            },
            new ZSTD_BlockCompressor_f?[10]
            {
                null,
                null,
                null,
                ZSTD_compressBlock_greedy_dedicatedDictSearch,
                ZSTD_compressBlock_lazy_dedicatedDictSearch,
                ZSTD_compressBlock_lazy2_dedicatedDictSearch,
                null,
                null,
                null,
                null,
            },
        };
    private static readonly ZSTD_BlockCompressor_f[][] rowBasedBlockCompressors =
        new ZSTD_BlockCompressor_f[4][]
        {
            new ZSTD_BlockCompressor_f[3]
            {
                ZSTD_compressBlock_greedy_row,
                ZSTD_compressBlock_lazy_row,
                ZSTD_compressBlock_lazy2_row,
            },
            new ZSTD_BlockCompressor_f[3]
            {
                ZSTD_compressBlock_greedy_extDict_row,
                ZSTD_compressBlock_lazy_extDict_row,
                ZSTD_compressBlock_lazy2_extDict_row,
            },
            new ZSTD_BlockCompressor_f[3]
            {
                ZSTD_compressBlock_greedy_dictMatchState_row,
                ZSTD_compressBlock_lazy_dictMatchState_row,
                ZSTD_compressBlock_lazy2_dictMatchState_row,
            },
            new ZSTD_BlockCompressor_f[3]
            {
                ZSTD_compressBlock_greedy_dedicatedDictSearch_row,
                ZSTD_compressBlock_lazy_dedicatedDictSearch_row,
                ZSTD_compressBlock_lazy2_dedicatedDictSearch_row,
            },
        };

    /* ZSTD_selectBlockCompressor() :
     * Not static, but internal use only (used by long distance matcher)
     * assumption : strat is a valid strategy */
    private static ZSTD_BlockCompressor_f ZSTD_selectBlockCompressor(
        ZSTD_strategy strat,
        ZSTD_paramSwitch_e useRowMatchFinder,
        ZSTD_dictMode_e dictMode
    )
    {
        ZSTD_BlockCompressor_f? selectedCompressor;
        assert(ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_strategy, (int)strat) != 0);
        if (ZSTD_rowMatchFinderUsed(strat, useRowMatchFinder) != 0)
        {
            assert(useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
            selectedCompressor = rowBasedBlockCompressors[(int)dictMode][
                (int)strat - (int)ZSTD_strategy.ZSTD_greedy
            ];
        }
        else
        {
            selectedCompressor = blockCompressor[(int)dictMode][(int)strat];
        }

        assert(selectedCompressor != null);
        return selectedCompressor.NotNull();
    }

    private static void ZSTD_storeLastLiterals(
        SeqStore_t* seqStorePtr,
        byte* anchor,
        nuint lastLLSize
    )
    {
        memcpy(seqStorePtr->lit, anchor, (uint)lastLLSize);
        seqStorePtr->lit += lastLLSize;
    }

    private static void ZSTD_resetSeqStore(SeqStore_t* ssPtr)
    {
        ssPtr->lit = ssPtr->litStart;
        ssPtr->sequences = ssPtr->sequencesStart;
        ssPtr->longLengthType = ZSTD_longLengthType_e.ZSTD_llt_none;
    }

    /* ZSTD_postProcessSequenceProducerResult() :
     * Validates and post-processes sequences obtained through the external matchfinder API:
     *   - Checks whether nbExternalSeqs represents an error condition.
     *   - Appends a block delimiter to outSeqs if one is not already present.
     *     See zstd.h for context regarding block delimiters.
     * Returns the number of sequences after post-processing, or an error code. */
    private static nuint ZSTD_postProcessSequenceProducerResult(
        ZSTD_Sequence* outSeqs,
        nuint nbExternalSeqs,
        nuint outSeqsCapacity,
        nuint srcSize
    )
    {
        if (nbExternalSeqs > outSeqsCapacity)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_sequenceProducer_failed));
        }

        if (nbExternalSeqs == 0 && srcSize > 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_sequenceProducer_failed));
        }

        if (srcSize == 0)
        {
            outSeqs[0] = new ZSTD_Sequence();
            return 1;
        }

        {
            ZSTD_Sequence lastSeq = outSeqs[nbExternalSeqs - 1];
            if (lastSeq.offset == 0 && lastSeq.matchLength == 0)
            {
                return nbExternalSeqs;
            }

            if (nbExternalSeqs == outSeqsCapacity)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_sequenceProducer_failed));
            }

            outSeqs[nbExternalSeqs] = new ZSTD_Sequence();
            return nbExternalSeqs + 1;
        }
    }

    /* ZSTD_fastSequenceLengthSum() :
     * Returns sum(litLen) + sum(matchLen) + lastLits for *seqBuf*.
     * Similar to another function in zstd_compress.c (determine_blockSize),
     * except it doesn't check for a block delimiter to end summation.
     * Removing the early exit allows the compiler to auto-vectorize (https://godbolt.org/z/cY1cajz9P).
     * This function can be deleted and replaced by determine_blockSize after we resolve issue #3456. */
    private static nuint ZSTD_fastSequenceLengthSum(ZSTD_Sequence* seqBuf, nuint seqBufSize)
    {
        nuint matchLenSum,
            litLenSum,
            i;
        matchLenSum = 0;
        litLenSum = 0;
        for (i = 0; i < seqBufSize; i++)
        {
            litLenSum += seqBuf[i].litLength;
            matchLenSum += seqBuf[i].matchLength;
        }

        return litLenSum + matchLenSum;
    }

    /**
     * Function to validate sequences produced by a block compressor.
     */
    private static void ZSTD_validateSeqStore(
        SeqStore_t* seqStore,
        ZSTD_compressionParameters* cParams
    ) { }

    private static nuint ZSTD_buildSeqStore(ZSTD_CCtx_s* zc, void* src, nuint srcSize)
    {
        ZSTD_MatchState_t* ms = &zc->blockState.matchState;
        assert(srcSize <= 1 << 17);
        ZSTD_assertEqualCParams(zc->appliedParams.cParams, ms->cParams);
        if (srcSize < (nuint)(1 + 1) + ZSTD_blockHeaderSize + 1 + 1)
        {
            if (zc->appliedParams.cParams.strategy >= ZSTD_strategy.ZSTD_btopt)
            {
                ZSTD_ldm_skipRawSeqStoreBytes(&zc->externSeqStore, srcSize);
            }
            else
            {
                ZSTD_ldm_skipSequences(
                    &zc->externSeqStore,
                    srcSize,
                    zc->appliedParams.cParams.minMatch
                );
            }

            return (nuint)ZSTD_BuildSeqStore_e.ZSTDbss_noCompress;
        }

        ZSTD_resetSeqStore(&zc->seqStore);
        ms->opt.symbolCosts = &zc->blockState.prevCBlock->entropy;
        ms->opt.literalCompressionMode = zc->appliedParams.literalCompressionMode;
        assert(ms->dictMatchState == null || ms->loadedDictEnd == ms->window.dictLimit);
        {
            byte* @base = ms->window.@base;
            byte* istart = (byte*)src;
            uint curr = (uint)(istart - @base);
#if DEBUG
            if (sizeof(nint) == 8)
                assert(istart - @base < unchecked((nint)(uint)-1));
#endif
            if (curr > ms->nextToUpdate + 384)
                ms->nextToUpdate =
                    curr
                    - (192 < curr - ms->nextToUpdate - 384 ? 192 : curr - ms->nextToUpdate - 384);
        }

        {
            ZSTD_dictMode_e dictMode = ZSTD_matchState_dictMode(ms);
            nuint lastLLSize;
            {
                int i;
                for (i = 0; i < 3; ++i)
                    zc->blockState.nextCBlock->rep[i] = zc->blockState.prevCBlock->rep[i];
            }

            if (zc->externSeqStore.pos < zc->externSeqStore.size)
            {
                assert(zc->appliedParams.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_disable);
                if (ZSTD_hasExtSeqProd(&zc->appliedParams) != 0)
                {
                    return unchecked(
                        (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_combination_unsupported)
                    );
                }

                lastLLSize = ZSTD_ldm_blockCompress(
                    &zc->externSeqStore,
                    ms,
                    &zc->seqStore,
                    zc->blockState.nextCBlock->rep,
                    zc->appliedParams.useRowMatchFinder,
                    src,
                    srcSize
                );
                assert(zc->externSeqStore.pos <= zc->externSeqStore.size);
            }
            else if (zc->appliedParams.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
            {
                RawSeqStore_t ldmSeqStore = kNullRawSeqStore;
                if (ZSTD_hasExtSeqProd(&zc->appliedParams) != 0)
                {
                    return unchecked(
                        (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_combination_unsupported)
                    );
                }

                ldmSeqStore.seq = zc->ldmSequences;
                ldmSeqStore.capacity = zc->maxNbLdmSequences;
                {
                    /* Updates ldmSeqStore.size */
                    nuint err_code = ZSTD_ldm_generateSequences(
                        &zc->ldmState,
                        &ldmSeqStore,
                        &zc->appliedParams.ldmParams,
                        src,
                        srcSize
                    );
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                lastLLSize = ZSTD_ldm_blockCompress(
                    &ldmSeqStore,
                    ms,
                    &zc->seqStore,
                    zc->blockState.nextCBlock->rep,
                    zc->appliedParams.useRowMatchFinder,
                    src,
                    srcSize
                );
                assert(ldmSeqStore.pos == ldmSeqStore.size);
            }
            else if (ZSTD_hasExtSeqProd(&zc->appliedParams) != 0)
            {
                assert(zc->extSeqBufCapacity >= ZSTD_sequenceBound(srcSize));
                assert(zc->appliedParams.extSeqProdFunc != null);
                {
                    uint windowSize = (uint)1 << (int)zc->appliedParams.cParams.windowLog;
                    nuint nbExternalSeqs = (
                        (delegate* managed<
                            void*,
                            ZSTD_Sequence*,
                            nuint,
                            void*,
                            nuint,
                            void*,
                            nuint,
                            int,
                            nuint,
                            nuint>)
                            zc->appliedParams.extSeqProdFunc
                    )(
                        zc->appliedParams.extSeqProdState,
                        zc->extSeqBuf,
                        zc->extSeqBufCapacity,
                        src,
                        srcSize,
                        null,
                        0,
                        zc->appliedParams.compressionLevel,
                        windowSize
                    );
                    nuint nbPostProcessedSeqs = ZSTD_postProcessSequenceProducerResult(
                        zc->extSeqBuf,
                        nbExternalSeqs,
                        zc->extSeqBufCapacity,
                        srcSize
                    );
                    if (!ERR_isError(nbPostProcessedSeqs))
                    {
                        ZSTD_SequencePosition seqPos = new ZSTD_SequencePosition
                        {
                            idx = 0,
                            posInSequence = 0,
                            posInSrc = 0,
                        };
                        nuint seqLenSum = ZSTD_fastSequenceLengthSum(
                            zc->extSeqBuf,
                            nbPostProcessedSeqs
                        );
                        if (seqLenSum > srcSize)
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                            );
                        }

                        {
                            nuint err_code = ZSTD_transferSequences_wBlockDelim(
                                zc,
                                &seqPos,
                                zc->extSeqBuf,
                                nbPostProcessedSeqs,
                                src,
                                srcSize,
                                zc->appliedParams.searchForExternalRepcodes
                            );
                            if (ERR_isError(err_code))
                            {
                                return err_code;
                            }
                        }

                        ms->ldmSeqStore = null;
                        return (nuint)ZSTD_BuildSeqStore_e.ZSTDbss_compress;
                    }

                    if (zc->appliedParams.enableMatchFinderFallback == 0)
                    {
                        return nbPostProcessedSeqs;
                    }

                    {
                        ZSTD_BlockCompressor_f blockCompressor = ZSTD_selectBlockCompressor(
                            zc->appliedParams.cParams.strategy,
                            zc->appliedParams.useRowMatchFinder,
                            dictMode
                        );
                        ms->ldmSeqStore = null;
                        lastLLSize = blockCompressor(
                            ms,
                            &zc->seqStore,
                            zc->blockState.nextCBlock->rep,
                            src,
                            srcSize
                        );
                    }
                }
            }
            else
            {
                ZSTD_BlockCompressor_f blockCompressor = ZSTD_selectBlockCompressor(
                    zc->appliedParams.cParams.strategy,
                    zc->appliedParams.useRowMatchFinder,
                    dictMode
                );
                ms->ldmSeqStore = null;
                lastLLSize = blockCompressor(
                    ms,
                    &zc->seqStore,
                    zc->blockState.nextCBlock->rep,
                    src,
                    srcSize
                );
            }

            {
                byte* lastLiterals = (byte*)src + srcSize - lastLLSize;
                ZSTD_storeLastLiterals(&zc->seqStore, lastLiterals, lastLLSize);
            }
        }

        ZSTD_validateSeqStore(&zc->seqStore, &zc->appliedParams.cParams);
        return (nuint)ZSTD_BuildSeqStore_e.ZSTDbss_compress;
    }

    private static nuint ZSTD_copyBlockSequences(
        SeqCollector* seqCollector,
        SeqStore_t* seqStore,
        uint* prevRepcodes
    )
    {
        SeqDef_s* inSeqs = seqStore->sequencesStart;
        nuint nbInSequences = (nuint)(seqStore->sequences - inSeqs);
        nuint nbInLiterals = (nuint)(seqStore->lit - seqStore->litStart);
        ZSTD_Sequence* outSeqs =
            seqCollector->seqIndex == 0
                ? seqCollector->seqStart
                : seqCollector->seqStart + seqCollector->seqIndex;
        nuint nbOutSequences = nbInSequences + 1;
        nuint nbOutLiterals = 0;
        repcodes_s repcodes;
        nuint i;
        assert(seqCollector->seqIndex <= seqCollector->maxSequences);
        if (nbOutSequences > seqCollector->maxSequences - seqCollector->seqIndex)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        memcpy(&repcodes, prevRepcodes, (uint)sizeof(repcodes_s));
        for (i = 0; i < nbInSequences; ++i)
        {
            uint rawOffset;
            outSeqs[i].litLength = inSeqs[i].litLength;
            outSeqs[i].matchLength = (uint)(inSeqs[i].mlBase + 3);
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

            if (1 <= inSeqs[i].offBase && inSeqs[i].offBase <= 3)
            {
                assert(1 <= inSeqs[i].offBase && inSeqs[i].offBase <= 3);
                uint repcode = inSeqs[i].offBase;
                assert(repcode > 0);
                outSeqs[i].rep = repcode;
                if (outSeqs[i].litLength != 0)
                {
                    rawOffset = repcodes.rep[repcode - 1];
                }
                else
                {
                    if (repcode == 3)
                    {
                        assert(repcodes.rep[0] > 1);
                        rawOffset = repcodes.rep[0] - 1;
                    }
                    else
                    {
                        rawOffset = repcodes.rep[repcode];
                    }
                }
            }
            else
            {
                assert(inSeqs[i].offBase > 3);
                rawOffset = inSeqs[i].offBase - 3;
            }

            outSeqs[i].offset = rawOffset;
            ZSTD_updateRep(repcodes.rep, inSeqs[i].offBase, inSeqs[i].litLength == 0 ? 1U : 0U);
            nbOutLiterals += outSeqs[i].litLength;
        }

        assert(nbInLiterals >= nbOutLiterals);
        {
            nuint lastLLSize = nbInLiterals - nbOutLiterals;
            outSeqs[nbInSequences].litLength = (uint)lastLLSize;
            outSeqs[nbInSequences].matchLength = 0;
            outSeqs[nbInSequences].offset = 0;
            assert(nbOutSequences == nbInSequences + 1);
        }

        seqCollector->seqIndex += nbOutSequences;
        assert(seqCollector->seqIndex <= seqCollector->maxSequences);
        return 0;
    }

    /*! ZSTD_sequenceBound() :
     * `srcSize` : size of the input buffer
     *  @return : upper-bound for the number of sequences that can be generated
     *            from a buffer of srcSize bytes
     *
     *  note : returns number of sequences - to get bytes, multiply by sizeof(ZSTD_Sequence).
     */
    public static nuint ZSTD_sequenceBound(nuint srcSize)
    {
        nuint maxNbSeq = srcSize / 3 + 1;
        nuint maxNbDelims = srcSize / (1 << 10) + 1;
        return maxNbSeq + maxNbDelims;
    }

    /*! ZSTD_generateSequences() :
     * WARNING: This function is meant for debugging and informational purposes ONLY!
     * Its implementation is flawed, and it will be deleted in a future version.
     * It is not guaranteed to succeed, as there are several cases where it will give
     * up and fail. You should NOT use this function in production code.
     *
     * This function is deprecated, and will be removed in a future version.
     *
     * Generate sequences using ZSTD_compress2(), given a source buffer.
     *
     * @param zc The compression context to be used for ZSTD_compress2(). Set any
     *           compression parameters you need on this context.
     * @param outSeqs The output sequences buffer of size @p outSeqsSize
     * @param outSeqsCapacity The size of the output sequences buffer.
     *                    ZSTD_sequenceBound(srcSize) is an upper bound on the number
     *                    of sequences that can be generated.
     * @param src The source buffer to generate sequences from of size @p srcSize.
     * @param srcSize The size of the source buffer.
     *
     * Each block will end with a dummy sequence
     * with offset == 0, matchLength == 0, and litLength == length of last literals.
     * litLength may be == 0, and if so, then the sequence of (of: 0 ml: 0 ll: 0)
     * simply acts as a block delimiter.
     *
     * @returns The number of sequences generated, necessarily less than
     *          ZSTD_sequenceBound(srcSize), or an error code that can be checked
     *          with ZSTD_isError().
     */
    public static nuint ZSTD_generateSequences(
        ZSTD_CCtx_s* zc,
        ZSTD_Sequence* outSeqs,
        nuint outSeqsSize,
        void* src,
        nuint srcSize
    )
    {
        nuint dstCapacity = ZSTD_compressBound(srcSize);
        /* Make C90 happy. */
        void* dst;
        SeqCollector seqCollector;
        {
            int targetCBlockSize;
            {
                nuint err_code = ZSTD_CCtx_getParameter(
                    zc,
                    ZSTD_cParameter.ZSTD_c_targetCBlockSize,
                    &targetCBlockSize
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (targetCBlockSize != 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported));
            }
        }

        {
            int nbWorkers;
            {
                nuint err_code = ZSTD_CCtx_getParameter(
                    zc,
                    ZSTD_cParameter.ZSTD_c_nbWorkers,
                    &nbWorkers
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (nbWorkers != 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported));
            }
        }

        dst = ZSTD_customMalloc(dstCapacity, ZSTD_defaultCMem);
        if (dst == null)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }

        seqCollector.collectSequences = 1;
        seqCollector.seqStart = outSeqs;
        seqCollector.seqIndex = 0;
        seqCollector.maxSequences = outSeqsSize;
        zc->seqCollector = seqCollector;
        {
            nuint ret = ZSTD_compress2(zc, dst, dstCapacity, src, srcSize);
            ZSTD_customFree(dst, ZSTD_defaultCMem);
            {
                nuint err_code = ret;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }
        }

        assert(zc->seqCollector.seqIndex <= ZSTD_sequenceBound(srcSize));
        return zc->seqCollector.seqIndex;
    }

    /*! ZSTD_mergeBlockDelimiters() :
     * Given an array of ZSTD_Sequence, remove all sequences that represent block delimiters/last literals
     * by merging them into the literals of the next sequence.
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
        nuint valueST = (nuint)(value * 0x0101010101010101UL);
        nuint unrollSize = (nuint)(sizeof(nuint) * 4);
        nuint unrollMask = unrollSize - 1;
        nuint prefixLength = length & unrollMask;
        nuint i;
        if (length == 1)
            return 1;
        if (prefixLength != 0 && ZSTD_count(ip + 1, ip, ip + prefixLength) != prefixLength - 1)
        {
            return 0;
        }

        for (i = prefixLength; i != length; i += unrollSize)
        {
            nuint u;
            for (u = 0; u < unrollSize; u += (nuint)sizeof(nuint))
            {
                if (MEM_readST(ip + i + u) != valueST)
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
    private static int ZSTD_maybeRLE(SeqStore_t* seqStore)
    {
        nuint nbSeqs = (nuint)(seqStore->sequences - seqStore->sequencesStart);
        nuint nbLits = (nuint)(seqStore->lit - seqStore->litStart);
        return nbSeqs < 4 && nbLits < 10 ? 1 : 0;
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
        uint cBlockHeader =
            cSize == 1
                ? lastBlock + ((uint)blockType_e.bt_rle << 1) + (uint)(blockSize << 3)
                : lastBlock + ((uint)blockType_e.bt_compressed << 1) + (uint)(cSize << 3);
        MEM_writeLE24(op, cBlockHeader);
    }

    /** ZSTD_buildBlockEntropyStats_literals() :
     *  Builds entropy for the literals.
     *  Stores literals block type (raw, rle, compressed, repeat) and
     *  huffman description table to hufMetadata.
     *  Requires ENTROPY_WORKSPACE_SIZE workspace
     * @return : size of huffman description table, or an error code
     */
    private static nuint ZSTD_buildBlockEntropyStats_literals(
        void* src,
        nuint srcSize,
        ZSTD_hufCTables_t* prevHuf,
        ZSTD_hufCTables_t* nextHuf,
        ZSTD_hufCTablesMetadata_t* hufMetadata,
        int literalsCompressionIsDisabled,
        void* workspace,
        nuint wkspSize,
        int hufFlags
    )
    {
        byte* wkspStart = (byte*)workspace;
        byte* wkspEnd = wkspStart + wkspSize;
        byte* countWkspStart = wkspStart;
        uint* countWksp = (uint*)workspace;
        const nuint countWkspSize = (255 + 1) * sizeof(uint);
        byte* nodeWksp = countWkspStart + countWkspSize;
        nuint nodeWkspSize = (nuint)(wkspEnd - nodeWksp);
        uint maxSymbolValue = 255;
        uint huffLog = 11;
        HUF_repeat repeat = prevHuf->repeatMode;
        memcpy(nextHuf, prevHuf, (uint)sizeof(ZSTD_hufCTables_t));
        if (literalsCompressionIsDisabled != 0)
        {
            hufMetadata->hType = SymbolEncodingType_e.set_basic;
            return 0;
        }

        {
            nuint minLitSize = (nuint)(prevHuf->repeatMode == HUF_repeat.HUF_repeat_valid ? 6 : 63);
            if (srcSize <= minLitSize)
            {
                hufMetadata->hType = SymbolEncodingType_e.set_basic;
                return 0;
            }
        }

        {
            nuint largest = HIST_count_wksp(
                countWksp,
                &maxSymbolValue,
                (byte*)src,
                srcSize,
                workspace,
                wkspSize
            );
            {
                nuint err_code = largest;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (largest == srcSize)
            {
                hufMetadata->hType = SymbolEncodingType_e.set_rle;
                return 0;
            }

            if (largest <= (srcSize >> 7) + 4)
            {
                hufMetadata->hType = SymbolEncodingType_e.set_basic;
                return 0;
            }
        }

        if (
            repeat == HUF_repeat.HUF_repeat_check
            && HUF_validateCTable(&prevHuf->CTable.e0, countWksp, maxSymbolValue) == 0
        )
        {
            repeat = HUF_repeat.HUF_repeat_none;
        }

        memset(&nextHuf->CTable.e0, 0, sizeof(ulong) * 257);
        huffLog = HUF_optimalTableLog(
            huffLog,
            srcSize,
            maxSymbolValue,
            nodeWksp,
            nodeWkspSize,
            &nextHuf->CTable.e0,
            countWksp,
            hufFlags
        );
        assert(huffLog <= 11);
        {
            nuint maxBits = HUF_buildCTable_wksp(
                &nextHuf->CTable.e0,
                countWksp,
                maxSymbolValue,
                huffLog,
                nodeWksp,
                nodeWkspSize
            );
            {
                nuint err_code = maxBits;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            huffLog = (uint)maxBits;
        }

        {
            nuint newCSize = HUF_estimateCompressedSize(
                &nextHuf->CTable.e0,
                countWksp,
                maxSymbolValue
            );
            nuint hSize = HUF_writeCTable_wksp(
                hufMetadata->hufDesBuffer,
                sizeof(byte) * 128,
                &nextHuf->CTable.e0,
                maxSymbolValue,
                huffLog,
                nodeWksp,
                nodeWkspSize
            );
            if (repeat != HUF_repeat.HUF_repeat_none)
            {
                nuint oldCSize = HUF_estimateCompressedSize(
                    &prevHuf->CTable.e0,
                    countWksp,
                    maxSymbolValue
                );
                if (oldCSize < srcSize && (oldCSize <= hSize + newCSize || hSize + 12 >= srcSize))
                {
                    memcpy(nextHuf, prevHuf, (uint)sizeof(ZSTD_hufCTables_t));
                    hufMetadata->hType = SymbolEncodingType_e.set_repeat;
                    return 0;
                }
            }

            if (newCSize + hSize >= srcSize)
            {
                memcpy(nextHuf, prevHuf, (uint)sizeof(ZSTD_hufCTables_t));
                hufMetadata->hType = SymbolEncodingType_e.set_basic;
                return 0;
            }

            hufMetadata->hType = SymbolEncodingType_e.set_compressed;
            nextHuf->repeatMode = HUF_repeat.HUF_repeat_check;
            return hSize;
        }
    }

    /* ZSTD_buildDummySequencesStatistics():
     * Returns a ZSTD_symbolEncodingTypeStats_t with all encoding types as set_basic,
     * and updates nextEntropy to the appropriate repeatMode.
     */
    private static ZSTD_symbolEncodingTypeStats_t ZSTD_buildDummySequencesStatistics(
        ZSTD_fseCTables_t* nextEntropy
    )
    {
        ZSTD_symbolEncodingTypeStats_t stats = new ZSTD_symbolEncodingTypeStats_t
        {
            LLtype = (uint)SymbolEncodingType_e.set_basic,
            Offtype = (uint)SymbolEncodingType_e.set_basic,
            MLtype = (uint)SymbolEncodingType_e.set_basic,
            size = 0,
            lastCountSize = 0,
            longOffsets = 0,
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
     * @return : size of fse tables or error code */
    private static nuint ZSTD_buildBlockEntropyStats_sequences(
        SeqStore_t* seqStorePtr,
        ZSTD_fseCTables_t* prevEntropy,
        ZSTD_fseCTables_t* nextEntropy,
        ZSTD_CCtx_params_s* cctxParams,
        ZSTD_fseCTablesMetadata_t* fseMetadata,
        void* workspace,
        nuint wkspSize
    )
    {
        ZSTD_strategy strategy = cctxParams->cParams.strategy;
        nuint nbSeq = (nuint)(seqStorePtr->sequences - seqStorePtr->sequencesStart);
        byte* ostart = fseMetadata->fseTablesBuffer;
        byte* oend = ostart + sizeof(byte) * 133;
        byte* op = ostart;
        uint* countWorkspace = (uint*)workspace;
        uint* entropyWorkspace = countWorkspace + (52 + 1);
        nuint entropyWorkspaceSize = wkspSize - (52 + 1) * sizeof(uint);
        ZSTD_symbolEncodingTypeStats_t stats;
        stats =
            nbSeq != 0
                ? ZSTD_buildSequencesStatistics(
                    seqStorePtr,
                    nbSeq,
                    prevEntropy,
                    nextEntropy,
                    op,
                    oend,
                    strategy,
                    countWorkspace,
                    entropyWorkspace,
                    entropyWorkspaceSize
                )
                : ZSTD_buildDummySequencesStatistics(nextEntropy);
        {
            nuint err_code = stats.size;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        fseMetadata->llType = (SymbolEncodingType_e)stats.LLtype;
        fseMetadata->ofType = (SymbolEncodingType_e)stats.Offtype;
        fseMetadata->mlType = (SymbolEncodingType_e)stats.MLtype;
        fseMetadata->lastCountSize = stats.lastCountSize;
        return stats.size;
    }

    /** ZSTD_buildBlockEntropyStats() :
     *  Builds entropy for the block.
     *  Requires workspace size ENTROPY_WORKSPACE_SIZE
     * @return : 0 on success, or an error code
     *  Note : also employed in superblock
     */
    private static nuint ZSTD_buildBlockEntropyStats(
        SeqStore_t* seqStorePtr,
        ZSTD_entropyCTables_t* prevEntropy,
        ZSTD_entropyCTables_t* nextEntropy,
        ZSTD_CCtx_params_s* cctxParams,
        ZSTD_entropyCTablesMetadata_t* entropyMetadata,
        void* workspace,
        nuint wkspSize
    )
    {
        nuint litSize = (nuint)(seqStorePtr->lit - seqStorePtr->litStart);
        int huf_useOptDepth = cctxParams->cParams.strategy >= ZSTD_strategy.ZSTD_btultra ? 1 : 0;
        int hufFlags = huf_useOptDepth != 0 ? (int)HUF_flags_e.HUF_flags_optimalDepth : 0;
        entropyMetadata->hufMetadata.hufDesSize = ZSTD_buildBlockEntropyStats_literals(
            seqStorePtr->litStart,
            litSize,
            &prevEntropy->huf,
            &nextEntropy->huf,
            &entropyMetadata->hufMetadata,
            ZSTD_literalsCompressionIsDisabled(cctxParams),
            workspace,
            wkspSize,
            hufFlags
        );
        {
            nuint err_code = entropyMetadata->hufMetadata.hufDesSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        entropyMetadata->fseMetadata.fseTablesSize = ZSTD_buildBlockEntropyStats_sequences(
            seqStorePtr,
            &prevEntropy->fse,
            &nextEntropy->fse,
            cctxParams,
            &entropyMetadata->fseMetadata,
            workspace,
            wkspSize
        );
        {
            nuint err_code = entropyMetadata->fseMetadata.fseTablesSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return 0;
    }

    /* Returns the size estimate for the literals section (header + content) of a block */
    private static nuint ZSTD_estimateBlockSize_literal(
        byte* literals,
        nuint litSize,
        ZSTD_hufCTables_t* huf,
        ZSTD_hufCTablesMetadata_t* hufMetadata,
        void* workspace,
        nuint wkspSize,
        int writeEntropy
    )
    {
        uint* countWksp = (uint*)workspace;
        uint maxSymbolValue = 255;
        nuint literalSectionHeaderSize = (nuint)(
            3 + (litSize >= 1 * (1 << 10) ? 1 : 0) + (litSize >= 16 * (1 << 10) ? 1 : 0)
        );
        uint singleStream = litSize < 256 ? 1U : 0U;
        if (hufMetadata->hType == SymbolEncodingType_e.set_basic)
            return litSize;
        else if (hufMetadata->hType == SymbolEncodingType_e.set_rle)
            return 1;
        else if (
            hufMetadata->hType == SymbolEncodingType_e.set_compressed
            || hufMetadata->hType == SymbolEncodingType_e.set_repeat
        )
        {
            nuint largest = HIST_count_wksp(
                countWksp,
                &maxSymbolValue,
                literals,
                litSize,
                workspace,
                wkspSize
            );
            if (ERR_isError(largest))
                return litSize;
            {
                nuint cLitSizeEstimate = HUF_estimateCompressedSize(
                    &huf->CTable.e0,
                    countWksp,
                    maxSymbolValue
                );
                if (writeEntropy != 0)
                    cLitSizeEstimate += hufMetadata->hufDesSize;
                if (singleStream == 0)
                    cLitSizeEstimate += 6;
                return cLitSizeEstimate + literalSectionHeaderSize;
            }
        }

        assert(0 != 0);
        return 0;
    }

    /* Returns the size estimate for the FSE-compressed symbols (of, ml, ll) of a block */
    private static nuint ZSTD_estimateBlockSize_symbolType(
        SymbolEncodingType_e type,
        byte* codeTable,
        nuint nbSeq,
        uint maxCode,
        uint* fseCTable,
        byte* additionalBits,
        short* defaultNorm,
        uint defaultNormLog,
        uint defaultMax,
        void* workspace,
        nuint wkspSize
    )
    {
        uint* countWksp = (uint*)workspace;
        byte* ctp = codeTable;
        byte* ctStart = ctp;
        byte* ctEnd = ctStart + nbSeq;
        nuint cSymbolTypeSizeEstimateInBits = 0;
        uint max = maxCode;
        HIST_countFast_wksp(countWksp, &max, codeTable, nbSeq, workspace, wkspSize);
        if (type == SymbolEncodingType_e.set_basic)
        {
            assert(max <= defaultMax);
            cSymbolTypeSizeEstimateInBits = ZSTD_crossEntropyCost(
                defaultNorm,
                defaultNormLog,
                countWksp,
                max
            );
        }
        else if (type == SymbolEncodingType_e.set_rle)
        {
            cSymbolTypeSizeEstimateInBits = 0;
        }
        else if (
            type == SymbolEncodingType_e.set_compressed
            || type == SymbolEncodingType_e.set_repeat
        )
        {
            cSymbolTypeSizeEstimateInBits = ZSTD_fseBitCost(fseCTable, countWksp, max);
        }

        if (ERR_isError(cSymbolTypeSizeEstimateInBits))
        {
            return nbSeq * 10;
        }

        while (ctp < ctEnd)
        {
            if (additionalBits != null)
                cSymbolTypeSizeEstimateInBits += additionalBits[*ctp];
            else
                cSymbolTypeSizeEstimateInBits += *ctp;
            ctp++;
        }

        return cSymbolTypeSizeEstimateInBits >> 3;
    }

    /* Returns the size estimate for the sequences section (header + content) of a block */
    private static nuint ZSTD_estimateBlockSize_sequences(
        byte* ofCodeTable,
        byte* llCodeTable,
        byte* mlCodeTable,
        nuint nbSeq,
        ZSTD_fseCTables_t* fseTables,
        ZSTD_fseCTablesMetadata_t* fseMetadata,
        void* workspace,
        nuint wkspSize,
        int writeEntropy
    )
    {
        /* seqHead */
        nuint sequencesSectionHeaderSize = (nuint)(
            1 + 1 + (nbSeq >= 128 ? 1 : 0) + (nbSeq >= 0x7F00 ? 1 : 0)
        );
        nuint cSeqSizeEstimate = 0;
        cSeqSizeEstimate += ZSTD_estimateBlockSize_symbolType(
            fseMetadata->ofType,
            ofCodeTable,
            nbSeq,
            31,
            fseTables->offcodeCTable,
            null,
            OF_defaultNorm,
            OF_defaultNormLog,
            28,
            workspace,
            wkspSize
        );
        cSeqSizeEstimate += ZSTD_estimateBlockSize_symbolType(
            fseMetadata->llType,
            llCodeTable,
            nbSeq,
            35,
            fseTables->litlengthCTable,
            LL_bits,
            LL_defaultNorm,
            LL_defaultNormLog,
            35,
            workspace,
            wkspSize
        );
        cSeqSizeEstimate += ZSTD_estimateBlockSize_symbolType(
            fseMetadata->mlType,
            mlCodeTable,
            nbSeq,
            52,
            fseTables->matchlengthCTable,
            ML_bits,
            ML_defaultNorm,
            ML_defaultNormLog,
            52,
            workspace,
            wkspSize
        );
        if (writeEntropy != 0)
            cSeqSizeEstimate += fseMetadata->fseTablesSize;
        return cSeqSizeEstimate + sequencesSectionHeaderSize;
    }

    /* Returns the size estimate for a given stream of literals, of, ll, ml */
    private static nuint ZSTD_estimateBlockSize(
        byte* literals,
        nuint litSize,
        byte* ofCodeTable,
        byte* llCodeTable,
        byte* mlCodeTable,
        nuint nbSeq,
        ZSTD_entropyCTables_t* entropy,
        ZSTD_entropyCTablesMetadata_t* entropyMetadata,
        void* workspace,
        nuint wkspSize,
        int writeLitEntropy,
        int writeSeqEntropy
    )
    {
        nuint literalsSize = ZSTD_estimateBlockSize_literal(
            literals,
            litSize,
            &entropy->huf,
            &entropyMetadata->hufMetadata,
            workspace,
            wkspSize,
            writeLitEntropy
        );
        nuint seqSize = ZSTD_estimateBlockSize_sequences(
            ofCodeTable,
            llCodeTable,
            mlCodeTable,
            nbSeq,
            &entropy->fse,
            &entropyMetadata->fseMetadata,
            workspace,
            wkspSize,
            writeSeqEntropy
        );
        return seqSize + literalsSize + ZSTD_blockHeaderSize;
    }

    /* Builds entropy statistics and uses them for blocksize estimation.
     *
     * @return: estimated compressed size of the seqStore, or a zstd error.
     */
    private static nuint ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(
        SeqStore_t* seqStore,
        ZSTD_CCtx_s* zc
    )
    {
        ZSTD_entropyCTablesMetadata_t* entropyMetadata = &zc->blockSplitCtx.entropyMetadata;
        {
            nuint err_code = ZSTD_buildBlockEntropyStats(
                seqStore,
                &zc->blockState.prevCBlock->entropy,
                &zc->blockState.nextCBlock->entropy,
                &zc->appliedParams,
                entropyMetadata,
                zc->tmpWorkspace,
                zc->tmpWkspSize
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return ZSTD_estimateBlockSize(
            seqStore->litStart,
            (nuint)(seqStore->lit - seqStore->litStart),
            seqStore->ofCode,
            seqStore->llCode,
            seqStore->mlCode,
            (nuint)(seqStore->sequences - seqStore->sequencesStart),
            &zc->blockState.nextCBlock->entropy,
            entropyMetadata,
            zc->tmpWorkspace,
            zc->tmpWkspSize,
            entropyMetadata->hufMetadata.hType == SymbolEncodingType_e.set_compressed ? 1 : 0,
            1
        );
    }

    /* Returns literals bytes represented in a seqStore */
    private static nuint ZSTD_countSeqStoreLiteralsBytes(SeqStore_t* seqStore)
    {
        nuint literalsBytes = 0;
        nuint nbSeqs = (nuint)(seqStore->sequences - seqStore->sequencesStart);
        nuint i;
        for (i = 0; i < nbSeqs; ++i)
        {
            SeqDef_s seq = seqStore->sequencesStart[i];
            literalsBytes += seq.litLength;
            if (
                i == seqStore->longLengthPos
                && seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_literalLength
            )
            {
                literalsBytes += 0x10000;
            }
        }

        return literalsBytes;
    }

    /* Returns match bytes represented in a seqStore */
    private static nuint ZSTD_countSeqStoreMatchBytes(SeqStore_t* seqStore)
    {
        nuint matchBytes = 0;
        nuint nbSeqs = (nuint)(seqStore->sequences - seqStore->sequencesStart);
        nuint i;
        for (i = 0; i < nbSeqs; ++i)
        {
            SeqDef_s seq = seqStore->sequencesStart[i];
            matchBytes += (nuint)(seq.mlBase + 3);
            if (
                i == seqStore->longLengthPos
                && seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_matchLength
            )
            {
                matchBytes += 0x10000;
            }
        }

        return matchBytes;
    }

    /* Derives the seqStore that is a chunk of the originalSeqStore from [startIdx, endIdx).
     * Stores the result in resultSeqStore.
     */
    private static void ZSTD_deriveSeqStoreChunk(
        SeqStore_t* resultSeqStore,
        SeqStore_t* originalSeqStore,
        nuint startIdx,
        nuint endIdx
    )
    {
        *resultSeqStore = *originalSeqStore;
        if (startIdx > 0)
        {
            resultSeqStore->sequences = originalSeqStore->sequencesStart + startIdx;
            resultSeqStore->litStart += ZSTD_countSeqStoreLiteralsBytes(resultSeqStore);
        }

        if (originalSeqStore->longLengthType != ZSTD_longLengthType_e.ZSTD_llt_none)
        {
            if (
                originalSeqStore->longLengthPos < startIdx
                || originalSeqStore->longLengthPos > endIdx
            )
            {
                resultSeqStore->longLengthType = ZSTD_longLengthType_e.ZSTD_llt_none;
            }
            else
            {
                resultSeqStore->longLengthPos -= (uint)startIdx;
            }
        }

        resultSeqStore->sequencesStart = originalSeqStore->sequencesStart + startIdx;
        resultSeqStore->sequences = originalSeqStore->sequencesStart + endIdx;
        if (endIdx == (nuint)(originalSeqStore->sequences - originalSeqStore->sequencesStart))
        {
            assert(resultSeqStore->lit == originalSeqStore->lit);
        }
        else
        {
            nuint literalsBytes = ZSTD_countSeqStoreLiteralsBytes(resultSeqStore);
            resultSeqStore->lit = resultSeqStore->litStart + literalsBytes;
        }

        resultSeqStore->llCode += startIdx;
        resultSeqStore->mlCode += startIdx;
        resultSeqStore->ofCode += startIdx;
    }

    /**
     * Returns the raw offset represented by the combination of offBase, ll0, and repcode history.
     * offBase must represent a repcode in the numeric representation of ZSTD_storeSeq().
     */
    private static uint ZSTD_resolveRepcodeToRawOffset(uint* rep, uint offBase, uint ll0)
    {
        assert(1 <= offBase && offBase <= 3);
        /* [ 0 - 3 ] */
        uint adjustedRepCode = offBase - 1 + ll0;
        assert(1 <= offBase && offBase <= 3);
        if (adjustedRepCode == 3)
        {
            assert(ll0 != 0);
            return rep[0] - 1;
        }

        return rep[adjustedRepCode];
    }

    /**
     * ZSTD_seqStore_resolveOffCodes() reconciles any possible divergences in offset history that may arise
     * due to emission of RLE/raw blocks that disturb the offset history,
     * and replaces any repcodes within the seqStore that may be invalid.
     *
     * dRepcodes are updated as would be on the decompression side.
     * cRepcodes are updated exactly in accordance with the seqStore.
     *
     * Note : this function assumes seq->offBase respects the following numbering scheme :
     *        0 : invalid
     *        1-3 : repcode 1-3
     *        4+ : real_offset+3
     */
    private static void ZSTD_seqStore_resolveOffCodes(
        repcodes_s* dRepcodes,
        repcodes_s* cRepcodes,
        SeqStore_t* seqStore,
        uint nbSeq
    )
    {
        uint idx = 0;
        uint longLitLenIdx =
            seqStore->longLengthType == ZSTD_longLengthType_e.ZSTD_llt_literalLength
                ? seqStore->longLengthPos
                : nbSeq;
        for (; idx < nbSeq; ++idx)
        {
            SeqDef_s* seq = seqStore->sequencesStart + idx;
            uint ll0 = seq->litLength == 0 && idx != longLitLenIdx ? 1U : 0U;
            uint offBase = seq->offBase;
            assert(offBase > 0);
            if (1 <= offBase && offBase <= 3)
            {
                uint dRawOffset = ZSTD_resolveRepcodeToRawOffset(dRepcodes->rep, offBase, ll0);
                uint cRawOffset = ZSTD_resolveRepcodeToRawOffset(cRepcodes->rep, offBase, ll0);
                if (dRawOffset != cRawOffset)
                {
                    assert(cRawOffset > 0);
                    seq->offBase = cRawOffset + 3;
                }
            }

            ZSTD_updateRep(dRepcodes->rep, seq->offBase, ll0);
            ZSTD_updateRep(cRepcodes->rep, offBase, ll0);
        }
    }

    /* ZSTD_compressSeqStore_singleBlock():
     * Compresses a seqStore into a block with a block header, into the buffer dst.
     *
     * Returns the total size of that block (including header) or a ZSTD error code.
     */
    private static nuint ZSTD_compressSeqStore_singleBlock(
        ZSTD_CCtx_s* zc,
        SeqStore_t* seqStore,
        repcodes_s* dRep,
        repcodes_s* cRep,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint lastBlock,
        uint isPartition
    )
    {
        const uint rleMaxLength = 25;
        byte* op = (byte*)dst;
        byte* ip = (byte*)src;
        nuint cSize;
        nuint cSeqsSize;
        /* In case of an RLE or raw block, the simulated decompression repcode history must be reset */
        repcodes_s dRepOriginal = *dRep;
        if (isPartition != 0)
            ZSTD_seqStore_resolveOffCodes(
                dRep,
                cRep,
                seqStore,
                (uint)(seqStore->sequences - seqStore->sequencesStart)
            );
        if (dstCapacity < ZSTD_blockHeaderSize)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        cSeqsSize = ZSTD_entropyCompressSeqStore(
            seqStore,
            &zc->blockState.prevCBlock->entropy,
            &zc->blockState.nextCBlock->entropy,
            &zc->appliedParams,
            op + ZSTD_blockHeaderSize,
            dstCapacity - ZSTD_blockHeaderSize,
            srcSize,
            zc->tmpWorkspace,
            zc->tmpWkspSize,
            zc->bmi2
        );
        {
            nuint err_code = cSeqsSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        if (
            zc->isFirstBlock == 0
            && cSeqsSize < rleMaxLength
            && ZSTD_isRLE((byte*)src, srcSize) != 0
        )
        {
            cSeqsSize = 1;
        }

        if (zc->seqCollector.collectSequences != 0)
        {
            {
                nuint err_code = ZSTD_copyBlockSequences(
                    &zc->seqCollector,
                    seqStore,
                    dRepOriginal.rep
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
            return 0;
        }

        if (cSeqsSize == 0)
        {
            cSize = ZSTD_noCompressBlock(op, dstCapacity, ip, srcSize, lastBlock);
            {
                nuint err_code = cSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            *dRep = dRepOriginal;
        }
        else if (cSeqsSize == 1)
        {
            cSize = ZSTD_rleCompressBlock(op, dstCapacity, *ip, srcSize, lastBlock);
            {
                nuint err_code = cSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            *dRep = dRepOriginal;
        }
        else
        {
            ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
            writeBlockHeader(op, cSeqsSize, srcSize, lastBlock);
            cSize = ZSTD_blockHeaderSize + cSeqsSize;
        }

        if (
            zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid
        )
            zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
        return cSize;
    }

    /* Helper function to perform the recursive search for block splits.
     * Estimates the cost of seqStore prior to split, and estimates the cost of splitting the sequences in half.
     * If advantageous to split, then we recurse down the two sub-blocks.
     * If not, or if an error occurred in estimation, then we do not recurse.
     *
     * Note: The recursion depth is capped by a heuristic minimum number of sequences,
     * defined by MIN_SEQUENCES_BLOCK_SPLITTING.
     * In theory, this means the absolute largest recursion depth is 10 == log2(maxNbSeqInBlock/MIN_SEQUENCES_BLOCK_SPLITTING).
     * In practice, recursion depth usually doesn't go beyond 4.
     *
     * Furthermore, the number of splits is capped by ZSTD_MAX_NB_BLOCK_SPLITS.
     * At ZSTD_MAX_NB_BLOCK_SPLITS == 196 with the current existing blockSize
     * maximum of 128 KB, this value is actually impossible to reach.
     */
    private static void ZSTD_deriveBlockSplitsHelper(
        seqStoreSplits* splits,
        nuint startIdx,
        nuint endIdx,
        ZSTD_CCtx_s* zc,
        SeqStore_t* origSeqStore
    )
    {
        SeqStore_t* fullSeqStoreChunk = &zc->blockSplitCtx.fullSeqStoreChunk;
        SeqStore_t* firstHalfSeqStore = &zc->blockSplitCtx.firstHalfSeqStore;
        SeqStore_t* secondHalfSeqStore = &zc->blockSplitCtx.secondHalfSeqStore;
        nuint estimatedOriginalSize;
        nuint estimatedFirstHalfSize;
        nuint estimatedSecondHalfSize;
        nuint midIdx = (startIdx + endIdx) / 2;
        assert(endIdx >= startIdx);
        if (endIdx - startIdx < 300 || splits->idx >= 196)
        {
            return;
        }

        ZSTD_deriveSeqStoreChunk(fullSeqStoreChunk, origSeqStore, startIdx, endIdx);
        ZSTD_deriveSeqStoreChunk(firstHalfSeqStore, origSeqStore, startIdx, midIdx);
        ZSTD_deriveSeqStoreChunk(secondHalfSeqStore, origSeqStore, midIdx, endIdx);
        estimatedOriginalSize = ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(
            fullSeqStoreChunk,
            zc
        );
        estimatedFirstHalfSize = ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(
            firstHalfSeqStore,
            zc
        );
        estimatedSecondHalfSize = ZSTD_buildEntropyStatisticsAndEstimateSubBlockSize(
            secondHalfSeqStore,
            zc
        );
        if (
            ERR_isError(estimatedOriginalSize)
            || ERR_isError(estimatedFirstHalfSize)
            || ERR_isError(estimatedSecondHalfSize)
        )
        {
            return;
        }

        if (estimatedFirstHalfSize + estimatedSecondHalfSize < estimatedOriginalSize)
        {
            ZSTD_deriveBlockSplitsHelper(splits, startIdx, midIdx, zc, origSeqStore);
            splits->splitLocations[splits->idx] = (uint)midIdx;
            splits->idx++;
            ZSTD_deriveBlockSplitsHelper(splits, midIdx, endIdx, zc, origSeqStore);
        }
    }

    /* Base recursive function.
     * Populates a table with intra-block partition indices that can improve compression ratio.
     *
     * @return: number of splits made (which equals the size of the partition table - 1).
     */
    private static nuint ZSTD_deriveBlockSplits(ZSTD_CCtx_s* zc, uint* partitions, uint nbSeq)
    {
        seqStoreSplits splits;
        splits.splitLocations = partitions;
        splits.idx = 0;
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
    private static nuint ZSTD_compressBlock_splitBlock_internal(
        ZSTD_CCtx_s* zc,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint blockSize,
        uint lastBlock,
        uint nbSeq
    )
    {
        nuint cSize = 0;
        byte* ip = (byte*)src;
        byte* op = (byte*)dst;
        nuint i = 0;
        nuint srcBytesTotal = 0;
        /* size == ZSTD_MAX_NB_BLOCK_SPLITS */
        uint* partitions = zc->blockSplitCtx.partitions;
        SeqStore_t* nextSeqStore = &zc->blockSplitCtx.nextSeqStore;
        SeqStore_t* currSeqStore = &zc->blockSplitCtx.currSeqStore;
        nuint numSplits = ZSTD_deriveBlockSplits(zc, partitions, nbSeq);
        /* If a block is split and some partitions are emitted as RLE/uncompressed, then repcode history
         * may become invalid. In order to reconcile potentially invalid repcodes, we keep track of two
         * separate repcode histories that simulate repcode history on compression and decompression side,
         * and use the histories to determine whether we must replace a particular repcode with its raw offset.
         *
         * 1) cRep gets updated for each partition, regardless of whether the block was emitted as uncompressed
         *    or RLE. This allows us to retrieve the offset value that an invalid repcode references within
         *    a nocompress/RLE block.
         * 2) dRep gets updated only for compressed partitions, and when a repcode gets replaced, will use
         *    the replacement offset value rather than the original repcode to update the repcode history.
         *    dRep also will be the final repcode history sent to the next block.
         *
         * See ZSTD_seqStore_resolveOffCodes() for more details.
         */
        repcodes_s dRep;
        repcodes_s cRep;
        memcpy(dRep.rep, zc->blockState.prevCBlock->rep, (uint)sizeof(repcodes_s));
        memcpy(cRep.rep, zc->blockState.prevCBlock->rep, (uint)sizeof(repcodes_s));
        *nextSeqStore = new SeqStore_t();
        if (numSplits == 0)
        {
            nuint cSizeSingleBlock = ZSTD_compressSeqStore_singleBlock(
                zc,
                &zc->seqStore,
                &dRep,
                &cRep,
                op,
                dstCapacity,
                ip,
                blockSize,
                lastBlock,
                0
            );
            {
                nuint err_code = cSizeSingleBlock;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(zc->blockSizeMax <= 1 << 17);
            assert(cSizeSingleBlock <= zc->blockSizeMax + ZSTD_blockHeaderSize);
            return cSizeSingleBlock;
        }

        ZSTD_deriveSeqStoreChunk(currSeqStore, &zc->seqStore, 0, partitions[0]);
        for (i = 0; i <= numSplits; ++i)
        {
            nuint cSizeChunk;
            uint lastPartition = i == numSplits ? 1U : 0U;
            uint lastBlockEntireSrc = 0;
            nuint srcBytes =
                ZSTD_countSeqStoreLiteralsBytes(currSeqStore)
                + ZSTD_countSeqStoreMatchBytes(currSeqStore);
            srcBytesTotal += srcBytes;
            if (lastPartition != 0)
            {
                srcBytes += blockSize - srcBytesTotal;
                lastBlockEntireSrc = lastBlock;
            }
            else
            {
                ZSTD_deriveSeqStoreChunk(
                    nextSeqStore,
                    &zc->seqStore,
                    partitions[i],
                    partitions[i + 1]
                );
            }

            cSizeChunk = ZSTD_compressSeqStore_singleBlock(
                zc,
                currSeqStore,
                &dRep,
                &cRep,
                op,
                dstCapacity,
                ip,
                srcBytes,
                lastBlockEntireSrc,
                1
            );
            {
                nuint err_code = cSizeChunk;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            ip += srcBytes;
            op += cSizeChunk;
            dstCapacity -= cSizeChunk;
            cSize += cSizeChunk;
            *currSeqStore = *nextSeqStore;
            assert(cSizeChunk <= zc->blockSizeMax + ZSTD_blockHeaderSize);
        }

        memcpy(zc->blockState.prevCBlock->rep, dRep.rep, (uint)sizeof(repcodes_s));
        return cSize;
    }

    private static nuint ZSTD_compressBlock_splitBlock(
        ZSTD_CCtx_s* zc,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint lastBlock
    )
    {
        uint nbSeq;
        nuint cSize;
        assert(zc->appliedParams.postBlockSplitter == ZSTD_paramSwitch_e.ZSTD_ps_enable);
        {
            nuint bss = ZSTD_buildSeqStore(zc, src, srcSize);
            {
                nuint err_code = bss;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (bss == (nuint)ZSTD_BuildSeqStore_e.ZSTDbss_noCompress)
            {
                if (
                    zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode
                    == FSE_repeat.FSE_repeat_valid
                )
                    zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode =
                        FSE_repeat.FSE_repeat_check;
                if (zc->seqCollector.collectSequences != 0)
                {
                    return unchecked(
                        (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_sequenceProducer_failed)
                    );
                }

                cSize = ZSTD_noCompressBlock(dst, dstCapacity, src, srcSize, lastBlock);
                {
                    nuint err_code = cSize;
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                return cSize;
            }

            nbSeq = (uint)(zc->seqStore.sequences - zc->seqStore.sequencesStart);
        }

        cSize = ZSTD_compressBlock_splitBlock_internal(
            zc,
            dst,
            dstCapacity,
            src,
            srcSize,
            lastBlock,
            nbSeq
        );
        {
            nuint err_code = cSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return cSize;
    }

    private static nuint ZSTD_compressBlock_internal(
        ZSTD_CCtx_s* zc,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint frame
    )
    {
        /* This is an estimated upper bound for the length of an rle block.
         * This isn't the actual upper bound.
         * Finding the real threshold needs further investigation.
         */
        const uint rleMaxLength = 25;
        nuint cSize;
        byte* ip = (byte*)src;
        byte* op = (byte*)dst;
        {
            nuint bss = ZSTD_buildSeqStore(zc, src, srcSize);
            {
                nuint err_code = bss;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (bss == (nuint)ZSTD_BuildSeqStore_e.ZSTDbss_noCompress)
            {
                if (zc->seqCollector.collectSequences != 0)
                {
                    return unchecked(
                        (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_sequenceProducer_failed)
                    );
                }

                cSize = 0;
                goto @out;
            }
        }

        if (zc->seqCollector.collectSequences != 0)
        {
            {
                nuint err_code = ZSTD_copyBlockSequences(
                    &zc->seqCollector,
                    ZSTD_getSeqStore(zc),
                    zc->blockState.prevCBlock->rep
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
            return 0;
        }

        cSize = ZSTD_entropyCompressSeqStore(
            &zc->seqStore,
            &zc->blockState.prevCBlock->entropy,
            &zc->blockState.nextCBlock->entropy,
            &zc->appliedParams,
            dst,
            dstCapacity,
            srcSize,
            zc->tmpWorkspace,
            zc->tmpWkspSize,
            zc->bmi2
        );
        if (
            frame != 0
            && zc->isFirstBlock == 0
            && cSize < rleMaxLength
            && ZSTD_isRLE(ip, srcSize) != 0
        )
        {
            cSize = 1;
            op[0] = ip[0];
        }

        @out:
        if (!ERR_isError(cSize) && cSize > 1)
        {
            ZSTD_blockState_confirmRepcodesAndEntropyTables(&zc->blockState);
        }

        if (
            zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid
        )
            zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
        return cSize;
    }

    private static nuint ZSTD_compressBlock_targetCBlockSize_body(
        ZSTD_CCtx_s* zc,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        nuint bss,
        uint lastBlock
    )
    {
        if (bss == (nuint)ZSTD_BuildSeqStore_e.ZSTDbss_compress)
        {
            if (
                zc->isFirstBlock == 0
                && ZSTD_maybeRLE(&zc->seqStore) != 0
                && ZSTD_isRLE((byte*)src, srcSize) != 0
            )
            {
                return ZSTD_rleCompressBlock(dst, dstCapacity, *(byte*)src, srcSize, lastBlock);
            }

            {
                nuint cSize = ZSTD_compressSuperBlock(
                    zc,
                    dst,
                    dstCapacity,
                    src,
                    srcSize,
                    lastBlock
                );
                if (cSize != unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)))
                {
                    nuint maxCSize =
                        srcSize - ZSTD_minGain(srcSize, zc->appliedParams.cParams.strategy);
                    {
                        nuint err_code = cSize;
                        if (ERR_isError(err_code))
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

    private static nuint ZSTD_compressBlock_targetCBlockSize(
        ZSTD_CCtx_s* zc,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint lastBlock
    )
    {
        nuint cSize = 0;
        nuint bss = ZSTD_buildSeqStore(zc, src, srcSize);
        {
            nuint err_code = bss;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        cSize = ZSTD_compressBlock_targetCBlockSize_body(
            zc,
            dst,
            dstCapacity,
            src,
            srcSize,
            bss,
            lastBlock
        );
        {
            nuint err_code = cSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        if (
            zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode == FSE_repeat.FSE_repeat_valid
        )
            zc->blockState.prevCBlock->entropy.fse.offcode_repeatMode = FSE_repeat.FSE_repeat_check;
        return cSize;
    }

    private static void ZSTD_overflowCorrectIfNeeded(
        ZSTD_MatchState_t* ms,
        ZSTD_cwksp* ws,
        ZSTD_CCtx_params_s* @params,
        void* ip,
        void* iend
    )
    {
        uint cycleLog = ZSTD_cycleLog(@params->cParams.chainLog, @params->cParams.strategy);
        uint maxDist = (uint)1 << (int)@params->cParams.windowLog;
        if (
            ZSTD_window_needOverflowCorrection(
                ms->window,
                cycleLog,
                maxDist,
                ms->loadedDictEnd,
                ip,
                iend
            ) != 0
        )
        {
            uint correction = ZSTD_window_correctOverflow(&ms->window, cycleLog, maxDist, ip);
            ZSTD_cwksp_mark_tables_dirty(ws);
            ZSTD_reduceIndex(ms, @params, correction);
            ZSTD_cwksp_mark_tables_clean(ws);
            if (ms->nextToUpdate < correction)
                ms->nextToUpdate = 0;
            else
                ms->nextToUpdate -= correction;
            ms->loadedDictEnd = 0;
            ms->dictMatchState = null;
        }
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<int> Span_splitLevels =>
        new int[10] { 0, 0, 1, 2, 2, 3, 3, 4, 4, 4 };
    private static int* splitLevels =>
        (int*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_splitLevels)
            );
#else

    private static readonly int* splitLevels = GetArrayPointer(
        new int[10] { 0, 0, 1, 2, 2, 3, 3, 4, 4, 4 }
    );
#endif

    private static nuint ZSTD_optimalBlockSize(
        ZSTD_CCtx_s* cctx,
        void* src,
        nuint srcSize,
        nuint blockSizeMax,
        int splitLevel,
        ZSTD_strategy strat,
        long savings
    )
    {
        if (srcSize < 128 * (1 << 10) || blockSizeMax < 128 * (1 << 10))
            return srcSize < blockSizeMax ? srcSize : blockSizeMax;
        if (savings < 3)
        {
            return 128 * (1 << 10);
        }

        if (splitLevel == 1)
            return 128 * (1 << 10);
        if (splitLevel == 0)
        {
            assert(ZSTD_strategy.ZSTD_fast <= strat && strat <= ZSTD_strategy.ZSTD_btultra2);
            splitLevel = splitLevels[(int)strat];
        }
        else
        {
            assert(2 <= splitLevel && splitLevel <= 6);
            splitLevel -= 2;
        }

        return ZSTD_splitBlock(
            src,
            blockSizeMax,
            splitLevel,
            cctx->tmpWorkspace,
            cctx->tmpWkspSize
        );
    }

    /*! ZSTD_compress_frameChunk() :
     *   Compress a chunk of data into one or multiple blocks.
     *   All blocks will be terminated, all input will be consumed.
     *   Function will issue an error if there is not enough `dstCapacity` to hold the compressed content.
     *   Frame is supposed already started (header already produced)
     *  @return : compressed size, or an error code
     */
    private static nuint ZSTD_compress_frameChunk(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint lastFrameChunk
    )
    {
        nuint blockSizeMax = cctx->blockSizeMax;
        nuint remaining = srcSize;
        byte* ip = (byte*)src;
        byte* ostart = (byte*)dst;
        byte* op = ostart;
        uint maxDist = (uint)1 << (int)cctx->appliedParams.cParams.windowLog;
        long savings = (long)cctx->consumedSrcSize - (long)cctx->producedCSize;
        assert(cctx->appliedParams.cParams.windowLog <= (uint)(sizeof(nuint) == 4 ? 30 : 31));
        if (cctx->appliedParams.fParams.checksumFlag != 0 && srcSize != 0)
            ZSTD_XXH64_update(&cctx->xxhState, src, srcSize);
        while (remaining != 0)
        {
            ZSTD_MatchState_t* ms = &cctx->blockState.matchState;
            nuint blockSize = ZSTD_optimalBlockSize(
                cctx,
                ip,
                remaining,
                blockSizeMax,
                cctx->appliedParams.preBlockSplitter_level,
                cctx->appliedParams.cParams.strategy,
                savings
            );
            uint lastBlock = lastFrameChunk & (uint)(blockSize == remaining ? 1 : 0);
            assert(blockSize <= remaining);
            if (dstCapacity < ZSTD_blockHeaderSize + (nuint)(1 + 1) + 1)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            ZSTD_overflowCorrectIfNeeded(
                ms,
                &cctx->workspace,
                &cctx->appliedParams,
                ip,
                ip + blockSize
            );
            ZSTD_checkDictValidity(
                &ms->window,
                ip + blockSize,
                maxDist,
                &ms->loadedDictEnd,
                &ms->dictMatchState
            );
            ZSTD_window_enforceMaxDist(
                &ms->window,
                ip,
                maxDist,
                &ms->loadedDictEnd,
                &ms->dictMatchState
            );
            if (ms->nextToUpdate < ms->window.lowLimit)
                ms->nextToUpdate = ms->window.lowLimit;
            {
                nuint cSize;
                if (ZSTD_useTargetCBlockSize(&cctx->appliedParams) != 0)
                {
                    cSize = ZSTD_compressBlock_targetCBlockSize(
                        cctx,
                        op,
                        dstCapacity,
                        ip,
                        blockSize,
                        lastBlock
                    );
                    {
                        nuint err_code = cSize;
                        if (ERR_isError(err_code))
                        {
                            return err_code;
                        }
                    }

                    assert(cSize > 0);
                    assert(cSize <= blockSize + ZSTD_blockHeaderSize);
                }
                else if (ZSTD_blockSplitterEnabled(&cctx->appliedParams) != 0)
                {
                    cSize = ZSTD_compressBlock_splitBlock(
                        cctx,
                        op,
                        dstCapacity,
                        ip,
                        blockSize,
                        lastBlock
                    );
                    {
                        nuint err_code = cSize;
                        if (ERR_isError(err_code))
                        {
                            return err_code;
                        }
                    }

                    assert(cSize > 0 || cctx->seqCollector.collectSequences == 1);
                }
                else
                {
                    cSize = ZSTD_compressBlock_internal(
                        cctx,
                        op + ZSTD_blockHeaderSize,
                        dstCapacity - ZSTD_blockHeaderSize,
                        ip,
                        blockSize,
                        1
                    );
                    {
                        nuint err_code = cSize;
                        if (ERR_isError(err_code))
                        {
                            return err_code;
                        }
                    }

                    if (cSize == 0)
                    {
                        cSize = ZSTD_noCompressBlock(op, dstCapacity, ip, blockSize, lastBlock);
                        {
                            nuint err_code = cSize;
                            if (ERR_isError(err_code))
                            {
                                return err_code;
                            }
                        }
                    }
                    else
                    {
                        uint cBlockHeader =
                            cSize == 1
                                ? lastBlock
                                    + ((uint)blockType_e.bt_rle << 1)
                                    + (uint)(blockSize << 3)
                                : lastBlock
                                    + ((uint)blockType_e.bt_compressed << 1)
                                    + (uint)(cSize << 3);
                        MEM_writeLE24(op, cBlockHeader);
                        cSize += ZSTD_blockHeaderSize;
                    }
                }

                savings += (long)blockSize - (long)cSize;
                ip += blockSize;
                assert(remaining >= blockSize);
                remaining -= blockSize;
                op += cSize;
                assert(dstCapacity >= cSize);
                dstCapacity -= cSize;
                cctx->isFirstBlock = 0;
            }
        }

        if (lastFrameChunk != 0 && op > ostart)
            cctx->stage = ZSTD_compressionStage_e.ZSTDcs_ending;
        return (nuint)(op - ostart);
    }

    private static nuint ZSTD_writeFrameHeader(
        void* dst,
        nuint dstCapacity,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize,
        uint dictID
    )
    {
        byte* op = (byte*)dst;
        /* 0-3 */
        uint dictIDSizeCodeLength = (uint)(
            (dictID > 0 ? 1 : 0) + (dictID >= 256 ? 1 : 0) + (dictID >= 65536 ? 1 : 0)
        );
        /* 0-3 */
        uint dictIDSizeCode = @params->fParams.noDictIDFlag != 0 ? 0 : dictIDSizeCodeLength;
        uint checksumFlag = @params->fParams.checksumFlag > 0 ? 1U : 0U;
        uint windowSize = (uint)1 << (int)@params->cParams.windowLog;
        uint singleSegment =
            @params->fParams.contentSizeFlag != 0 && windowSize >= pledgedSrcSize ? 1U : 0U;
        byte windowLogByte = (byte)(@params->cParams.windowLog - 10 << 3);
        uint fcsCode = (uint)(
            @params->fParams.contentSizeFlag != 0
                ? (pledgedSrcSize >= 256 ? 1 : 0)
                    + (pledgedSrcSize >= 65536 + 256 ? 1 : 0)
                    + (pledgedSrcSize >= 0xFFFFFFFFU ? 1 : 0)
                : 0
        );
        byte frameHeaderDescriptionByte = (byte)(
            dictIDSizeCode + (checksumFlag << 2) + (singleSegment << 5) + (fcsCode << 6)
        );
        nuint pos = 0;
        assert(!(@params->fParams.contentSizeFlag != 0 && pledgedSrcSize == unchecked(0UL - 1)));
        if (dstCapacity < 18)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (@params->format == ZSTD_format_e.ZSTD_f_zstd1)
        {
            MEM_writeLE32(dst, 0xFD2FB528);
            pos = 4;
        }

        op[pos++] = frameHeaderDescriptionByte;
        if (singleSegment == 0)
            op[pos++] = windowLogByte;
        switch (dictIDSizeCode)
        {
            default:
                assert(0 != 0);
                goto case 0;
            case 0:
                break;
            case 1:
                op[pos] = (byte)dictID;
                pos++;
                break;
            case 2:
                MEM_writeLE16(op + pos, (ushort)dictID);
                pos += 2;
                break;
            case 3:
                MEM_writeLE32(op + pos, dictID);
                pos += 4;
                break;
        }

        switch (fcsCode)
        {
            default:
                assert(0 != 0);
                goto case 0;
            case 0:
                if (singleSegment != 0)
                    op[pos++] = (byte)pledgedSrcSize;
                break;
            case 1:
                MEM_writeLE16(op + pos, (ushort)(pledgedSrcSize - 256));
                pos += 2;
                break;
            case 2:
                MEM_writeLE32(op + pos, (uint)pledgedSrcSize);
                pos += 4;
                break;
            case 3:
                MEM_writeLE64(op + pos, pledgedSrcSize);
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
    public static nuint ZSTD_writeSkippableFrame(
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint magicVariant
    )
    {
        byte* op = (byte*)dst;
        if (dstCapacity < srcSize + 8)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (srcSize > 0xFFFFFFFF)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        if (magicVariant > 15)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        MEM_writeLE32(op, 0x184D2A50 + magicVariant);
        MEM_writeLE32(op + 4, (uint)srcSize);
        memcpy(op + 8, src, (uint)srcSize);
        return srcSize + 8;
    }

    /* ZSTD_writeLastEmptyBlock() :
     * output an empty Block with end-of-frame mark to complete a frame
     * @return : size of data written into `dst` (== ZSTD_blockHeaderSize (defined in zstd_internal.h))
     *           or an error code if `dstCapacity` is too small (<ZSTD_blockHeaderSize)
     */
    private static nuint ZSTD_writeLastEmptyBlock(void* dst, nuint dstCapacity)
    {
        if (dstCapacity < ZSTD_blockHeaderSize)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        {
            /*lastBlock*/
            uint cBlockHeader24 = 1 + ((uint)blockType_e.bt_raw << 1);
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
     * NOTE: seqs are not verified! Invalid sequences can cause out-of-bounds memory
     * access and data corruption.
     */
    private static void ZSTD_referenceExternalSequences(ZSTD_CCtx_s* cctx, rawSeq* seq, nuint nbSeq)
    {
        assert(cctx->stage == ZSTD_compressionStage_e.ZSTDcs_init);
        assert(
            nbSeq == 0
                || cctx->appliedParams.ldmParams.enableLdm != ZSTD_paramSwitch_e.ZSTD_ps_enable
        );
        cctx->externSeqStore.seq = seq;
        cctx->externSeqStore.size = nbSeq;
        cctx->externSeqStore.capacity = nbSeq;
        cctx->externSeqStore.pos = 0;
        cctx->externSeqStore.posInSequence = 0;
    }

    private static nuint ZSTD_compressContinue_internal(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint frame,
        uint lastFrameChunk
    )
    {
        ZSTD_MatchState_t* ms = &cctx->blockState.matchState;
        nuint fhSize = 0;
        if (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_created)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        if (frame != 0 && cctx->stage == ZSTD_compressionStage_e.ZSTDcs_init)
        {
            fhSize = ZSTD_writeFrameHeader(
                dst,
                dstCapacity,
                &cctx->appliedParams,
                cctx->pledgedSrcSizePlusOne - 1,
                cctx->dictID
            );
            {
                nuint err_code = fhSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(fhSize <= dstCapacity);
            dstCapacity -= fhSize;
            dst = (sbyte*)dst + fhSize;
            cctx->stage = ZSTD_compressionStage_e.ZSTDcs_ongoing;
        }

        if (srcSize == 0)
            return fhSize;
        if (ZSTD_window_update(&ms->window, src, srcSize, ms->forceNonContiguous) == 0)
        {
            ms->forceNonContiguous = 0;
            ms->nextToUpdate = ms->window.dictLimit;
        }

        if (cctx->appliedParams.ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable)
        {
            ZSTD_window_update(&cctx->ldmState.window, src, srcSize, 0);
        }

        if (frame == 0)
        {
            ZSTD_overflowCorrectIfNeeded(
                ms,
                &cctx->workspace,
                &cctx->appliedParams,
                src,
                (byte*)src + srcSize
            );
        }

        {
            nuint cSize =
                frame != 0
                    ? ZSTD_compress_frameChunk(cctx, dst, dstCapacity, src, srcSize, lastFrameChunk)
                    : ZSTD_compressBlock_internal(cctx, dst, dstCapacity, src, srcSize, 0);
            {
                nuint err_code = cSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            cctx->consumedSrcSize += srcSize;
            cctx->producedCSize += cSize + fhSize;
            assert(
                !(
                    cctx->appliedParams.fParams.contentSizeFlag != 0
                    && cctx->pledgedSrcSizePlusOne == 0
                )
            );
            if (cctx->pledgedSrcSizePlusOne != 0)
            {
                if (cctx->consumedSrcSize + 1 > cctx->pledgedSrcSizePlusOne)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
                }
            }

            return cSize + fhSize;
        }
    }

    private static nuint ZSTD_compressContinue_public(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressContinue_internal(cctx, dst, dstCapacity, src, srcSize, 1, 0);
    }

    /* NOTE: Must just wrap ZSTD_compressContinue_public() */
    public static nuint ZSTD_compressContinue(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressContinue_public(cctx, dst, dstCapacity, src, srcSize);
    }

    private static nuint ZSTD_getBlockSize_deprecated(ZSTD_CCtx_s* cctx)
    {
        ZSTD_compressionParameters cParams = cctx->appliedParams.cParams;
        assert(ZSTD_checkCParams(cParams) == 0);
        return cctx->appliedParams.maxBlockSize < (nuint)1 << (int)cParams.windowLog
            ? cctx->appliedParams.maxBlockSize
            : (nuint)1 << (int)cParams.windowLog;
    }

    /* NOTE: Must just wrap ZSTD_getBlockSize_deprecated() */
    public static nuint ZSTD_getBlockSize(ZSTD_CCtx_s* cctx)
    {
        return ZSTD_getBlockSize_deprecated(cctx);
    }

    /* NOTE: Must just wrap ZSTD_compressBlock_deprecated() */
    private static nuint ZSTD_compressBlock_deprecated(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        {
            nuint blockSizeMax = ZSTD_getBlockSize_deprecated(cctx);
            if (srcSize > blockSizeMax)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
            }
        }

        return ZSTD_compressContinue_internal(cctx, dst, dstCapacity, src, srcSize, 0, 0);
    }

    /* NOTE: Must just wrap ZSTD_compressBlock_deprecated() */
    public static nuint ZSTD_compressBlock(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressBlock_deprecated(cctx, dst, dstCapacity, src, srcSize);
    }

    /*! ZSTD_loadDictionaryContent() :
     *  @return : 0, or an error code
     */
    private static nuint ZSTD_loadDictionaryContent(
        ZSTD_MatchState_t* ms,
        ldmState_t* ls,
        ZSTD_cwksp* ws,
        ZSTD_CCtx_params_s* @params,
        void* src,
        nuint srcSize,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_tableFillPurpose_e tfp
    )
    {
        byte* ip = (byte*)src;
        byte* iend = ip + srcSize;
        int loadLdmDict =
            @params->ldmParams.enableLdm == ZSTD_paramSwitch_e.ZSTD_ps_enable && ls != null ? 1 : 0;
        ZSTD_assertEqualCParams(@params->cParams, ms->cParams);
        {
            /* Allow the dictionary to set indices up to exactly ZSTD_CURRENT_MAX.
             * Dictionaries right at the edge will immediately trigger overflow
             * correction, but I don't want to insert extra constraints here.
             */
            uint maxDictSize = (MEM_64bits ? 3500U * (1 << 20) : 2000U * (1 << 20)) - 2;
            int CDictTaggedIndices = ZSTD_CDictIndicesAreTagged(&@params->cParams);
            if (CDictTaggedIndices != 0 && tfp == ZSTD_tableFillPurpose_e.ZSTD_tfp_forCDict)
            {
                /* Some dictionary matchfinders in zstd use "short cache",
                 * which treats the lower ZSTD_SHORT_CACHE_TAG_BITS of each
                 * CDict hashtable entry as a tag rather than as part of an index.
                 * When short cache is used, we need to truncate the dictionary
                 * so that its indices don't overlap with the tag. */
                const uint shortCacheMaxDictSize = (1U << 32 - 8) - 2;
                maxDictSize =
                    maxDictSize < shortCacheMaxDictSize ? maxDictSize : shortCacheMaxDictSize;
                assert(loadLdmDict == 0);
            }

            if (srcSize > maxDictSize)
            {
                ip = iend - maxDictSize;
                src = ip;
                srcSize = maxDictSize;
            }
        }

        if (srcSize > unchecked((uint)-1) - (MEM_64bits ? 3500U * (1 << 20) : 2000U * (1 << 20)))
        {
            assert(ZSTD_window_isEmpty(ms->window) != 0);
#if DEBUG
            if (loadLdmDict != 0)
                assert(ZSTD_window_isEmpty(ls->window) != 0);
#endif
        }

        ZSTD_window_update(&ms->window, src, srcSize, 0);
        if (loadLdmDict != 0)
        {
            ZSTD_window_update(&ls->window, src, srcSize, 0);
            ls->loadedDictEnd = @params->forceWindow != 0 ? 0 : (uint)(iend - ls->window.@base);
            ZSTD_ldm_fillHashTable(ls, ip, iend, &@params->ldmParams);
        }

        {
            uint maxDictSize =
                1U
                << (int)(
                    (
                        @params->cParams.hashLog + 3 > @params->cParams.chainLog + 1
                            ? @params->cParams.hashLog + 3
                            : @params->cParams.chainLog + 1
                    ) < 31
                        ? @params->cParams.hashLog + 3 > @params->cParams.chainLog + 1
                            ? @params->cParams.hashLog + 3
                            : @params->cParams.chainLog + 1
                        : 31
                );
            if (srcSize > maxDictSize)
            {
                ip = iend - maxDictSize;
                src = ip;
                srcSize = maxDictSize;
            }
        }

        ms->nextToUpdate = (uint)(ip - ms->window.@base);
        ms->loadedDictEnd = @params->forceWindow != 0 ? 0 : (uint)(iend - ms->window.@base);
        ms->forceNonContiguous = @params->deterministicRefPrefix;
        if (srcSize <= 8)
            return 0;
        ZSTD_overflowCorrectIfNeeded(ms, ws, @params, ip, iend);
        switch (@params->cParams.strategy)
        {
            case ZSTD_strategy.ZSTD_fast:
                ZSTD_fillHashTable(ms, iend, dtlm, tfp);
                break;
            case ZSTD_strategy.ZSTD_dfast:
                ZSTD_fillDoubleHashTable(ms, iend, dtlm, tfp);
                break;
            case ZSTD_strategy.ZSTD_greedy:
            case ZSTD_strategy.ZSTD_lazy:
            case ZSTD_strategy.ZSTD_lazy2:
                assert(srcSize >= 8);
                if (ms->dedicatedDictSearch != 0)
                {
                    assert(ms->chainTable != null);
                    ZSTD_dedicatedDictSearch_lazy_loadDictionary(ms, iend - 8);
                }
                else
                {
                    assert(@params->useRowMatchFinder != ZSTD_paramSwitch_e.ZSTD_ps_auto);
                    if (@params->useRowMatchFinder == ZSTD_paramSwitch_e.ZSTD_ps_enable)
                    {
                        nuint tagTableSize = (nuint)1 << (int)@params->cParams.hashLog;
                        memset(ms->tagTable, 0, (uint)tagTableSize);
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
                assert(srcSize >= 8);
                ZSTD_updateTree(ms, iend - 8, iend);
                break;
            default:
                assert(0 != 0);
                break;
        }

        ms->nextToUpdate = (uint)(iend - ms->window.@base);
        return 0;
    }

    /* Dictionaries that assign zero probability to symbols that show up causes problems
     * when FSE encoding. Mark dictionaries with zero probability symbols as FSE_repeat_check
     * and only dictionaries with 100% valid symbols can be assumed valid.
     */
    private static FSE_repeat ZSTD_dictNCountRepeat(
        short* normalizedCounter,
        uint dictMaxSymbolValue,
        uint maxSymbolValue
    )
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
    private static nuint ZSTD_loadCEntropy(
        ZSTD_compressedBlockState_t* bs,
        void* workspace,
        void* dict,
        nuint dictSize
    )
    {
        short* offcodeNCount = stackalloc short[32];
        uint offcodeMaxValue = 31;
        /* skip magic num and dict ID */
        byte* dictPtr = (byte*)dict;
        byte* dictEnd = dictPtr + dictSize;
        dictPtr += 8;
        bs->entropy.huf.repeatMode = HUF_repeat.HUF_repeat_check;
        {
            uint maxSymbolValue = 255;
            uint hasZeroWeights = 1;
            nuint hufHeaderSize = HUF_readCTable(
                &bs->entropy.huf.CTable.e0,
                &maxSymbolValue,
                dictPtr,
                (nuint)(dictEnd - dictPtr),
                &hasZeroWeights
            );
            if (hasZeroWeights == 0 && maxSymbolValue == 255)
                bs->entropy.huf.repeatMode = HUF_repeat.HUF_repeat_valid;
            if (ERR_isError(hufHeaderSize))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            dictPtr += hufHeaderSize;
        }

        {
            uint offcodeLog;
            nuint offcodeHeaderSize = FSE_readNCount(
                offcodeNCount,
                &offcodeMaxValue,
                &offcodeLog,
                dictPtr,
                (nuint)(dictEnd - dictPtr)
            );
            if (ERR_isError(offcodeHeaderSize))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            if (offcodeLog > 8)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            if (
                ERR_isError(
                    FSE_buildCTable_wksp(
                        bs->entropy.fse.offcodeCTable,
                        offcodeNCount,
                        31,
                        offcodeLog,
                        workspace,
                        (8 << 10) + 512
                    )
                )
            )
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            dictPtr += offcodeHeaderSize;
        }

        {
            short* matchlengthNCount = stackalloc short[53];
            uint matchlengthMaxValue = 52,
                matchlengthLog;
            nuint matchlengthHeaderSize = FSE_readNCount(
                matchlengthNCount,
                &matchlengthMaxValue,
                &matchlengthLog,
                dictPtr,
                (nuint)(dictEnd - dictPtr)
            );
            if (ERR_isError(matchlengthHeaderSize))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            if (matchlengthLog > 9)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            if (
                ERR_isError(
                    FSE_buildCTable_wksp(
                        bs->entropy.fse.matchlengthCTable,
                        matchlengthNCount,
                        matchlengthMaxValue,
                        matchlengthLog,
                        workspace,
                        (8 << 10) + 512
                    )
                )
            )
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            bs->entropy.fse.matchlength_repeatMode = ZSTD_dictNCountRepeat(
                matchlengthNCount,
                matchlengthMaxValue,
                52
            );
            dictPtr += matchlengthHeaderSize;
        }

        {
            short* litlengthNCount = stackalloc short[36];
            uint litlengthMaxValue = 35,
                litlengthLog;
            nuint litlengthHeaderSize = FSE_readNCount(
                litlengthNCount,
                &litlengthMaxValue,
                &litlengthLog,
                dictPtr,
                (nuint)(dictEnd - dictPtr)
            );
            if (ERR_isError(litlengthHeaderSize))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            if (litlengthLog > 9)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            if (
                ERR_isError(
                    FSE_buildCTable_wksp(
                        bs->entropy.fse.litlengthCTable,
                        litlengthNCount,
                        litlengthMaxValue,
                        litlengthLog,
                        workspace,
                        (8 << 10) + 512
                    )
                )
            )
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            }

            bs->entropy.fse.litlength_repeatMode = ZSTD_dictNCountRepeat(
                litlengthNCount,
                litlengthMaxValue,
                35
            );
            dictPtr += litlengthHeaderSize;
        }

        if (dictPtr + 12 > dictEnd)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
        }

        bs->rep[0] = MEM_readLE32(dictPtr + 0);
        bs->rep[1] = MEM_readLE32(dictPtr + 4);
        bs->rep[2] = MEM_readLE32(dictPtr + 8);
        dictPtr += 12;
        {
            nuint dictContentSize = (nuint)(dictEnd - dictPtr);
            uint offcodeMax = 31;
            if (dictContentSize <= unchecked((uint)-1) - 128 * (1 << 10))
            {
                /* The maximum offset that must be supported */
                uint maxOffset = (uint)dictContentSize + 128 * (1 << 10);
                offcodeMax = ZSTD_highbit32(maxOffset);
            }

            bs->entropy.fse.offcode_repeatMode = ZSTD_dictNCountRepeat(
                offcodeNCount,
                offcodeMaxValue,
                offcodeMax < 31 ? offcodeMax : 31
            );
            {
                uint u;
                for (u = 0; u < 3; u++)
                {
                    if (bs->rep[u] == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)
                        );
                    }

                    if (bs->rep[u] > dictContentSize)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)
                        );
                    }
                }
            }
        }

        return (nuint)(dictPtr - (byte*)dict);
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
    private static nuint ZSTD_loadZstdDictionary(
        ZSTD_compressedBlockState_t* bs,
        ZSTD_MatchState_t* ms,
        ZSTD_cwksp* ws,
        ZSTD_CCtx_params_s* @params,
        void* dict,
        nuint dictSize,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_tableFillPurpose_e tfp,
        void* workspace
    )
    {
        byte* dictPtr = (byte*)dict;
        byte* dictEnd = dictPtr + dictSize;
        nuint dictID;
        nuint eSize;
        assert(dictSize >= 8);
        assert(MEM_readLE32(dictPtr) == 0xEC30A437);
        dictID = @params->fParams.noDictIDFlag != 0 ? 0 : MEM_readLE32(dictPtr + 4);
        eSize = ZSTD_loadCEntropy(bs, workspace, dict, dictSize);
        {
            nuint err_code = eSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        dictPtr += eSize;
        {
            nuint dictContentSize = (nuint)(dictEnd - dictPtr);
            {
                nuint err_code = ZSTD_loadDictionaryContent(
                    ms,
                    null,
                    ws,
                    @params,
                    dictPtr,
                    dictContentSize,
                    dtlm,
                    tfp
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }
        }

        return dictID;
    }

    /** ZSTD_compress_insertDictionary() :
     *   @return : dictID, or an error code */
    private static nuint ZSTD_compress_insertDictionary(
        ZSTD_compressedBlockState_t* bs,
        ZSTD_MatchState_t* ms,
        ldmState_t* ls,
        ZSTD_cwksp* ws,
        ZSTD_CCtx_params_s* @params,
        void* dict,
        nuint dictSize,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_tableFillPurpose_e tfp,
        void* workspace
    )
    {
        if (dict == null || dictSize < 8)
        {
            if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_fullDict)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong));
            }

            return 0;
        }

        ZSTD_reset_compressedBlockState(bs);
        if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_rawContent)
            return ZSTD_loadDictionaryContent(ms, ls, ws, @params, dict, dictSize, dtlm, tfp);
        if (MEM_readLE32(dict) != 0xEC30A437)
        {
            if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_auto)
            {
                return ZSTD_loadDictionaryContent(ms, ls, ws, @params, dict, dictSize, dtlm, tfp);
            }

            if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_fullDict)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong));
            }

            assert(0 != 0);
        }

        return ZSTD_loadZstdDictionary(bs, ms, ws, @params, dict, dictSize, dtlm, tfp, workspace);
    }

    /*! ZSTD_compressBegin_internal() :
     * Assumption : either @dict OR @cdict (or none) is non-NULL, never both
     * @return : 0, or an error code */
    private static nuint ZSTD_compressBegin_internal(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize,
        ZSTD_buffered_policy_e zbuff
    )
    {
        nuint dictContentSize = cdict != null ? cdict->dictContentSize : dictSize;
        assert(!ERR_isError(ZSTD_checkCParams(@params->cParams)));
        assert(!(dict != null && cdict != null));
        if (
            cdict != null
            && cdict->dictContentSize > 0
            && (
                pledgedSrcSize < 128 * (1 << 10)
                || pledgedSrcSize < cdict->dictContentSize * 6UL
                || pledgedSrcSize == unchecked(0UL - 1)
                || cdict->compressionLevel == 0
            )
            && @params->attachDictPref != ZSTD_dictAttachPref_e.ZSTD_dictForceLoad
        )
        {
            return ZSTD_resetCCtx_usingCDict(cctx, cdict, @params, pledgedSrcSize, zbuff);
        }

        {
            nuint err_code = ZSTD_resetCCtx_internal(
                cctx,
                @params,
                pledgedSrcSize,
                dictContentSize,
                ZSTD_compResetPolicy_e.ZSTDcrp_makeClean,
                zbuff
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint dictID =
                cdict != null
                    ? ZSTD_compress_insertDictionary(
                        cctx->blockState.prevCBlock,
                        &cctx->blockState.matchState,
                        &cctx->ldmState,
                        &cctx->workspace,
                        &cctx->appliedParams,
                        cdict->dictContent,
                        cdict->dictContentSize,
                        cdict->dictContentType,
                        dtlm,
                        ZSTD_tableFillPurpose_e.ZSTD_tfp_forCCtx,
                        cctx->tmpWorkspace
                    )
                    : ZSTD_compress_insertDictionary(
                        cctx->blockState.prevCBlock,
                        &cctx->blockState.matchState,
                        &cctx->ldmState,
                        &cctx->workspace,
                        &cctx->appliedParams,
                        dict,
                        dictSize,
                        dictContentType,
                        dtlm,
                        ZSTD_tableFillPurpose_e.ZSTD_tfp_forCCtx,
                        cctx->tmpWorkspace
                    );
            {
                nuint err_code = dictID;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(dictID <= 0xffffffff);
            cctx->dictID = (uint)dictID;
            cctx->dictContentSize = dictContentSize;
        }

        return 0;
    }

    /* ZSTD_compressBegin_advanced_internal() :
     * Private use only. To be called from zstdmt_compress.c. */
    private static nuint ZSTD_compressBegin_advanced_internal(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_dictTableLoadMethod_e dtlm,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize
    )
    {
        {
            /* compression parameters verification and optimization */
            nuint err_code = ZSTD_checkCParams(@params->cParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return ZSTD_compressBegin_internal(
            cctx,
            dict,
            dictSize,
            dictContentType,
            dtlm,
            cdict,
            @params,
            pledgedSrcSize,
            ZSTD_buffered_policy_e.ZSTDb_not_buffered
        );
    }

    /*! ZSTD_compressBegin_advanced() :
     *   @return : 0, or an error code */
    public static nuint ZSTD_compressBegin_advanced(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize,
        ZSTD_parameters @params,
        ulong pledgedSrcSize
    )
    {
        ZSTD_CCtx_params_s cctxParams;
        ZSTD_CCtxParams_init_internal(&cctxParams, &@params, 0);
        return ZSTD_compressBegin_advanced_internal(
            cctx,
            dict,
            dictSize,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
            null,
            &cctxParams,
            pledgedSrcSize
        );
    }

    private static nuint ZSTD_compressBegin_usingDict_deprecated(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize,
        int compressionLevel
    )
    {
        ZSTD_CCtx_params_s cctxParams;
        {
            ZSTD_parameters @params = ZSTD_getParams_internal(
                compressionLevel,
                unchecked(0UL - 1),
                dictSize,
                ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
            );
            ZSTD_CCtxParams_init_internal(
                &cctxParams,
                &@params,
                compressionLevel == 0 ? 3 : compressionLevel
            );
        }

        return ZSTD_compressBegin_internal(
            cctx,
            dict,
            dictSize,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
            null,
            &cctxParams,
            unchecked(0UL - 1),
            ZSTD_buffered_policy_e.ZSTDb_not_buffered
        );
    }

    public static nuint ZSTD_compressBegin_usingDict(
        ZSTD_CCtx_s* cctx,
        void* dict,
        nuint dictSize,
        int compressionLevel
    )
    {
        return ZSTD_compressBegin_usingDict_deprecated(cctx, dict, dictSize, compressionLevel);
    }

    /*=====   Buffer-less streaming compression functions  =====*/
    public static nuint ZSTD_compressBegin(ZSTD_CCtx_s* cctx, int compressionLevel)
    {
        return ZSTD_compressBegin_usingDict_deprecated(cctx, null, 0, compressionLevel);
    }

    /*! ZSTD_writeEpilogue() :
     *   Ends a frame.
     *   @return : nb of bytes written into dst (or an error code) */
    private static nuint ZSTD_writeEpilogue(ZSTD_CCtx_s* cctx, void* dst, nuint dstCapacity)
    {
        byte* ostart = (byte*)dst;
        byte* op = ostart;
        if (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_created)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong));
        }

        if (cctx->stage == ZSTD_compressionStage_e.ZSTDcs_init)
        {
            nuint fhSize = ZSTD_writeFrameHeader(dst, dstCapacity, &cctx->appliedParams, 0, 0);
            {
                nuint err_code = fhSize;
                if (ERR_isError(err_code))
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
            /* last block */
            uint cBlockHeader24 = 1 + ((uint)blockType_e.bt_raw << 1) + 0;
            if (dstCapacity < 3)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            MEM_writeLE24(op, cBlockHeader24);
            op += ZSTD_blockHeaderSize;
            dstCapacity -= ZSTD_blockHeaderSize;
        }

        if (cctx->appliedParams.fParams.checksumFlag != 0)
        {
            uint checksum = (uint)ZSTD_XXH64_digest(&cctx->xxhState);
            if (dstCapacity < 4)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            MEM_writeLE32(op, checksum);
            op += 4;
        }

        cctx->stage = ZSTD_compressionStage_e.ZSTDcs_created;
        return (nuint)(op - ostart);
    }

    /** ZSTD_CCtx_trace() :
     *  Trace the end of a compression call.
     */
    private static void ZSTD_CCtx_trace(ZSTD_CCtx_s* cctx, nuint extraCSize) { }

    private static nuint ZSTD_compressEnd_public(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        nuint endResult;
        nuint cSize = ZSTD_compressContinue_internal(cctx, dst, dstCapacity, src, srcSize, 1, 1);
        {
            nuint err_code = cSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        endResult = ZSTD_writeEpilogue(cctx, (sbyte*)dst + cSize, dstCapacity - cSize);
        {
            nuint err_code = endResult;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        assert(
            !(cctx->appliedParams.fParams.contentSizeFlag != 0 && cctx->pledgedSrcSizePlusOne == 0)
        );
        if (cctx->pledgedSrcSizePlusOne != 0)
        {
            if (cctx->pledgedSrcSizePlusOne != cctx->consumedSrcSize + 1)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
            }
        }

        ZSTD_CCtx_trace(cctx, endResult);
        return cSize + endResult;
    }

    /* NOTE: Must just wrap ZSTD_compressEnd_public() */
    public static nuint ZSTD_compressEnd(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_compressEnd_public(cctx, dst, dstCapacity, src, srcSize);
    }

    /*! ZSTD_compress_advanced() :
     *  Note : this function is now DEPRECATED.
     *         It can be replaced by ZSTD_compress2(), in combination with ZSTD_CCtx_setParameter() and other parameter setters.
     *  This prototype will generate compilation warnings. */
    public static nuint ZSTD_compress_advanced(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        void* dict,
        nuint dictSize,
        ZSTD_parameters @params
    )
    {
        {
            nuint err_code = ZSTD_checkCParams(@params.cParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        ZSTD_CCtxParams_init_internal(&cctx->simpleApiParams, &@params, 0);
        return ZSTD_compress_advanced_internal(
            cctx,
            dst,
            dstCapacity,
            src,
            srcSize,
            dict,
            dictSize,
            &cctx->simpleApiParams
        );
    }

    /* Internal */
    private static nuint ZSTD_compress_advanced_internal(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        void* dict,
        nuint dictSize,
        ZSTD_CCtx_params_s* @params
    )
    {
        {
            nuint err_code = ZSTD_compressBegin_internal(
                cctx,
                dict,
                dictSize,
                ZSTD_dictContentType_e.ZSTD_dct_auto,
                ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
                null,
                @params,
                srcSize,
                ZSTD_buffered_policy_e.ZSTDb_not_buffered
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return ZSTD_compressEnd_public(cctx, dst, dstCapacity, src, srcSize);
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
    public static nuint ZSTD_compress_usingDict(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        void* dict,
        nuint dictSize,
        int compressionLevel
    )
    {
        {
            ZSTD_parameters @params = ZSTD_getParams_internal(
                compressionLevel,
                srcSize,
                dict != null ? dictSize : 0,
                ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict
            );
            assert(@params.fParams.contentSizeFlag == 1);
            ZSTD_CCtxParams_init_internal(
                &cctx->simpleApiParams,
                &@params,
                compressionLevel == 0 ? 3 : compressionLevel
            );
        }

        return ZSTD_compress_advanced_internal(
            cctx,
            dst,
            dstCapacity,
            src,
            srcSize,
            dict,
            dictSize,
            &cctx->simpleApiParams
        );
    }

    /*! ZSTD_compressCCtx() :
     *  Same as ZSTD_compress(), using an explicit ZSTD_CCtx.
     *  Important : in order to mirror `ZSTD_compress()` behavior,
     *  this function compresses at the requested compression level,
     *  __ignoring any other advanced parameter__ .
     *  If any advanced parameter was set using the advanced API,
     *  they will all be reset. Only @compressionLevel remains.
     */
    public static nuint ZSTD_compressCCtx(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        int compressionLevel
    )
    {
        assert(cctx != null);
        return ZSTD_compress_usingDict(
            cctx,
            dst,
            dstCapacity,
            src,
            srcSize,
            null,
            0,
            compressionLevel
        );
    }

    /***************************************
     *  Simple Core API
     ***************************************/
    /*! ZSTD_compress() :
     *  Compresses `src` content as a single zstd compressed frame into already allocated `dst`.
     *  NOTE: Providing `dstCapacity >= ZSTD_compressBound(srcSize)` guarantees that zstd will have
     *        enough space to successfully compress the data.
     *  @return : compressed size written into `dst` (<= `dstCapacity),
     *            or an error code if it fails (which can be tested using ZSTD_isError()). */
    public static nuint ZSTD_compress(
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        int compressionLevel
    )
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
    public static nuint ZSTD_estimateCDictSize_advanced(
        nuint dictSize,
        ZSTD_compressionParameters cParams,
        ZSTD_dictLoadMethod_e dictLoadMethod
    )
    {
        return ZSTD_cwksp_alloc_size((nuint)sizeof(ZSTD_CDict_s))
            + ZSTD_cwksp_alloc_size((8 << 10) + 512)
            + ZSTD_sizeof_matchState(
                &cParams,
                ZSTD_resolveRowMatchFinderMode(ZSTD_paramSwitch_e.ZSTD_ps_auto, &cParams),
                1,
                0
            )
            + (
                dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef
                    ? 0
                    : ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(dictSize, (nuint)sizeof(void*)))
            );
    }

    /*! ZSTD_estimate?DictSize() :
     *  ZSTD_estimateCDictSize() will bet that src size is relatively "small", and content is copied, like ZSTD_createCDict().
     *  ZSTD_estimateCDictSize_advanced() makes it possible to control compression parameters precisely, like ZSTD_createCDict_advanced().
     *  Note : dictionaries created by reference (`ZSTD_dlm_byRef`) are logically smaller.
     */
    public static nuint ZSTD_estimateCDictSize(nuint dictSize, int compressionLevel)
    {
        ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
            compressionLevel,
            unchecked(0UL - 1),
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_createCDict
        );
        return ZSTD_estimateCDictSize_advanced(
            dictSize,
            cParams,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy
        );
    }

    public static nuint ZSTD_sizeof_CDict(ZSTD_CDict_s* cdict)
    {
        if (cdict == null)
            return 0;
        return (nuint)(cdict->workspace.workspace == cdict ? 0 : sizeof(ZSTD_CDict_s))
            + ZSTD_cwksp_sizeof(&cdict->workspace);
    }

    private static nuint ZSTD_initCDict_internal(
        ZSTD_CDict_s* cdict,
        void* dictBuffer,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_CCtx_params_s @params
    )
    {
        assert(ZSTD_checkCParams(@params.cParams) == 0);
        cdict->matchState.cParams = @params.cParams;
        cdict->matchState.dedicatedDictSearch = @params.enableDedicatedDictSearch;
        if (
            dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef
            || dictBuffer == null
            || dictSize == 0
        )
        {
            cdict->dictContent = dictBuffer;
        }
        else
        {
            void* internalBuffer = ZSTD_cwksp_reserve_object(
                &cdict->workspace,
                ZSTD_cwksp_align(dictSize, (nuint)sizeof(void*))
            );
            if (internalBuffer == null)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            }

            cdict->dictContent = internalBuffer;
            memcpy(internalBuffer, dictBuffer, (uint)dictSize);
        }

        cdict->dictContentSize = dictSize;
        cdict->dictContentType = dictContentType;
        cdict->entropyWorkspace = (uint*)ZSTD_cwksp_reserve_object(
            &cdict->workspace,
            (8 << 10) + 512
        );
        ZSTD_reset_compressedBlockState(&cdict->cBlockState);
        {
            nuint err_code = ZSTD_reset_matchState(
                &cdict->matchState,
                &cdict->workspace,
                &@params.cParams,
                @params.useRowMatchFinder,
                ZSTD_compResetPolicy_e.ZSTDcrp_makeClean,
                ZSTD_indexResetPolicy_e.ZSTDirp_reset,
                ZSTD_resetTarget_e.ZSTD_resetTarget_CDict
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            @params.compressionLevel = 3;
            @params.fParams.contentSizeFlag = 1;
            {
                nuint dictID = ZSTD_compress_insertDictionary(
                    &cdict->cBlockState,
                    &cdict->matchState,
                    null,
                    &cdict->workspace,
                    &@params,
                    cdict->dictContent,
                    cdict->dictContentSize,
                    dictContentType,
                    ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_full,
                    ZSTD_tableFillPurpose_e.ZSTD_tfp_forCDict,
                    cdict->entropyWorkspace
                );
                {
                    nuint err_code = dictID;
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                assert(dictID <= unchecked((uint)-1));
                cdict->dictID = (uint)dictID;
            }
        }

        return 0;
    }

    private static ZSTD_CDict_s* ZSTD_createCDict_advanced_internal(
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_compressionParameters cParams,
        ZSTD_paramSwitch_e useRowMatchFinder,
        int enableDedicatedDictSearch,
        ZSTD_customMem customMem
    )
    {
        if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            return null;
        {
            nuint workspaceSize =
                ZSTD_cwksp_alloc_size((nuint)sizeof(ZSTD_CDict_s))
                + ZSTD_cwksp_alloc_size((8 << 10) + 512)
                + ZSTD_sizeof_matchState(&cParams, useRowMatchFinder, enableDedicatedDictSearch, 0)
                + (
                    dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef
                        ? 0
                        : ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(dictSize, (nuint)sizeof(void*)))
                );
            void* workspace = ZSTD_customMalloc(workspaceSize, customMem);
            ZSTD_cwksp ws;
            ZSTD_CDict_s* cdict;
            if (workspace == null)
            {
                ZSTD_customFree(workspace, customMem);
                return null;
            }

            ZSTD_cwksp_init(
                &ws,
                workspace,
                workspaceSize,
                ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_dynamic_alloc
            );
            cdict = (ZSTD_CDict_s*)ZSTD_cwksp_reserve_object(&ws, (nuint)sizeof(ZSTD_CDict_s));
            assert(cdict != null);
            ZSTD_cwksp_move(&cdict->workspace, &ws);
            cdict->customMem = customMem;
            cdict->compressionLevel = 0;
            cdict->useRowMatchFinder = useRowMatchFinder;
            return cdict;
        }
    }

    public static ZSTD_CDict_s* ZSTD_createCDict_advanced(
        void* dictBuffer,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_compressionParameters cParams,
        ZSTD_customMem customMem
    )
    {
        ZSTD_CCtx_params_s cctxParams;
        cctxParams = new ZSTD_CCtx_params_s();
        ZSTD_CCtxParams_init(&cctxParams, 0);
        cctxParams.cParams = cParams;
        cctxParams.customMem = customMem;
        return ZSTD_createCDict_advanced2(
            dictBuffer,
            dictSize,
            dictLoadMethod,
            dictContentType,
            &cctxParams,
            customMem
        );
    }

    /*
     * This API is temporary and is expected to change or disappear in the future!
     */
    public static ZSTD_CDict_s* ZSTD_createCDict_advanced2(
        void* dict,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_CCtx_params_s* originalCctxParams,
        ZSTD_customMem customMem
    )
    {
        ZSTD_CCtx_params_s cctxParams = *originalCctxParams;
        ZSTD_compressionParameters cParams;
        ZSTD_CDict_s* cdict;
        if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            return null;
        if (cctxParams.enableDedicatedDictSearch != 0)
        {
            cParams = ZSTD_dedicatedDictSearch_getCParams(cctxParams.compressionLevel, dictSize);
            ZSTD_overrideCParams(&cParams, &cctxParams.cParams);
        }
        else
        {
            cParams = ZSTD_getCParamsFromCCtxParams(
                &cctxParams,
                unchecked(0UL - 1),
                dictSize,
                ZSTD_CParamMode_e.ZSTD_cpm_createCDict
            );
        }

        if (ZSTD_dedicatedDictSearch_isSupported(&cParams) == 0)
        {
            cctxParams.enableDedicatedDictSearch = 0;
            cParams = ZSTD_getCParamsFromCCtxParams(
                &cctxParams,
                unchecked(0UL - 1),
                dictSize,
                ZSTD_CParamMode_e.ZSTD_cpm_createCDict
            );
        }

        cctxParams.cParams = cParams;
        cctxParams.useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
            cctxParams.useRowMatchFinder,
            &cParams
        );
        cdict = ZSTD_createCDict_advanced_internal(
            dictSize,
            dictLoadMethod,
            cctxParams.cParams,
            cctxParams.useRowMatchFinder,
            cctxParams.enableDedicatedDictSearch,
            customMem
        );
        if (
            cdict == null
            || ERR_isError(
                ZSTD_initCDict_internal(
                    cdict,
                    dict,
                    dictSize,
                    dictLoadMethod,
                    dictContentType,
                    cctxParams
                )
            )
        )
        {
            ZSTD_freeCDict(cdict);
            return null;
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
        ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
            compressionLevel,
            unchecked(0UL - 1),
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_createCDict
        );
        ZSTD_CDict_s* cdict = ZSTD_createCDict_advanced(
            dict,
            dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            cParams,
            ZSTD_defaultCMem
        );
        if (cdict != null)
            cdict->compressionLevel = compressionLevel == 0 ? 3 : compressionLevel;
        return cdict;
    }

    /*! ZSTD_createCDict_byReference() :
     *  Create a digested dictionary for compression
     *  Dictionary content is just referenced, not duplicated.
     *  As a consequence, `dictBuffer` **must** outlive CDict,
     *  and its content must remain unmodified throughout the lifetime of CDict.
     *  note: equivalent to ZSTD_createCDict_advanced(), with dictLoadMethod==ZSTD_dlm_byRef */
    public static ZSTD_CDict_s* ZSTD_createCDict_byReference(
        void* dict,
        nuint dictSize,
        int compressionLevel
    )
    {
        ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
            compressionLevel,
            unchecked(0UL - 1),
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_createCDict
        );
        ZSTD_CDict_s* cdict = ZSTD_createCDict_advanced(
            dict,
            dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            cParams,
            ZSTD_defaultCMem
        );
        if (cdict != null)
            cdict->compressionLevel = compressionLevel == 0 ? 3 : compressionLevel;
        return cdict;
    }

    /*! ZSTD_freeCDict() :
     *  Function frees memory allocated by ZSTD_createCDict().
     *  If a NULL pointer is passed, no operation is performed. */
    public static nuint ZSTD_freeCDict(ZSTD_CDict_s* cdict)
    {
        if (cdict == null)
            return 0;
        {
            ZSTD_customMem cMem = cdict->customMem;
            int cdictInWorkspace = ZSTD_cwksp_owns_buffer(&cdict->workspace, cdict);
            ZSTD_cwksp_free(&cdict->workspace, cMem);
            if (cdictInWorkspace == 0)
            {
                ZSTD_customFree(cdict, cMem);
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
     *            into its relevant cParams.
     * @return : pointer to ZSTD_CDict*, or NULL if error (size too small)
     *  Note : there is no corresponding "free" function.
     *         Since workspace was allocated externally, it must be freed externally.
     */
    public static ZSTD_CDict_s* ZSTD_initStaticCDict(
        void* workspace,
        nuint workspaceSize,
        void* dict,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_compressionParameters cParams
    )
    {
        ZSTD_paramSwitch_e useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
            ZSTD_paramSwitch_e.ZSTD_ps_auto,
            &cParams
        );
        /* enableDedicatedDictSearch */
        nuint matchStateSize = ZSTD_sizeof_matchState(&cParams, useRowMatchFinder, 1, 0);
        nuint neededSize =
            ZSTD_cwksp_alloc_size((nuint)sizeof(ZSTD_CDict_s))
            + (
                dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef
                    ? 0
                    : ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(dictSize, (nuint)sizeof(void*)))
            )
            + ZSTD_cwksp_alloc_size((8 << 10) + 512)
            + matchStateSize;
        ZSTD_CDict_s* cdict;
        ZSTD_CCtx_params_s @params;
        if (((nuint)workspace & 7) != 0)
            return null;
        {
            ZSTD_cwksp ws;
            ZSTD_cwksp_init(
                &ws,
                workspace,
                workspaceSize,
                ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_static_alloc
            );
            cdict = (ZSTD_CDict_s*)ZSTD_cwksp_reserve_object(&ws, (nuint)sizeof(ZSTD_CDict_s));
            if (cdict == null)
                return null;
            ZSTD_cwksp_move(&cdict->workspace, &ws);
        }

        if (workspaceSize < neededSize)
            return null;
        ZSTD_CCtxParams_init(&@params, 0);
        @params.cParams = cParams;
        @params.useRowMatchFinder = useRowMatchFinder;
        cdict->useRowMatchFinder = useRowMatchFinder;
        cdict->compressionLevel = 0;
        if (
            ERR_isError(
                ZSTD_initCDict_internal(
                    cdict,
                    dict,
                    dictSize,
                    dictLoadMethod,
                    dictContentType,
                    @params
                )
            )
        )
            return null;
        return cdict;
    }

    /*! ZSTD_getCParamsFromCDict() :
     *  as the name implies */
    private static ZSTD_compressionParameters ZSTD_getCParamsFromCDict(ZSTD_CDict_s* cdict)
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
            return 0;
        return cdict->dictID;
    }

    /* ZSTD_compressBegin_usingCDict_internal() :
     * Implementation of various ZSTD_compressBegin_usingCDict* functions.
     */
    private static nuint ZSTD_compressBegin_usingCDict_internal(
        ZSTD_CCtx_s* cctx,
        ZSTD_CDict_s* cdict,
        ZSTD_frameParameters fParams,
        ulong pledgedSrcSize
    )
    {
        ZSTD_CCtx_params_s cctxParams;
        if (cdict == null)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong));
        }

        {
            ZSTD_parameters @params;
            @params.fParams = fParams;
            @params.cParams =
                pledgedSrcSize < 128 * (1 << 10)
                || pledgedSrcSize < cdict->dictContentSize * 6UL
                || pledgedSrcSize == unchecked(0UL - 1)
                || cdict->compressionLevel == 0
                    ? ZSTD_getCParamsFromCDict(cdict)
                    : ZSTD_getCParams(
                        cdict->compressionLevel,
                        pledgedSrcSize,
                        cdict->dictContentSize
                    );
            ZSTD_CCtxParams_init_internal(&cctxParams, &@params, cdict->compressionLevel);
        }

        if (pledgedSrcSize != unchecked(0UL - 1))
        {
            uint limitedSrcSize = (uint)(pledgedSrcSize < 1U << 19 ? pledgedSrcSize : 1U << 19);
            uint limitedSrcLog = limitedSrcSize > 1 ? ZSTD_highbit32(limitedSrcSize - 1) + 1 : 1;
            cctxParams.cParams.windowLog =
                cctxParams.cParams.windowLog > limitedSrcLog
                    ? cctxParams.cParams.windowLog
                    : limitedSrcLog;
        }

        return ZSTD_compressBegin_internal(
            cctx,
            null,
            0,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
            cdict,
            &cctxParams,
            pledgedSrcSize,
            ZSTD_buffered_policy_e.ZSTDb_not_buffered
        );
    }

    /* ZSTD_compressBegin_usingCDict_advanced() :
     * This function is DEPRECATED.
     * cdict must be != NULL */
    public static nuint ZSTD_compressBegin_usingCDict_advanced(
        ZSTD_CCtx_s* cctx,
        ZSTD_CDict_s* cdict,
        ZSTD_frameParameters fParams,
        ulong pledgedSrcSize
    )
    {
        return ZSTD_compressBegin_usingCDict_internal(cctx, cdict, fParams, pledgedSrcSize);
    }

    /* ZSTD_compressBegin_usingCDict() :
     * cdict must be != NULL */
    private static nuint ZSTD_compressBegin_usingCDict_deprecated(
        ZSTD_CCtx_s* cctx,
        ZSTD_CDict_s* cdict
    )
    {
        /*content*/
        ZSTD_frameParameters fParams = new ZSTD_frameParameters
        {
            contentSizeFlag = 0,
            checksumFlag = 0,
            noDictIDFlag = 0,
        };
        return ZSTD_compressBegin_usingCDict_internal(cctx, cdict, fParams, unchecked(0UL - 1));
    }

    public static nuint ZSTD_compressBegin_usingCDict(ZSTD_CCtx_s* cctx, ZSTD_CDict_s* cdict)
    {
        return ZSTD_compressBegin_usingCDict_deprecated(cctx, cdict);
    }

    /*! ZSTD_compress_usingCDict_internal():
     * Implementation of various ZSTD_compress_usingCDict* functions.
     */
    private static nuint ZSTD_compress_usingCDict_internal(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        ZSTD_CDict_s* cdict,
        ZSTD_frameParameters fParams
    )
    {
        {
            /* will check if cdict != NULL */
            nuint err_code = ZSTD_compressBegin_usingCDict_internal(cctx, cdict, fParams, srcSize);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return ZSTD_compressEnd_public(cctx, dst, dstCapacity, src, srcSize);
    }

    /*! ZSTD_compress_usingCDict_advanced():
     * This function is DEPRECATED.
     */
    public static nuint ZSTD_compress_usingCDict_advanced(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        ZSTD_CDict_s* cdict,
        ZSTD_frameParameters fParams
    )
    {
        return ZSTD_compress_usingCDict_internal(
            cctx,
            dst,
            dstCapacity,
            src,
            srcSize,
            cdict,
            fParams
        );
    }

    /*! ZSTD_compress_usingCDict() :
     *  Compression using a digested Dictionary.
     *  Faster startup than ZSTD_compress_usingDict(), recommended when same dictionary is used multiple times.
     *  Note that compression parameters are decided at CDict creation time
     *  while frame parameters are hardcoded */
    public static nuint ZSTD_compress_usingCDict(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        ZSTD_CDict_s* cdict
    )
    {
        /*content*/
        ZSTD_frameParameters fParams = new ZSTD_frameParameters
        {
            contentSizeFlag = 1,
            checksumFlag = 0,
            noDictIDFlag = 0,
        };
        return ZSTD_compress_usingCDict_internal(
            cctx,
            dst,
            dstCapacity,
            src,
            srcSize,
            cdict,
            fParams
        );
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
        return 1 << 17;
    }

    public static nuint ZSTD_CStreamOutSize()
    {
        return ZSTD_compressBound(1 << 17) + ZSTD_blockHeaderSize + 4;
    }

    private static ZSTD_CParamMode_e ZSTD_getCParamMode(
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize
    )
    {
        if (cdict != null && ZSTD_shouldAttachDict(cdict, @params, pledgedSrcSize) != 0)
            return ZSTD_CParamMode_e.ZSTD_cpm_attachDict;
        else
            return ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict;
    }

    /* ZSTD_resetCStream():
     * pledgedSrcSize == 0 means "unknown" */
    public static nuint ZSTD_resetCStream(ZSTD_CCtx_s* zcs, ulong pss)
    {
        /* temporary : 0 interpreted as "unknown" during transition period.
         * Users willing to specify "unknown" **must** use ZSTD_CONTENTSIZE_UNKNOWN.
         * 0 will be interpreted as "empty" in the future.
         */
        ulong pledgedSrcSize = pss == 0 ? unchecked(0UL - 1) : pss;
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize);
            if (ERR_isError(err_code))
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
    private static nuint ZSTD_initCStream_internal(
        ZSTD_CCtx_s* zcs,
        void* dict,
        nuint dictSize,
        ZSTD_CDict_s* cdict,
        ZSTD_CCtx_params_s* @params,
        ulong pledgedSrcSize
    )
    {
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        assert(!ERR_isError(ZSTD_checkCParams(@params->cParams)));
        zcs->requestedParams = *@params;
        assert(!(dict != null && cdict != null));
        if (dict != null)
        {
            nuint err_code = ZSTD_CCtx_loadDictionary(zcs, dict, dictSize);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }
        else
        {
            /* Dictionary is cleared if !cdict */
            nuint err_code = ZSTD_CCtx_refCDict(zcs, cdict);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return 0;
    }

    /* ZSTD_initCStream_usingCDict_advanced() :
     * same as ZSTD_initCStream_usingCDict(), with control over frame parameters */
    public static nuint ZSTD_initCStream_usingCDict_advanced(
        ZSTD_CCtx_s* zcs,
        ZSTD_CDict_s* cdict,
        ZSTD_frameParameters fParams,
        ulong pledgedSrcSize
    )
    {
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        zcs->requestedParams.fParams = fParams;
        {
            nuint err_code = ZSTD_CCtx_refCDict(zcs, cdict);
            if (ERR_isError(err_code))
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
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_refCDict(zcs, cdict);
            if (ERR_isError(err_code))
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
    public static nuint ZSTD_initCStream_advanced(
        ZSTD_CCtx_s* zcs,
        void* dict,
        nuint dictSize,
        ZSTD_parameters @params,
        ulong pss
    )
    {
        /* for compatibility with older programs relying on this behavior.
         * Users should now specify ZSTD_CONTENTSIZE_UNKNOWN.
         * This line will be removed in the future.
         */
        ulong pledgedSrcSize =
            pss == 0 && @params.fParams.contentSizeFlag == 0 ? unchecked(0UL - 1) : pss;
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_checkCParams(@params.cParams);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        ZSTD_CCtxParams_setZstdParams(&zcs->requestedParams, &@params);
        {
            nuint err_code = ZSTD_CCtx_loadDictionary(zcs, dict, dictSize);
            if (ERR_isError(err_code))
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
    public static nuint ZSTD_initCStream_usingDict(
        ZSTD_CCtx_s* zcs,
        void* dict,
        nuint dictSize,
        int compressionLevel
    )
    {
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                zcs,
                ZSTD_cParameter.ZSTD_c_compressionLevel,
                compressionLevel
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_loadDictionary(zcs, dict, dictSize);
            if (ERR_isError(err_code))
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
        /* temporary : 0 interpreted as "unknown" during transition period.
         * Users willing to specify "unknown" **must** use ZSTD_CONTENTSIZE_UNKNOWN.
         * 0 will be interpreted as "empty" in the future.
         */
        ulong pledgedSrcSize = pss == 0 ? unchecked(0UL - 1) : pss;
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_refCDict(zcs, null);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                zcs,
                ZSTD_cParameter.ZSTD_c_compressionLevel,
                compressionLevel
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setPledgedSrcSize(zcs, pledgedSrcSize);
            if (ERR_isError(err_code))
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
     *
     * Note that ZSTD_initCStream() clears any previously set dictionary. Use the new API
     * to compress with a dictionary.
     */
    public static nuint ZSTD_initCStream(ZSTD_CCtx_s* zcs, int compressionLevel)
    {
        {
            nuint err_code = ZSTD_CCtx_reset(zcs, ZSTD_ResetDirective.ZSTD_reset_session_only);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_refCDict(zcs, null);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint err_code = ZSTD_CCtx_setParameter(
                zcs,
                ZSTD_cParameter.ZSTD_c_compressionLevel,
                compressionLevel
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return 0;
    }

    /*======   Compression   ======*/
    private static nuint ZSTD_nextInputSizeHint(ZSTD_CCtx_s* cctx)
    {
        if (cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
        {
            return cctx->blockSizeMax - cctx->stableIn_notConsumed;
        }

        assert(cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered);
        {
            nuint hintInSize = cctx->inBuffTarget - cctx->inBuffPos;
            if (hintInSize == 0)
                hintInSize = cctx->blockSizeMax;
            return hintInSize;
        }
    }

    /** ZSTD_compressStream_generic():
     *  internal function for all *compressStream*() variants
     * @return : hint size for next input to complete ongoing block */
    private static nuint ZSTD_compressStream_generic(
        ZSTD_CCtx_s* zcs,
        ZSTD_outBuffer_s* output,
        ZSTD_inBuffer_s* input,
        ZSTD_EndDirective flushMode
    )
    {
        assert(input != null);
        sbyte* istart = (sbyte*)input->src;
        sbyte* iend = istart != null ? istart + input->size : istart;
        sbyte* ip = istart != null ? istart + input->pos : istart;
        assert(output != null);
        sbyte* ostart = (sbyte*)output->dst;
        sbyte* oend = ostart != null ? ostart + output->size : ostart;
        sbyte* op = ostart != null ? ostart + output->pos : ostart;
        uint someMoreWork = 1;
        assert(zcs != null);
        if (zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
        {
            assert(input->pos >= zcs->stableIn_notConsumed);
            input->pos -= zcs->stableIn_notConsumed;
            if (ip != null)
                ip -= zcs->stableIn_notConsumed;
            zcs->stableIn_notConsumed = 0;
        }

#if DEBUG
        if (zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
        {
            assert(zcs->inBuff != null);
            assert(zcs->inBuffSize > 0);
        }
#endif

#if DEBUG
        if (zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
        {
            assert(zcs->outBuff != null);
            assert(zcs->outBuffSize > 0);
        }
#endif

#if DEBUG
        if (input->src == null)
            assert(input->size == 0);
#endif
        assert(input->pos <= input->size);
#if DEBUG
        if (output->dst == null)
            assert(output->size == 0);
#endif
        assert(output->pos <= output->size);
        assert((uint)flushMode <= (uint)ZSTD_EndDirective.ZSTD_e_end);
        while (someMoreWork != 0)
        {
            switch (zcs->streamStage)
            {
                case ZSTD_cStreamStage.zcss_init:
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_init_missing));
                case ZSTD_cStreamStage.zcss_load:
                    if (
                        flushMode == ZSTD_EndDirective.ZSTD_e_end
                        && (
                            (nuint)(oend - op) >= ZSTD_compressBound((nuint)(iend - ip))
                            || zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable
                        )
                        && zcs->inBuffPos == 0
                    )
                    {
                        /* shortcut to compression pass directly into output buffer */
                        nuint cSize = ZSTD_compressEnd_public(
                            zcs,
                            op,
                            (nuint)(oend - op),
                            ip,
                            (nuint)(iend - ip)
                        );
                        {
                            nuint err_code = cSize;
                            if (ERR_isError(err_code))
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

                    if (zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
                    {
                        nuint toLoad = zcs->inBuffTarget - zcs->inBuffPos;
                        nuint loaded = ZSTD_limitCopy(
                            zcs->inBuff + zcs->inBuffPos,
                            toLoad,
                            ip,
                            (nuint)(iend - ip)
                        );
                        zcs->inBuffPos += loaded;
                        if (ip != null)
                            ip += loaded;
                        if (
                            flushMode == ZSTD_EndDirective.ZSTD_e_continue
                            && zcs->inBuffPos < zcs->inBuffTarget
                        )
                        {
                            someMoreWork = 0;
                            break;
                        }

                        if (
                            flushMode == ZSTD_EndDirective.ZSTD_e_flush
                            && zcs->inBuffPos == zcs->inToCompress
                        )
                        {
                            someMoreWork = 0;
                            break;
                        }
                    }
                    else
                    {
                        assert(zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable);
                        if (
                            flushMode == ZSTD_EndDirective.ZSTD_e_continue
                            && (nuint)(iend - ip) < zcs->blockSizeMax
                        )
                        {
                            zcs->stableIn_notConsumed = (nuint)(iend - ip);
                            ip = iend;
                            someMoreWork = 0;
                            break;
                        }

                        if (flushMode == ZSTD_EndDirective.ZSTD_e_flush && ip == iend)
                        {
                            someMoreWork = 0;
                            break;
                        }
                    }

                    {
                        int inputBuffered =
                            zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered
                                ? 1
                                : 0;
                        void* cDst;
                        nuint cSize;
                        nuint oSize = (nuint)(oend - op);
                        nuint iSize =
                            inputBuffered != 0 ? zcs->inBuffPos - zcs->inToCompress
                            : (nuint)(iend - ip) < zcs->blockSizeMax ? (nuint)(iend - ip)
                            : zcs->blockSizeMax;
                        if (
                            oSize >= ZSTD_compressBound(iSize)
                            || zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable
                        )
                            cDst = op;
                        else
                        {
                            cDst = zcs->outBuff;
                            oSize = zcs->outBuffSize;
                        }

                        if (inputBuffered != 0)
                        {
                            uint lastBlock =
                                flushMode == ZSTD_EndDirective.ZSTD_e_end && ip == iend ? 1U : 0U;
                            cSize =
                                lastBlock != 0
                                    ? ZSTD_compressEnd_public(
                                        zcs,
                                        cDst,
                                        oSize,
                                        zcs->inBuff + zcs->inToCompress,
                                        iSize
                                    )
                                    : ZSTD_compressContinue_public(
                                        zcs,
                                        cDst,
                                        oSize,
                                        zcs->inBuff + zcs->inToCompress,
                                        iSize
                                    );
                            {
                                nuint err_code = cSize;
                                if (ERR_isError(err_code))
                                {
                                    return err_code;
                                }
                            }

                            zcs->frameEnded = lastBlock;
                            zcs->inBuffTarget = zcs->inBuffPos + zcs->blockSizeMax;
                            if (zcs->inBuffTarget > zcs->inBuffSize)
                            {
                                zcs->inBuffPos = 0;
                                zcs->inBuffTarget = zcs->blockSizeMax;
                            }

#if DEBUG
                            if (lastBlock == 0)
                                assert(zcs->inBuffTarget <= zcs->inBuffSize);
#endif
                            zcs->inToCompress = zcs->inBuffPos;
                        }
                        else
                        {
                            uint lastBlock =
                                flushMode == ZSTD_EndDirective.ZSTD_e_end && ip + iSize == iend
                                    ? 1U
                                    : 0U;
                            cSize =
                                lastBlock != 0
                                    ? ZSTD_compressEnd_public(zcs, cDst, oSize, ip, iSize)
                                    : ZSTD_compressContinue_public(zcs, cDst, oSize, ip, iSize);
                            if (ip != null)
                                ip += iSize;
                            {
                                nuint err_code = cSize;
                                if (ERR_isError(err_code))
                                {
                                    return err_code;
                                }
                            }

                            zcs->frameEnded = lastBlock;
#if DEBUG
                            if (lastBlock != 0)
                                assert(ip == iend);
#endif
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
                    assert(zcs->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered);

                    {
                        nuint toFlush = zcs->outBuffContentSize - zcs->outBuffFlushedSize;
                        nuint flushed = ZSTD_limitCopy(
                            op,
                            (nuint)(oend - op),
                            zcs->outBuff + zcs->outBuffFlushedSize,
                            toFlush
                        );
                        if (flushed != 0)
                            op += flushed;
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
                    assert(0 != 0);
                    break;
            }
        }

        input->pos = (nuint)(ip - istart);
        output->pos = (nuint)(op - ostart);
        if (zcs->frameEnded != 0)
            return 0;
        return ZSTD_nextInputSizeHint(zcs);
    }

    private static nuint ZSTD_nextInputSizeHint_MTorST(ZSTD_CCtx_s* cctx)
    {
        if (cctx->appliedParams.nbWorkers >= 1)
        {
            assert(cctx->mtctx != null);
            return ZSTDMT_nextInputSizeHint(cctx->mtctx);
        }

        return ZSTD_nextInputSizeHint(cctx);
    }

    /*!
     * Alternative for ZSTD_compressStream2(zcs, output, input, ZSTD_e_continue).
     * NOTE: The return value is different. ZSTD_compressStream() returns a hint for
     * the next read size (if non-zero and not an error). ZSTD_compressStream2()
     * returns the minimum nb of bytes left to flush (if non-zero and not an error).
     */
    public static nuint ZSTD_compressStream(
        ZSTD_CCtx_s* zcs,
        ZSTD_outBuffer_s* output,
        ZSTD_inBuffer_s* input
    )
    {
        {
            nuint err_code = ZSTD_compressStream2(
                zcs,
                output,
                input,
                ZSTD_EndDirective.ZSTD_e_continue
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return ZSTD_nextInputSizeHint_MTorST(zcs);
    }

    /* After a compression call set the expected input/output buffer.
     * This is validated at the start of the next compression call.
     */
    private static void ZSTD_setBufferExpectations(
        ZSTD_CCtx_s* cctx,
        ZSTD_outBuffer_s* output,
        ZSTD_inBuffer_s* input
    )
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
    private static nuint ZSTD_checkBufferStability(
        ZSTD_CCtx_s* cctx,
        ZSTD_outBuffer_s* output,
        ZSTD_inBuffer_s* input,
        ZSTD_EndDirective endOp
    )
    {
        if (cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
        {
            ZSTD_inBuffer_s expect = cctx->expectedInBuffer;
            if (expect.src != input->src || expect.pos != input->pos)
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stabilityCondition_notRespected)
                );
        }

        if (cctx->appliedParams.outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable)
        {
            nuint outBufferSize = output->size - output->pos;
            if (cctx->expectedOutBufferSize != outBufferSize)
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stabilityCondition_notRespected)
                );
        }

        return 0;
    }

    /*
     * If @endOp == ZSTD_e_end, @inSize becomes pledgedSrcSize.
     * Otherwise, it's ignored.
     * @return: 0 on success, or a ZSTD_error code otherwise.
     */
    private static nuint ZSTD_CCtx_init_compressStream2(
        ZSTD_CCtx_s* cctx,
        ZSTD_EndDirective endOp,
        nuint inSize
    )
    {
        ZSTD_CCtx_params_s @params = cctx->requestedParams;
        ZSTD_prefixDict_s prefixDict = cctx->prefixDict;
        {
            /* Init the local dict if present. */
            nuint err_code = ZSTD_initLocalDict(cctx);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        cctx->prefixDict = new ZSTD_prefixDict_s();
        assert(prefixDict.dict == null || cctx->cdict == null);
        if (cctx->cdict != null && cctx->localDict.cdict == null)
        {
            @params.compressionLevel = cctx->cdict->compressionLevel;
        }

        if (endOp == ZSTD_EndDirective.ZSTD_e_end)
            cctx->pledgedSrcSizePlusOne = inSize + 1;
        {
            nuint dictSize =
                prefixDict.dict != null ? prefixDict.dictSize
                : cctx->cdict != null ? cctx->cdict->dictContentSize
                : 0;
            ZSTD_CParamMode_e mode = ZSTD_getCParamMode(
                cctx->cdict,
                &@params,
                cctx->pledgedSrcSizePlusOne - 1
            );
            @params.cParams = ZSTD_getCParamsFromCCtxParams(
                &@params,
                cctx->pledgedSrcSizePlusOne - 1,
                dictSize,
                mode
            );
        }

        @params.postBlockSplitter = ZSTD_resolveBlockSplitterMode(
            @params.postBlockSplitter,
            &@params.cParams
        );
        @params.ldmParams.enableLdm = ZSTD_resolveEnableLdm(
            @params.ldmParams.enableLdm,
            &@params.cParams
        );
        @params.useRowMatchFinder = ZSTD_resolveRowMatchFinderMode(
            @params.useRowMatchFinder,
            &@params.cParams
        );
        @params.validateSequences = ZSTD_resolveExternalSequenceValidation(
            @params.validateSequences
        );
        @params.maxBlockSize = ZSTD_resolveMaxBlockSize(@params.maxBlockSize);
        @params.searchForExternalRepcodes = ZSTD_resolveExternalRepcodeSearch(
            @params.searchForExternalRepcodes,
            @params.compressionLevel
        );
        if (ZSTD_hasExtSeqProd(&@params) != 0 && @params.nbWorkers >= 1)
        {
            return unchecked(
                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_combination_unsupported)
            );
        }

        if (cctx->pledgedSrcSizePlusOne - 1 <= 512 * (1 << 10))
        {
            @params.nbWorkers = 0;
        }

        if (@params.nbWorkers > 0)
        {
            if (cctx->mtctx == null)
            {
                cctx->mtctx = ZSTDMT_createCCtx_advanced(
                    (uint)@params.nbWorkers,
                    cctx->customMem,
                    cctx->pool
                );
                if (cctx->mtctx == null)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
                }
            }

            {
                nuint err_code = ZSTDMT_initCStream_internal(
                    cctx->mtctx,
                    prefixDict.dict,
                    prefixDict.dictSize,
                    prefixDict.dictContentType,
                    cctx->cdict,
                    @params,
                    cctx->pledgedSrcSizePlusOne - 1
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            cctx->dictID = cctx->cdict != null ? cctx->cdict->dictID : 0;
            cctx->dictContentSize =
                cctx->cdict != null ? cctx->cdict->dictContentSize : prefixDict.dictSize;
            cctx->consumedSrcSize = 0;
            cctx->producedCSize = 0;
            cctx->streamStage = ZSTD_cStreamStage.zcss_load;
            cctx->appliedParams = @params;
        }
        else
        {
            ulong pledgedSrcSize = cctx->pledgedSrcSizePlusOne - 1;
            assert(!ERR_isError(ZSTD_checkCParams(@params.cParams)));
            {
                nuint err_code = ZSTD_compressBegin_internal(
                    cctx,
                    prefixDict.dict,
                    prefixDict.dictSize,
                    prefixDict.dictContentType,
                    ZSTD_dictTableLoadMethod_e.ZSTD_dtlm_fast,
                    cctx->cdict,
                    &@params,
                    pledgedSrcSize,
                    ZSTD_buffered_policy_e.ZSTDb_buffered
                );
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(cctx->appliedParams.nbWorkers == 0);
            cctx->inToCompress = 0;
            cctx->inBuffPos = 0;
            if (cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
            {
                cctx->inBuffTarget =
                    cctx->blockSizeMax + (nuint)(cctx->blockSizeMax == pledgedSrcSize ? 1 : 0);
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

    /* @return provides a minimum amount of data remaining to be flushed from internal buffers
     */
    public static nuint ZSTD_compressStream2(
        ZSTD_CCtx_s* cctx,
        ZSTD_outBuffer_s* output,
        ZSTD_inBuffer_s* input,
        ZSTD_EndDirective endOp
    )
    {
        if (output->pos > output->size)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (input->pos > input->size)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        if ((uint)endOp > (uint)ZSTD_EndDirective.ZSTD_e_end)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound));
        }

        assert(cctx != null);
        if (cctx->streamStage == ZSTD_cStreamStage.zcss_init)
        {
            /* no obligation to start from pos==0 */
            nuint inputSize = input->size - input->pos;
            nuint totalInputSize = inputSize + cctx->stableIn_notConsumed;
            if (
                cctx->requestedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable
                && endOp == ZSTD_EndDirective.ZSTD_e_continue
                && totalInputSize < 1 << 17
            )
            {
                if (cctx->stableIn_notConsumed != 0)
                {
                    if (input->src != cctx->expectedInBuffer.src)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stabilityCondition_notRespected)
                        );
                    }

                    if (input->pos != cctx->expectedInBuffer.size)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stabilityCondition_notRespected)
                        );
                    }
                }

                input->pos = input->size;
                cctx->expectedInBuffer = *input;
                cctx->stableIn_notConsumed += inputSize;
                return (nuint)(cctx->requestedParams.format == ZSTD_format_e.ZSTD_f_zstd1 ? 6 : 2);
            }

            {
                nuint err_code = ZSTD_CCtx_init_compressStream2(cctx, endOp, totalInputSize);
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            ZSTD_setBufferExpectations(cctx, output, input);
        }

        {
            /* end of transparent initialization stage */
            nuint err_code = ZSTD_checkBufferStability(cctx, output, input, endOp);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        if (cctx->appliedParams.nbWorkers > 0)
        {
            nuint flushMin;
            if (cctx->cParamsChanged != 0)
            {
                ZSTDMT_updateCParams_whileCompressing(cctx->mtctx, &cctx->requestedParams);
                cctx->cParamsChanged = 0;
            }

            if (cctx->stableIn_notConsumed != 0)
            {
                assert(cctx->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable);
                assert(input->pos >= cctx->stableIn_notConsumed);
                input->pos -= cctx->stableIn_notConsumed;
                cctx->stableIn_notConsumed = 0;
            }

            for (; ; )
            {
                nuint ipos = input->pos;
                nuint opos = output->pos;
                flushMin = ZSTDMT_compressStream_generic(cctx->mtctx, output, input, endOp);
                cctx->consumedSrcSize += input->pos - ipos;
                cctx->producedCSize += output->pos - opos;
                if (ERR_isError(flushMin) || endOp == ZSTD_EndDirective.ZSTD_e_end && flushMin == 0)
                {
                    if (flushMin == 0)
                        ZSTD_CCtx_trace(cctx, 0);
                    ZSTD_CCtx_reset(cctx, ZSTD_ResetDirective.ZSTD_reset_session_only);
                }

                {
                    nuint err_code = flushMin;
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                if (endOp == ZSTD_EndDirective.ZSTD_e_continue)
                {
                    if (
                        input->pos != ipos
                        || output->pos != opos
                        || input->pos == input->size
                        || output->pos == output->size
                    )
                        break;
                }
                else
                {
                    assert(
                        endOp == ZSTD_EndDirective.ZSTD_e_flush
                            || endOp == ZSTD_EndDirective.ZSTD_e_end
                    );
                    if (flushMin == 0 || output->pos == output->size)
                        break;
                }
            }

            assert(
                endOp == ZSTD_EndDirective.ZSTD_e_continue
                    || flushMin == 0
                    || output->pos == output->size
            );
            ZSTD_setBufferExpectations(cctx, output, input);
            return flushMin;
        }

        {
            nuint err_code = ZSTD_compressStream_generic(cctx, output, input, endOp);
            if (ERR_isError(err_code))
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
    public static nuint ZSTD_compressStream2_simpleArgs(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        nuint* dstPos,
        void* src,
        nuint srcSize,
        nuint* srcPos,
        ZSTD_EndDirective endOp
    )
    {
        ZSTD_outBuffer_s output;
        ZSTD_inBuffer_s input;
        output.dst = dst;
        output.size = dstCapacity;
        output.pos = *dstPos;
        input.src = src;
        input.size = srcSize;
        input.pos = *srcPos;
        {
            nuint cErr = ZSTD_compressStream2(cctx, &output, &input, endOp);
            *dstPos = output.pos;
            *srcPos = input.pos;
            return cErr;
        }
    }

    /*! ZSTD_compress2() :
     *  Behave the same as ZSTD_compressCCtx(), but compression parameters are set using the advanced API.
     *  (note that this entry point doesn't even expose a compression level parameter).
     *  ZSTD_compress2() always starts a new frame.
     *  Should cctx hold data from a previously unfinished frame, everything about it is forgotten.
     *  - Compression parameters are pushed into CCtx before starting compression, using ZSTD_CCtx_set*()
     *  - The function is always blocking, returns when compression is completed.
     *  NOTE: Providing `dstCapacity >= ZSTD_compressBound(srcSize)` guarantees that zstd will have
     *        enough space to successfully compress the data, though it is possible it fails for other reasons.
     * @return : compressed size written into `dst` (<= `dstCapacity),
     *           or an error code if it fails (which can be tested using ZSTD_isError()).
     */
    public static nuint ZSTD_compress2(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        ZSTD_bufferMode_e originalInBufferMode = cctx->requestedParams.inBufferMode;
        ZSTD_bufferMode_e originalOutBufferMode = cctx->requestedParams.outBufferMode;
        ZSTD_CCtx_reset(cctx, ZSTD_ResetDirective.ZSTD_reset_session_only);
        cctx->requestedParams.inBufferMode = ZSTD_bufferMode_e.ZSTD_bm_stable;
        cctx->requestedParams.outBufferMode = ZSTD_bufferMode_e.ZSTD_bm_stable;
        {
            nuint oPos = 0;
            nuint iPos = 0;
            nuint result = ZSTD_compressStream2_simpleArgs(
                cctx,
                dst,
                dstCapacity,
                &oPos,
                src,
                srcSize,
                &iPos,
                ZSTD_EndDirective.ZSTD_e_end
            );
            cctx->requestedParams.inBufferMode = originalInBufferMode;
            cctx->requestedParams.outBufferMode = originalOutBufferMode;
            {
                nuint err_code = result;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (result != 0)
            {
                assert(oPos == dstCapacity);
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            assert(iPos == srcSize);
            return oPos;
        }
    }

    /* ZSTD_validateSequence() :
     * @offBase : must use the format required by ZSTD_storeSeq()
     * @returns a ZSTD error code if sequence is not valid
     */
    private static nuint ZSTD_validateSequence(
        uint offBase,
        uint matchLength,
        uint minMatch,
        nuint posInSrc,
        uint windowLog,
        nuint dictSize,
        int useSequenceProducer
    )
    {
        uint windowSize = 1U << (int)windowLog;
        /* posInSrc represents the amount of data the decoder would decode up to this point.
         * As long as the amount of data decoded is less than or equal to window size, offsets may be
         * larger than the total length of output decoded in order to reference the dict, even larger than
         * window size. After output surpasses windowSize, we're limited to windowSize offsets again.
         */
        nuint offsetBound = posInSrc > windowSize ? windowSize : posInSrc + dictSize;
        nuint matchLenLowerBound = (nuint)(minMatch == 3 || useSequenceProducer != 0 ? 3 : 4);
        {
            assert(offsetBound > 0);
            if (offBase > offsetBound + 3)
            {
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                );
            }
        }

        if (matchLength < matchLenLowerBound)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        return 0;
    }

    /* Returns an offset code, given a sequence's raw offset, the ongoing repcode array, and whether litLength == 0 */
    private static uint ZSTD_finalizeOffBase(uint rawOffset, uint* rep, uint ll0)
    {
        assert(rawOffset > 0);
        uint offBase = rawOffset + 3;
        if (ll0 == 0 && rawOffset == rep[0])
        {
            assert(1 >= 1);
            assert(1 <= 3);
            offBase = 1;
        }
        else if (rawOffset == rep[1])
        {
            assert(2 - ll0 >= 1);
            assert(2 - ll0 <= 3);
            offBase = 2 - ll0;
        }
        else if (rawOffset == rep[2])
        {
            assert(3 - ll0 >= 1);
            assert(3 - ll0 <= 3);
            offBase = 3 - ll0;
        }
        else if (ll0 != 0 && rawOffset == rep[0] - 1)
        {
            assert(3 >= 1);
            assert(3 <= 3);
            offBase = 3;
        }

        return offBase;
    }

    /* This function scans through an array of ZSTD_Sequence,
     * storing the sequences it reads, until it reaches a block delimiter.
     * Note that the block delimiter includes the last literals of the block.
     * @blockSize must be == sum(sequence_lengths).
     * @returns @blockSize on success, and a ZSTD_error otherwise.
     */
    private static nuint ZSTD_transferSequences_wBlockDelim(
        ZSTD_CCtx_s* cctx,
        ZSTD_SequencePosition* seqPos,
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        void* src,
        nuint blockSize,
        ZSTD_paramSwitch_e externalRepSearch
    )
    {
        uint idx = seqPos->idx;
        uint startIdx = idx;
        byte* ip = (byte*)src;
        byte* iend = ip + blockSize;
        repcodes_s updatedRepcodes;
        uint dictSize;
        if (cctx->cdict != null)
        {
            dictSize = (uint)cctx->cdict->dictContentSize;
        }
        else if (cctx->prefixDict.dict != null)
        {
            dictSize = (uint)cctx->prefixDict.dictSize;
        }
        else
        {
            dictSize = 0;
        }

        memcpy(updatedRepcodes.rep, cctx->blockState.prevCBlock->rep, (uint)sizeof(repcodes_s));
        for (; idx < inSeqsSize && (inSeqs[idx].matchLength != 0 || inSeqs[idx].offset != 0); ++idx)
        {
            uint litLength = inSeqs[idx].litLength;
            uint matchLength = inSeqs[idx].matchLength;
            uint offBase;
            if (externalRepSearch == ZSTD_paramSwitch_e.ZSTD_ps_disable)
            {
                assert(inSeqs[idx].offset > 0);
                offBase = inSeqs[idx].offset + 3;
            }
            else
            {
                uint ll0 = litLength == 0 ? 1U : 0U;
                offBase = ZSTD_finalizeOffBase(inSeqs[idx].offset, updatedRepcodes.rep, ll0);
                ZSTD_updateRep(updatedRepcodes.rep, offBase, ll0);
            }

            if (cctx->appliedParams.validateSequences != 0)
            {
                seqPos->posInSrc += litLength + matchLength;
                {
                    nuint err_code = ZSTD_validateSequence(
                        offBase,
                        matchLength,
                        cctx->appliedParams.cParams.minMatch,
                        seqPos->posInSrc,
                        cctx->appliedParams.cParams.windowLog,
                        dictSize,
                        ZSTD_hasExtSeqProd(&cctx->appliedParams)
                    );
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }
            }

            if (idx - seqPos->idx >= cctx->seqStore.maxNbSeq)
            {
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                );
            }

            ZSTD_storeSeq(&cctx->seqStore, litLength, ip, iend, offBase, matchLength);
            ip += matchLength + litLength;
        }

        if (idx == inSeqsSize)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        assert(externalRepSearch != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        assert(idx >= startIdx);
        if (externalRepSearch == ZSTD_paramSwitch_e.ZSTD_ps_disable && idx != startIdx)
        {
            uint* rep = updatedRepcodes.rep;
            /* index of last non-block-delimiter sequence */
            uint lastSeqIdx = idx - 1;
            if (lastSeqIdx >= startIdx + 2)
            {
                rep[2] = inSeqs[lastSeqIdx - 2].offset;
                rep[1] = inSeqs[lastSeqIdx - 1].offset;
                rep[0] = inSeqs[lastSeqIdx].offset;
            }
            else if (lastSeqIdx == startIdx + 1)
            {
                rep[2] = rep[0];
                rep[1] = inSeqs[lastSeqIdx - 1].offset;
                rep[0] = inSeqs[lastSeqIdx].offset;
            }
            else
            {
                assert(lastSeqIdx == startIdx);
                rep[2] = rep[1];
                rep[1] = rep[0];
                rep[0] = inSeqs[lastSeqIdx].offset;
            }
        }

        memcpy(cctx->blockState.nextCBlock->rep, updatedRepcodes.rep, (uint)sizeof(repcodes_s));
        if (inSeqs[idx].litLength != 0)
        {
            ZSTD_storeLastLiterals(&cctx->seqStore, ip, inSeqs[idx].litLength);
            ip += inSeqs[idx].litLength;
            seqPos->posInSrc += inSeqs[idx].litLength;
        }

        if (ip != iend)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        seqPos->idx = idx + 1;
        return blockSize;
    }

    /*
     * This function attempts to scan through @blockSize bytes in @src
     * represented by the sequences in @inSeqs,
     * storing any (partial) sequences.
     *
     * Occasionally, we may want to reduce the actual number of bytes consumed from @src
     * to avoid splitting a match, notably if it would produce a match smaller than MINMATCH.
     *
     * @returns the number of bytes consumed from @src, necessarily <= @blockSize.
     * Otherwise, it may return a ZSTD error if something went wrong.
     */
    private static nuint ZSTD_transferSequences_noDelim(
        ZSTD_CCtx_s* cctx,
        ZSTD_SequencePosition* seqPos,
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        void* src,
        nuint blockSize,
        ZSTD_paramSwitch_e externalRepSearch
    )
    {
        uint idx = seqPos->idx;
        uint startPosInSequence = seqPos->posInSequence;
        uint endPosInSequence = seqPos->posInSequence + (uint)blockSize;
        nuint dictSize;
        byte* istart = (byte*)src;
        byte* ip = istart;
        /* May be adjusted if we decide to process fewer than blockSize bytes */
        byte* iend = istart + blockSize;
        repcodes_s updatedRepcodes;
        uint bytesAdjustment = 0;
        uint finalMatchSplit = 0;
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

        memcpy(updatedRepcodes.rep, cctx->blockState.prevCBlock->rep, (uint)sizeof(repcodes_s));
        while (endPosInSequence != 0 && idx < inSeqsSize && finalMatchSplit == 0)
        {
            ZSTD_Sequence currSeq = inSeqs[idx];
            uint litLength = currSeq.litLength;
            uint matchLength = currSeq.matchLength;
            uint rawOffset = currSeq.offset;
            uint offBase;
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
            }
            else
            {
                if (endPosInSequence > litLength)
                {
                    uint firstHalfMatchLength;
                    litLength =
                        startPosInSequence >= litLength ? 0 : litLength - startPosInSequence;
                    firstHalfMatchLength = endPosInSequence - startPosInSequence - litLength;
                    if (
                        matchLength > blockSize
                        && firstHalfMatchLength >= cctx->appliedParams.cParams.minMatch
                    )
                    {
                        /* Only ever split the match if it is larger than the block size */
                        uint secondHalfMatchLength =
                            currSeq.matchLength + currSeq.litLength - endPosInSequence;
                        if (secondHalfMatchLength < cctx->appliedParams.cParams.minMatch)
                        {
                            endPosInSequence -=
                                cctx->appliedParams.cParams.minMatch - secondHalfMatchLength;
                            bytesAdjustment =
                                cctx->appliedParams.cParams.minMatch - secondHalfMatchLength;
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
                uint ll0 = litLength == 0 ? 1U : 0U;
                offBase = ZSTD_finalizeOffBase(rawOffset, updatedRepcodes.rep, ll0);
                ZSTD_updateRep(updatedRepcodes.rep, offBase, ll0);
            }

            if (cctx->appliedParams.validateSequences != 0)
            {
                seqPos->posInSrc += litLength + matchLength;
                {
                    nuint err_code = ZSTD_validateSequence(
                        offBase,
                        matchLength,
                        cctx->appliedParams.cParams.minMatch,
                        seqPos->posInSrc,
                        cctx->appliedParams.cParams.windowLog,
                        dictSize,
                        ZSTD_hasExtSeqProd(&cctx->appliedParams)
                    );
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }
            }

            if (idx - seqPos->idx >= cctx->seqStore.maxNbSeq)
            {
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                );
            }

            ZSTD_storeSeq(&cctx->seqStore, litLength, ip, iend, offBase, matchLength);
            ip += matchLength + litLength;
            if (finalMatchSplit == 0)
                idx++;
        }

        assert(
            idx == inSeqsSize || endPosInSequence <= inSeqs[idx].litLength + inSeqs[idx].matchLength
        );
        seqPos->idx = idx;
        seqPos->posInSequence = endPosInSequence;
        memcpy(cctx->blockState.nextCBlock->rep, updatedRepcodes.rep, (uint)sizeof(repcodes_s));
        iend -= bytesAdjustment;
        if (ip != iend)
        {
            /* Store any last literals */
            uint lastLLSize = (uint)(iend - ip);
            assert(ip <= iend);
            ZSTD_storeLastLiterals(&cctx->seqStore, ip, lastLLSize);
            seqPos->posInSrc += lastLLSize;
        }

        return (nuint)(iend - istart);
    }

    private static void* ZSTD_selectSequenceCopier(ZSTD_sequenceFormat_e mode)
    {
        assert(
            ZSTD_cParam_withinBounds(ZSTD_cParameter.ZSTD_c_experimentalParam11, (int)mode) != 0
        );
        if (mode == ZSTD_sequenceFormat_e.ZSTD_sf_explicitBlockDelimiters)
        {
            return (delegate* managed<
                ZSTD_CCtx_s*,
                ZSTD_SequencePosition*,
                ZSTD_Sequence*,
                nuint,
                void*,
                nuint,
                ZSTD_paramSwitch_e,
                nuint>)(&ZSTD_transferSequences_wBlockDelim);
        }

        assert(mode == ZSTD_sequenceFormat_e.ZSTD_sf_noBlockDelimiters);
        return (delegate* managed<
            ZSTD_CCtx_s*,
            ZSTD_SequencePosition*,
            ZSTD_Sequence*,
            nuint,
            void*,
            nuint,
            ZSTD_paramSwitch_e,
            nuint>)(&ZSTD_transferSequences_noDelim);
    }

    /* Discover the size of next block by searching for the delimiter.
     * Note that a block delimiter **must** exist in this mode,
     * otherwise it's an input error.
     * The block size retrieved will be later compared to ensure it remains within bounds */
    private static nuint blockSize_explicitDelimiter(
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        ZSTD_SequencePosition seqPos
    )
    {
        int end = 0;
        nuint blockSize = 0;
        nuint spos = seqPos.idx;
        assert(spos <= inSeqsSize);
        while (spos < inSeqsSize)
        {
            end = inSeqs[spos].offset == 0 ? 1 : 0;
            blockSize += inSeqs[spos].litLength + inSeqs[spos].matchLength;
            if (end != 0)
            {
                if (inSeqs[spos].matchLength != 0)
                    return unchecked(
                        (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                    );
                break;
            }

            spos++;
        }

        if (end == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        return blockSize;
    }

    private static nuint determine_blockSize(
        ZSTD_sequenceFormat_e mode,
        nuint blockSize,
        nuint remaining,
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        ZSTD_SequencePosition seqPos
    )
    {
        if (mode == ZSTD_sequenceFormat_e.ZSTD_sf_noBlockDelimiters)
        {
            return remaining < blockSize ? remaining : blockSize;
        }

        assert(mode == ZSTD_sequenceFormat_e.ZSTD_sf_explicitBlockDelimiters);
        {
            nuint explicitBlockSize = blockSize_explicitDelimiter(inSeqs, inSeqsSize, seqPos);
            {
                nuint err_code = explicitBlockSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (explicitBlockSize > blockSize)
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                );
            if (explicitBlockSize > remaining)
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                );
            return explicitBlockSize;
        }
    }

    /* Compress all provided sequences, block-by-block.
     *
     * Returns the cumulative size of all compressed blocks (including their headers),
     * otherwise a ZSTD error.
     */
    private static nuint ZSTD_compressSequences_internal(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        void* src,
        nuint srcSize
    )
    {
        nuint cSize = 0;
        nuint remaining = srcSize;
        ZSTD_SequencePosition seqPos = new ZSTD_SequencePosition
        {
            idx = 0,
            posInSequence = 0,
            posInSrc = 0,
        };
        byte* ip = (byte*)src;
        byte* op = (byte*)dst;
        void* sequenceCopier = ZSTD_selectSequenceCopier(cctx->appliedParams.blockDelimiters);
        if (remaining == 0)
        {
            /* last block */
            uint cBlockHeader24 = 1 + ((uint)blockType_e.bt_raw << 1);
            if (dstCapacity < 4)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            MEM_writeLE32(op, cBlockHeader24);
            op += ZSTD_blockHeaderSize;
            dstCapacity -= ZSTD_blockHeaderSize;
            cSize += ZSTD_blockHeaderSize;
        }

        while (remaining != 0)
        {
            nuint compressedSeqsSize;
            nuint cBlockSize;
            nuint blockSize = determine_blockSize(
                cctx->appliedParams.blockDelimiters,
                cctx->blockSizeMax,
                remaining,
                inSeqs,
                inSeqsSize,
                seqPos
            );
            uint lastBlock = blockSize == remaining ? 1U : 0U;
            {
                nuint err_code = blockSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(blockSize <= remaining);
            ZSTD_resetSeqStore(&cctx->seqStore);
            blockSize = (
                (delegate* managed<
                    ZSTD_CCtx_s*,
                    ZSTD_SequencePosition*,
                    ZSTD_Sequence*,
                    nuint,
                    void*,
                    nuint,
                    ZSTD_paramSwitch_e,
                    nuint>)sequenceCopier
            )(
                cctx,
                &seqPos,
                inSeqs,
                inSeqsSize,
                ip,
                blockSize,
                cctx->appliedParams.searchForExternalRepcodes
            );
            {
                nuint err_code = blockSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (blockSize < (nuint)(1 + 1) + ZSTD_blockHeaderSize + 1 + 1)
            {
                cBlockSize = ZSTD_noCompressBlock(op, dstCapacity, ip, blockSize, lastBlock);
                {
                    nuint err_code = cBlockSize;
                    if (ERR_isError(err_code))
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

            if (dstCapacity < ZSTD_blockHeaderSize)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            compressedSeqsSize = ZSTD_entropyCompressSeqStore(
                &cctx->seqStore,
                &cctx->blockState.prevCBlock->entropy,
                &cctx->blockState.nextCBlock->entropy,
                &cctx->appliedParams,
                op + ZSTD_blockHeaderSize,
                dstCapacity - ZSTD_blockHeaderSize,
                blockSize,
                cctx->tmpWorkspace,
                cctx->tmpWkspSize,
                cctx->bmi2
            );
            {
                nuint err_code = compressedSeqsSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (
                cctx->isFirstBlock == 0
                && ZSTD_maybeRLE(&cctx->seqStore) != 0
                && ZSTD_isRLE(ip, blockSize) != 0
            )
            {
                compressedSeqsSize = 1;
            }

            if (compressedSeqsSize == 0)
            {
                cBlockSize = ZSTD_noCompressBlock(op, dstCapacity, ip, blockSize, lastBlock);
                {
                    nuint err_code = cBlockSize;
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }
            }
            else if (compressedSeqsSize == 1)
            {
                cBlockSize = ZSTD_rleCompressBlock(op, dstCapacity, *ip, blockSize, lastBlock);
                {
                    nuint err_code = cBlockSize;
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }
            }
            else
            {
                uint cBlockHeader;
                ZSTD_blockState_confirmRepcodesAndEntropyTables(&cctx->blockState);
                if (
                    cctx->blockState.prevCBlock->entropy.fse.offcode_repeatMode
                    == FSE_repeat.FSE_repeat_valid
                )
                    cctx->blockState.prevCBlock->entropy.fse.offcode_repeatMode =
                        FSE_repeat.FSE_repeat_check;
                cBlockHeader =
                    lastBlock
                    + ((uint)blockType_e.bt_compressed << 1)
                    + (uint)(compressedSeqsSize << 3);
                MEM_writeLE24(op, cBlockHeader);
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
     * Compress an array of ZSTD_Sequence, associated with @src buffer, into dst.
     * @src contains the entire input (not just the literals).
     * If @srcSize > sum(sequence.length), the remaining bytes are considered all literals
     * If a dictionary is included, then the cctx should reference the dict (see: ZSTD_CCtx_refCDict(), ZSTD_CCtx_loadDictionary(), etc.).
     * The entire source is compressed into a single frame.
     *
     * The compression behavior changes based on cctx params. In particular:
     *    If ZSTD_c_blockDelimiters == ZSTD_sf_noBlockDelimiters, the array of ZSTD_Sequence is expected to contain
     *    no block delimiters (defined in ZSTD_Sequence). Block boundaries are roughly determined based on
     *    the block size derived from the cctx, and sequences may be split. This is the default setting.
     *
     *    If ZSTD_c_blockDelimiters == ZSTD_sf_explicitBlockDelimiters, the array of ZSTD_Sequence is expected to contain
     *    valid block delimiters (defined in ZSTD_Sequence). Behavior is undefined if no block delimiters are provided.
     *
     *    When ZSTD_c_blockDelimiters == ZSTD_sf_explicitBlockDelimiters, it's possible to decide generating repcodes
     *    using the advanced parameter ZSTD_c_repcodeResolution. Repcodes will improve compression ratio, though the benefit
     *    can vary greatly depending on Sequences. On the other hand, repcode resolution is an expensive operation.
     *    By default, it's disabled at low (<10) compression levels, and enabled above the threshold (>=10).
     *    ZSTD_c_repcodeResolution makes it possible to directly manage this processing in either direction.
     *
     *    If ZSTD_c_validateSequences == 0, this function blindly accepts the Sequences provided. Invalid Sequences cause undefined
     *    behavior. If ZSTD_c_validateSequences == 1, then the function will detect invalid Sequences (see doc/zstd_compression_format.md for
     *    specifics regarding offset/matchlength requirements) and then bail out and return an error.
     *
     *    In addition to the two adjustable experimental params, there are other important cctx params.
     *    - ZSTD_c_minMatch MUST be set as less than or equal to the smallest match generated by the match finder. It has a minimum value of ZSTD_MINMATCH_MIN.
     *    - ZSTD_c_compressionLevel accordingly adjusts the strength of the entropy coder, as it would in typical compression.
     *    - ZSTD_c_windowLog affects offset validation: this function will return an error at higher debug levels if a provided offset
     *      is larger than what the spec allows for a given window log and dictionary (if present). See: doc/zstd_compression_format.md
     *
     * Note: Repcodes are, as of now, always re-calculated within this function, ZSTD_Sequence.rep is effectively unused.
     * Dev Note: Once ability to ingest repcodes become available, the explicit block delims mode must respect those repcodes exactly,
     *         and cannot emit an RLE block that disagrees with the repcode history.
     * @return : final compressed size, or a ZSTD error code.
     */
    public static nuint ZSTD_compressSequences(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        void* src,
        nuint srcSize
    )
    {
        byte* op = (byte*)dst;
        nuint cSize = 0;
        assert(cctx != null);
        {
            nuint err_code = ZSTD_CCtx_init_compressStream2(
                cctx,
                ZSTD_EndDirective.ZSTD_e_end,
                srcSize
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        {
            nuint frameHeaderSize = ZSTD_writeFrameHeader(
                op,
                dstCapacity,
                &cctx->appliedParams,
                srcSize,
                cctx->dictID
            );
            op += frameHeaderSize;
            assert(frameHeaderSize <= dstCapacity);
            dstCapacity -= frameHeaderSize;
            cSize += frameHeaderSize;
        }

        if (cctx->appliedParams.fParams.checksumFlag != 0 && srcSize != 0)
        {
            ZSTD_XXH64_update(&cctx->xxhState, src, srcSize);
        }

        {
            nuint cBlocksSize = ZSTD_compressSequences_internal(
                cctx,
                op,
                dstCapacity,
                inSeqs,
                inSeqsSize,
                src,
                srcSize
            );
            {
                nuint err_code = cBlocksSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            cSize += cBlocksSize;
            assert(cBlocksSize <= dstCapacity);
            dstCapacity -= cBlocksSize;
        }

        if (cctx->appliedParams.fParams.checksumFlag != 0)
        {
            uint checksum = (uint)ZSTD_XXH64_digest(&cctx->xxhState);
            if (dstCapacity < 4)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            MEM_writeLE32((sbyte*)dst + cSize, checksum);
            cSize += 4;
        }

        return cSize;
    }

    private static nuint convertSequences_noRepcodes(
        SeqDef_s* dstSeqs,
        ZSTD_Sequence* inSeqs,
        nuint nbSequences
    )
    {
        nuint longLen = 0;
        nuint n;
        for (n = 0; n < nbSequences; n++)
        {
            assert(inSeqs[n].offset > 0);
            dstSeqs[n].offBase = inSeqs[n].offset + 3;
            dstSeqs[n].litLength = (ushort)inSeqs[n].litLength;
            dstSeqs[n].mlBase = (ushort)(inSeqs[n].matchLength - 3);
            if (inSeqs[n].matchLength > 65535 + 3)
            {
                assert(longLen == 0);
                longLen = n + 1;
            }

            if (inSeqs[n].litLength > 65535)
            {
                assert(longLen == 0);
                longLen = n + nbSequences + 1;
            }
        }

        return longLen;
    }

    /*
     * Precondition: Sequences must end on an explicit Block Delimiter
     * @return: 0 on success, or an error code.
     * Note: Sequence validation functionality has been disabled (removed).
     * This is helpful to generate a lean main pipeline, improving performance.
     * It may be re-inserted later.
     */
    private static nuint ZSTD_convertBlockSequences(
        ZSTD_CCtx_s* cctx,
        ZSTD_Sequence* inSeqs,
        nuint nbSequences,
        int repcodeResolution
    )
    {
        repcodes_s updatedRepcodes;
        nuint seqNb = 0;
        if (nbSequences >= cctx->seqStore.maxNbSeq)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        memcpy(updatedRepcodes.rep, cctx->blockState.prevCBlock->rep, (uint)sizeof(repcodes_s));
        assert(nbSequences >= 1);
        assert(inSeqs[nbSequences - 1].matchLength == 0);
        assert(inSeqs[nbSequences - 1].offset == 0);
        if (repcodeResolution == 0)
        {
            nuint longl = convertSequences_noRepcodes(
                cctx->seqStore.sequencesStart,
                inSeqs,
                nbSequences - 1
            );
            cctx->seqStore.sequences = cctx->seqStore.sequencesStart + nbSequences - 1;
            if (longl != 0)
            {
                assert(cctx->seqStore.longLengthType == ZSTD_longLengthType_e.ZSTD_llt_none);
                if (longl <= nbSequences - 1)
                {
                    cctx->seqStore.longLengthType = ZSTD_longLengthType_e.ZSTD_llt_matchLength;
                    cctx->seqStore.longLengthPos = (uint)(longl - 1);
                }
                else
                {
                    assert(longl <= 2 * (nbSequences - 1));
                    cctx->seqStore.longLengthType = ZSTD_longLengthType_e.ZSTD_llt_literalLength;
                    cctx->seqStore.longLengthPos = (uint)(longl - (nbSequences - 1) - 1);
                }
            }
        }
        else
        {
            for (seqNb = 0; seqNb < nbSequences - 1; seqNb++)
            {
                uint litLength = inSeqs[seqNb].litLength;
                uint matchLength = inSeqs[seqNb].matchLength;
                uint ll0 = litLength == 0 ? 1U : 0U;
                uint offBase = ZSTD_finalizeOffBase(inSeqs[seqNb].offset, updatedRepcodes.rep, ll0);
                ZSTD_storeSeqOnly(&cctx->seqStore, litLength, offBase, matchLength);
                ZSTD_updateRep(updatedRepcodes.rep, offBase, ll0);
            }
        }

        if (repcodeResolution == 0 && nbSequences > 1)
        {
            uint* rep = updatedRepcodes.rep;
            if (nbSequences >= 4)
            {
                /* index of last full sequence */
                uint lastSeqIdx = (uint)nbSequences - 2;
                rep[2] = inSeqs[lastSeqIdx - 2].offset;
                rep[1] = inSeqs[lastSeqIdx - 1].offset;
                rep[0] = inSeqs[lastSeqIdx].offset;
            }
            else if (nbSequences == 3)
            {
                rep[2] = rep[0];
                rep[1] = inSeqs[0].offset;
                rep[0] = inSeqs[1].offset;
            }
            else
            {
                assert(nbSequences == 2);
                rep[2] = rep[1];
                rep[1] = rep[0];
                rep[0] = inSeqs[0].offset;
            }
        }

        memcpy(cctx->blockState.nextCBlock->rep, updatedRepcodes.rep, (uint)sizeof(repcodes_s));
        return 0;
    }

    private static BlockSummary ZSTD_get1BlockSummary(ZSTD_Sequence* seqs, nuint nbSeqs)
    {
        nuint totalMatchSize = 0;
        nuint litSize = 0;
        nuint n;
        assert(seqs != null);
        for (n = 0; n < nbSeqs; n++)
        {
            totalMatchSize += seqs[n].matchLength;
            litSize += seqs[n].litLength;
            if (seqs[n].matchLength == 0)
            {
                assert(seqs[n].offset == 0);
                break;
            }
        }

        if (n == nbSeqs)
        {
            BlockSummary bs;
            System.Runtime.CompilerServices.Unsafe.SkipInit(out bs);
            bs.nbSequences = unchecked(
                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
            );
            return bs;
        }

        {
            BlockSummary bs;
            bs.nbSequences = n + 1;
            bs.blockSize = litSize + totalMatchSize;
            bs.litSize = litSize;
            return bs;
        }
    }

    private static nuint ZSTD_compressSequencesAndLiterals_internal(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        ZSTD_Sequence* inSeqs,
        nuint nbSequences,
        void* literals,
        nuint litSize,
        nuint srcSize
    )
    {
        nuint remaining = srcSize;
        nuint cSize = 0;
        byte* op = (byte*)dst;
        int repcodeResolution =
            cctx->appliedParams.searchForExternalRepcodes == ZSTD_paramSwitch_e.ZSTD_ps_enable
                ? 1
                : 0;
        assert(cctx->appliedParams.searchForExternalRepcodes != ZSTD_paramSwitch_e.ZSTD_ps_auto);
        if (nbSequences == 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        if (nbSequences == 1 && inSeqs[0].litLength == 0)
        {
            /* last block */
            uint cBlockHeader24 = 1 + ((uint)blockType_e.bt_raw << 1);
            if (dstCapacity < 3)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            MEM_writeLE24(op, cBlockHeader24);
            op += ZSTD_blockHeaderSize;
            dstCapacity -= ZSTD_blockHeaderSize;
            cSize += ZSTD_blockHeaderSize;
        }

        while (nbSequences != 0)
        {
            nuint compressedSeqsSize,
                cBlockSize,
                conversionStatus;
            BlockSummary block = ZSTD_get1BlockSummary(inSeqs, nbSequences);
            uint lastBlock = block.nbSequences == nbSequences ? 1U : 0U;
            {
                nuint err_code = block.nbSequences;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(block.nbSequences <= nbSequences);
            if (block.litSize > litSize)
            {
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid)
                );
            }

            ZSTD_resetSeqStore(&cctx->seqStore);
            conversionStatus = ZSTD_convertBlockSequences(
                cctx,
                inSeqs,
                block.nbSequences,
                repcodeResolution
            );
            {
                nuint err_code = conversionStatus;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            inSeqs += block.nbSequences;
            nbSequences -= block.nbSequences;
            remaining -= block.blockSize;
            if (dstCapacity < ZSTD_blockHeaderSize)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            compressedSeqsSize = ZSTD_entropyCompressSeqStore_internal(
                op + ZSTD_blockHeaderSize,
                dstCapacity - ZSTD_blockHeaderSize,
                literals,
                block.litSize,
                &cctx->seqStore,
                &cctx->blockState.prevCBlock->entropy,
                &cctx->blockState.nextCBlock->entropy,
                &cctx->appliedParams,
                cctx->tmpWorkspace,
                cctx->tmpWkspSize,
                cctx->bmi2
            );
            {
                nuint err_code = compressedSeqsSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (compressedSeqsSize > cctx->blockSizeMax)
                compressedSeqsSize = 0;
            litSize -= block.litSize;
            literals = (sbyte*)literals + block.litSize;
            if (compressedSeqsSize == 0)
            {
                return unchecked(
                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_cannotProduce_uncompressedBlock)
                );
            }
            else
            {
                uint cBlockHeader;
                assert(compressedSeqsSize > 1);
                ZSTD_blockState_confirmRepcodesAndEntropyTables(&cctx->blockState);
                if (
                    cctx->blockState.prevCBlock->entropy.fse.offcode_repeatMode
                    == FSE_repeat.FSE_repeat_valid
                )
                    cctx->blockState.prevCBlock->entropy.fse.offcode_repeatMode =
                        FSE_repeat.FSE_repeat_check;
                cBlockHeader =
                    lastBlock
                    + ((uint)blockType_e.bt_compressed << 1)
                    + (uint)(compressedSeqsSize << 3);
                MEM_writeLE24(op, cBlockHeader);
                cBlockSize = ZSTD_blockHeaderSize + compressedSeqsSize;
            }

            cSize += cBlockSize;
            op += cBlockSize;
            dstCapacity -= cBlockSize;
            cctx->isFirstBlock = 0;
            if (lastBlock != 0)
            {
                assert(nbSequences == 0);
                break;
            }
        }

        if (litSize != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        if (remaining != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_externalSequences_invalid));
        }

        return cSize;
    }

    /*! ZSTD_compressSequencesAndLiterals() :
     * This is a variant of ZSTD_compressSequences() which,
     * instead of receiving (src,srcSize) as input parameter, receives (literals,litSize),
     * aka all the literals, already extracted and laid out into a single continuous buffer.
     * This can be useful if the process generating the sequences also happens to generate the buffer of literals,
     * thus skipping an extraction + caching stage.
     * It's a speed optimization, useful when the right conditions are met,
     * but it also features the following limitations:
     * - Only supports explicit delimiter mode
     * - Currently does not support Sequences validation (so input Sequences are trusted)
     * - Not compatible with frame checksum, which must be disabled
     * - If any block is incompressible, will fail and return an error
     * - @litSize must be == sum of all @.litLength fields in @inSeqs. Any discrepancy will generate an error.
     * - @litBufCapacity is the size of the underlying buffer into which literals are written, starting at address @literals.
     *   @litBufCapacity must be at least 8 bytes larger than @litSize.
     * - @decompressedSize must be correct, and correspond to the sum of all Sequences. Any discrepancy will generate an error.
     * @return : final compressed size, or a ZSTD error code.
     */
    public static nuint ZSTD_compressSequencesAndLiterals(
        ZSTD_CCtx_s* cctx,
        void* dst,
        nuint dstCapacity,
        ZSTD_Sequence* inSeqs,
        nuint inSeqsSize,
        void* literals,
        nuint litSize,
        nuint litCapacity,
        nuint decompressedSize
    )
    {
        byte* op = (byte*)dst;
        nuint cSize = 0;
        assert(cctx != null);
        if (litCapacity < litSize)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall));
        }

        {
            nuint err_code = ZSTD_CCtx_init_compressStream2(
                cctx,
                ZSTD_EndDirective.ZSTD_e_end,
                decompressedSize
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        if (cctx->appliedParams.blockDelimiters == ZSTD_sequenceFormat_e.ZSTD_sf_noBlockDelimiters)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_unsupported));
        }

        if (cctx->appliedParams.validateSequences != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported));
        }

        if (cctx->appliedParams.fParams.checksumFlag != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_unsupported));
        }

        {
            nuint frameHeaderSize = ZSTD_writeFrameHeader(
                op,
                dstCapacity,
                &cctx->appliedParams,
                decompressedSize,
                cctx->dictID
            );
            op += frameHeaderSize;
            assert(frameHeaderSize <= dstCapacity);
            dstCapacity -= frameHeaderSize;
            cSize += frameHeaderSize;
        }

        {
            nuint cBlocksSize = ZSTD_compressSequencesAndLiterals_internal(
                cctx,
                op,
                dstCapacity,
                inSeqs,
                inSeqsSize,
                literals,
                litSize,
                decompressedSize
            );
            {
                nuint err_code = cBlocksSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            cSize += cBlocksSize;
            assert(cBlocksSize <= dstCapacity);
            dstCapacity -= cBlocksSize;
        }

        return cSize;
    }

    /*======   Finalize   ======*/
    private static ZSTD_inBuffer_s inBuffer_forEndFlush(ZSTD_CCtx_s* zcs)
    {
        ZSTD_inBuffer_s nullInput = new ZSTD_inBuffer_s
        {
            src = null,
            size = 0,
            pos = 0,
        };
        int stableInput =
            zcs->appliedParams.inBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable ? 1 : 0;
        return stableInput != 0 ? zcs->expectedInBuffer : nullInput;
    }

    /*! ZSTD_flushStream() :
     * @return : amount of data remaining to flush */
    public static nuint ZSTD_flushStream(ZSTD_CCtx_s* zcs, ZSTD_outBuffer_s* output)
    {
        ZSTD_inBuffer_s input = inBuffer_forEndFlush(zcs);
        input.size = input.pos;
        return ZSTD_compressStream2(zcs, output, &input, ZSTD_EndDirective.ZSTD_e_flush);
    }

    /*! Equivalent to ZSTD_compressStream2(zcs, output, &emptyInput, ZSTD_e_end). */
    public static nuint ZSTD_endStream(ZSTD_CCtx_s* zcs, ZSTD_outBuffer_s* output)
    {
        ZSTD_inBuffer_s input = inBuffer_forEndFlush(zcs);
        nuint remainingToFlush = ZSTD_compressStream2(
            zcs,
            output,
            &input,
            ZSTD_EndDirective.ZSTD_e_end
        );
        {
            nuint err_code = remainingToFlush;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        if (zcs->appliedParams.nbWorkers > 0)
            return remainingToFlush;
        {
            nuint lastBlockSize = (nuint)(zcs->frameEnded != 0 ? 0 : 3);
            nuint checksumSize = (nuint)(
                zcs->frameEnded != 0 ? 0 : zcs->appliedParams.fParams.checksumFlag * 4
            );
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
        return -(1 << 17);
    }

    public static int ZSTD_defaultCLevel()
    {
        return 3;
    }

    private static ZSTD_compressionParameters ZSTD_dedicatedDictSearch_getCParams(
        int compressionLevel,
        nuint dictSize
    )
    {
        ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
            compressionLevel,
            0,
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_createCDict
        );
        switch (cParams.strategy)
        {
            case ZSTD_strategy.ZSTD_fast:
            case ZSTD_strategy.ZSTD_dfast:
                break;
            case ZSTD_strategy.ZSTD_greedy:
            case ZSTD_strategy.ZSTD_lazy:
            case ZSTD_strategy.ZSTD_lazy2:
                cParams.hashLog += 2;
                break;
            case ZSTD_strategy.ZSTD_btlazy2:
            case ZSTD_strategy.ZSTD_btopt:
            case ZSTD_strategy.ZSTD_btultra:
            case ZSTD_strategy.ZSTD_btultra2:
                break;
        }

        return cParams;
    }

    private static int ZSTD_dedicatedDictSearch_isSupported(ZSTD_compressionParameters* cParams)
    {
        return
            cParams->strategy >= ZSTD_strategy.ZSTD_greedy
            && cParams->strategy <= ZSTD_strategy.ZSTD_lazy2
            && cParams->hashLog > cParams->chainLog
            && cParams->chainLog <= 24
            ? 1
            : 0;
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
                break;
            case ZSTD_strategy.ZSTD_greedy:
            case ZSTD_strategy.ZSTD_lazy:
            case ZSTD_strategy.ZSTD_lazy2:
                cParams->hashLog -= 2;
                if (cParams->hashLog < 6)
                {
                    cParams->hashLog = 6;
                }

                break;
            case ZSTD_strategy.ZSTD_btlazy2:
            case ZSTD_strategy.ZSTD_btopt:
            case ZSTD_strategy.ZSTD_btultra:
            case ZSTD_strategy.ZSTD_btultra2:
                break;
        }
    }

    private static ulong ZSTD_getCParamRowSize(
        ulong srcSizeHint,
        nuint dictSize,
        ZSTD_CParamMode_e mode
    )
    {
        switch (mode)
        {
            case ZSTD_CParamMode_e.ZSTD_cpm_unknown:
            case ZSTD_CParamMode_e.ZSTD_cpm_noAttachDict:
            case ZSTD_CParamMode_e.ZSTD_cpm_createCDict:
                break;
            case ZSTD_CParamMode_e.ZSTD_cpm_attachDict:
                dictSize = 0;
                break;
            default:
                assert(0 != 0);
                break;
        }

        {
            int unknown = srcSizeHint == unchecked(0UL - 1) ? 1 : 0;
            nuint addedSize = (nuint)(unknown != 0 && dictSize > 0 ? 500 : 0);
            return unknown != 0 && dictSize == 0
                ? unchecked(0UL - 1)
                : srcSizeHint + dictSize + addedSize;
        }
    }

    /*! ZSTD_getCParams_internal() :
     * @return ZSTD_compressionParameters structure for a selected compression level, srcSize and dictSize.
     *  Note: srcSizeHint 0 means 0, use ZSTD_CONTENTSIZE_UNKNOWN for unknown.
     *        Use dictSize == 0 for unknown or unused.
     *  Note: `mode` controls how we treat the `dictSize`. See docs for `ZSTD_CParamMode_e`. */
    private static ZSTD_compressionParameters ZSTD_getCParams_internal(
        int compressionLevel,
        ulong srcSizeHint,
        nuint dictSize,
        ZSTD_CParamMode_e mode
    )
    {
        ulong rSize = ZSTD_getCParamRowSize(srcSizeHint, dictSize, mode);
        uint tableID = (uint)(
            (rSize <= 256 * (1 << 10) ? 1 : 0)
            + (rSize <= 128 * (1 << 10) ? 1 : 0)
            + (rSize <= 16 * (1 << 10) ? 1 : 0)
        );
        int row;
        if (compressionLevel == 0)
            row = 3;
        else if (compressionLevel < 0)
            row = 0;
        else if (compressionLevel > 22)
            row = 22;
        else
            row = compressionLevel;
        {
            ZSTD_compressionParameters cp = ZSTD_defaultCParameters[tableID][row];
            if (compressionLevel < 0)
            {
                int clampedCompressionLevel =
                    ZSTD_minCLevel() > compressionLevel ? ZSTD_minCLevel() : compressionLevel;
                cp.targetLength = (uint)-clampedCompressionLevel;
            }

            return ZSTD_adjustCParams_internal(
                cp,
                srcSizeHint,
                dictSize,
                mode,
                ZSTD_paramSwitch_e.ZSTD_ps_auto
            );
        }
    }

    /*! ZSTD_getCParams() :
     * @return ZSTD_compressionParameters structure for a selected compression level, srcSize and dictSize.
     *  Size values are optional, provide 0 if not known or unused */
    public static ZSTD_compressionParameters ZSTD_getCParams(
        int compressionLevel,
        ulong srcSizeHint,
        nuint dictSize
    )
    {
        if (srcSizeHint == 0)
            srcSizeHint = unchecked(0UL - 1);
        return ZSTD_getCParams_internal(
            compressionLevel,
            srcSizeHint,
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_unknown
        );
    }

    /*! ZSTD_getParams() :
     *  same idea as ZSTD_getCParams()
     * @return a `ZSTD_parameters` structure (instead of `ZSTD_compressionParameters`).
     *  Fields of `ZSTD_frameParameters` are set to default values */
    private static ZSTD_parameters ZSTD_getParams_internal(
        int compressionLevel,
        ulong srcSizeHint,
        nuint dictSize,
        ZSTD_CParamMode_e mode
    )
    {
        ZSTD_parameters @params;
        ZSTD_compressionParameters cParams = ZSTD_getCParams_internal(
            compressionLevel,
            srcSizeHint,
            dictSize,
            mode
        );
        @params = new ZSTD_parameters { cParams = cParams };
        @params.fParams.contentSizeFlag = 1;
        return @params;
    }

    /*! ZSTD_getParams() :
     *  same idea as ZSTD_getCParams()
     * @return a `ZSTD_parameters` structure (instead of `ZSTD_compressionParameters`).
     *  Fields of `ZSTD_frameParameters` are set to default values */
    public static ZSTD_parameters ZSTD_getParams(
        int compressionLevel,
        ulong srcSizeHint,
        nuint dictSize
    )
    {
        if (srcSizeHint == 0)
            srcSizeHint = unchecked(0UL - 1);
        return ZSTD_getParams_internal(
            compressionLevel,
            srcSizeHint,
            dictSize,
            ZSTD_CParamMode_e.ZSTD_cpm_unknown
        );
    }

    /*! ZSTD_registerSequenceProducer() :
     * Instruct zstd to use a block-level external sequence producer function.
     *
     * The sequenceProducerState must be initialized by the caller, and the caller is
     * responsible for managing its lifetime. This parameter is sticky across
     * compressions. It will remain set until the user explicitly resets compression
     * parameters.
     *
     * Sequence producer registration is considered to be an "advanced parameter",
     * part of the "advanced API". This means it will only have an effect on compression
     * APIs which respect advanced parameters, such as compress2() and compressStream2().
     * Older compression APIs such as compressCCtx(), which predate the introduction of
     * "advanced parameters", will ignore any external sequence producer setting.
     *
     * The sequence producer can be "cleared" by registering a NULL function pointer. This
     * removes all limitations described above in the "LIMITATIONS" section of the API docs.
     *
     * The user is strongly encouraged to read the full API documentation (above) before
     * calling this function. */
    public static void ZSTD_registerSequenceProducer(
        ZSTD_CCtx_s* zc,
        void* extSeqProdState,
        void* extSeqProdFunc
    )
    {
        assert(zc != null);
        ZSTD_CCtxParams_registerSequenceProducer(
            &zc->requestedParams,
            extSeqProdState,
            extSeqProdFunc
        );
    }

    /*! ZSTD_CCtxParams_registerSequenceProducer() :
     * Same as ZSTD_registerSequenceProducer(), but operates on ZSTD_CCtx_params.
     * This is used for accurate size estimation with ZSTD_estimateCCtxSize_usingCCtxParams(),
     * which is needed when creating a ZSTD_CCtx with ZSTD_initStaticCCtx().
     *
     * If you are using the external sequence producer API in a scenario where ZSTD_initStaticCCtx()
     * is required, then this function is for you. Otherwise, you probably don't need it.
     *
     * See tests/zstreamtest.c for example usage. */
    public static void ZSTD_CCtxParams_registerSequenceProducer(
        ZSTD_CCtx_params_s* @params,
        void* extSeqProdState,
        void* extSeqProdFunc
    )
    {
        assert(@params != null);
        if (extSeqProdFunc != null)
        {
            @params->extSeqProdFunc = extSeqProdFunc;
            @params->extSeqProdState = extSeqProdState;
        }
        else
        {
            @params->extSeqProdFunc = null;
            @params->extSeqProdState = null;
        }
    }
}
