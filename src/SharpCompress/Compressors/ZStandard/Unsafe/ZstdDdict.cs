using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /* note: several prototypes are already published in `zstd.h` :
     * ZSTD_createDDict()
     * ZSTD_createDDict_byReference()
     * ZSTD_createDDict_advanced()
     * ZSTD_freeDDict()
     * ZSTD_initStaticDDict()
     * ZSTD_sizeof_DDict()
     * ZSTD_estimateDDictSize()
     * ZSTD_getDictID_fromDict()
     */
    private static void* ZSTD_DDict_dictContent(ZSTD_DDict_s* ddict)
    {
        assert(ddict != null);
        return ddict->dictContent;
    }

    private static nuint ZSTD_DDict_dictSize(ZSTD_DDict_s* ddict)
    {
        assert(ddict != null);
        return ddict->dictSize;
    }

    private static void ZSTD_copyDDictParameters(ZSTD_DCtx_s* dctx, ZSTD_DDict_s* ddict)
    {
        assert(dctx != null);
        assert(ddict != null);
        dctx->dictID = ddict->dictID;
        dctx->prefixStart = ddict->dictContent;
        dctx->virtualStart = ddict->dictContent;
        dctx->dictEnd = (byte*)ddict->dictContent + ddict->dictSize;
        dctx->previousDstEnd = dctx->dictEnd;
        if (ddict->entropyPresent != 0)
        {
            dctx->litEntropy = 1;
            dctx->fseEntropy = 1;
            dctx->LLTptr = &ddict->entropy.LLTable.e0;
            dctx->MLTptr = &ddict->entropy.MLTable.e0;
            dctx->OFTptr = &ddict->entropy.OFTable.e0;
            dctx->HUFptr = ddict->entropy.hufTable;
            dctx->entropy.rep[0] = ddict->entropy.rep[0];
            dctx->entropy.rep[1] = ddict->entropy.rep[1];
            dctx->entropy.rep[2] = ddict->entropy.rep[2];
        }
        else
        {
            dctx->litEntropy = 0;
            dctx->fseEntropy = 0;
        }
    }

    private static nuint ZSTD_loadEntropy_intoDDict(
        ZSTD_DDict_s* ddict,
        ZSTD_dictContentType_e dictContentType
    )
    {
        ddict->dictID = 0;
        ddict->entropyPresent = 0;
        if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_rawContent)
            return 0;
        if (ddict->dictSize < 8)
        {
            if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_fullDict)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
            return 0;
        }

        {
            uint magic = MEM_readLE32(ddict->dictContent);
            if (magic != 0xEC30A437)
            {
                if (dictContentType == ZSTD_dictContentType_e.ZSTD_dct_fullDict)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
                return 0;
            }
        }

        ddict->dictID = MEM_readLE32((sbyte*)ddict->dictContent + 4);
        if (ERR_isError(ZSTD_loadDEntropy(&ddict->entropy, ddict->dictContent, ddict->dictSize)))
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted));
        }

        ddict->entropyPresent = 1;
        return 0;
    }

    private static nuint ZSTD_initDDict_internal(
        ZSTD_DDict_s* ddict,
        void* dict,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType
    )
    {
        if (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef || dict == null || dictSize == 0)
        {
            ddict->dictBuffer = null;
            ddict->dictContent = dict;
            if (dict == null)
                dictSize = 0;
        }
        else
        {
            void* internalBuffer = ZSTD_customMalloc(dictSize, ddict->cMem);
            ddict->dictBuffer = internalBuffer;
            ddict->dictContent = internalBuffer;
            if (internalBuffer == null)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
            memcpy(internalBuffer, dict, (uint)dictSize);
        }

        ddict->dictSize = dictSize;
        ddict->entropy.hufTable[0] = 12 * 0x1000001;
        {
            /* parse dictionary content */
            nuint err_code = ZSTD_loadEntropy_intoDDict(ddict, dictContentType);
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return 0;
    }

    public static ZSTD_DDict_s* ZSTD_createDDict_advanced(
        void* dict,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType,
        ZSTD_customMem customMem
    )
    {
        if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            return null;
        {
            ZSTD_DDict_s* ddict = (ZSTD_DDict_s*)ZSTD_customMalloc(
                (nuint)sizeof(ZSTD_DDict_s),
                customMem
            );
            if (ddict == null)
                return null;
            ddict->cMem = customMem;
            {
                nuint initResult = ZSTD_initDDict_internal(
                    ddict,
                    dict,
                    dictSize,
                    dictLoadMethod,
                    dictContentType
                );
                if (ERR_isError(initResult))
                {
                    ZSTD_freeDDict(ddict);
                    return null;
                }
            }

            return ddict;
        }
    }

    /*! ZSTD_createDDict() :
     *   Create a digested dictionary, to start decompression without startup delay.
     *   `dict` content is copied inside DDict.
     *   Consequently, `dict` can be released after `ZSTD_DDict` creation */
    public static ZSTD_DDict_s* ZSTD_createDDict(void* dict, nuint dictSize)
    {
        ZSTD_customMem allocator = new ZSTD_customMem
        {
            customAlloc = null,
            customFree = null,
            opaque = null,
        };
        return ZSTD_createDDict_advanced(
            dict,
            dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            allocator
        );
    }

    /*! ZSTD_createDDict_byReference() :
     *  Create a digested dictionary, to start decompression without startup delay.
     *  Dictionary content is simply referenced, it will be accessed during decompression.
     *  Warning : dictBuffer must outlive DDict (DDict must be freed before dictBuffer) */
    public static ZSTD_DDict_s* ZSTD_createDDict_byReference(void* dictBuffer, nuint dictSize)
    {
        ZSTD_customMem allocator = new ZSTD_customMem
        {
            customAlloc = null,
            customFree = null,
            opaque = null,
        };
        return ZSTD_createDDict_advanced(
            dictBuffer,
            dictSize,
            ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
            ZSTD_dictContentType_e.ZSTD_dct_auto,
            allocator
        );
    }

    public static ZSTD_DDict_s* ZSTD_initStaticDDict(
        void* sBuffer,
        nuint sBufferSize,
        void* dict,
        nuint dictSize,
        ZSTD_dictLoadMethod_e dictLoadMethod,
        ZSTD_dictContentType_e dictContentType
    )
    {
        nuint neededSpace =
            (nuint)sizeof(ZSTD_DDict_s)
            + (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef ? 0 : dictSize);
        ZSTD_DDict_s* ddict = (ZSTD_DDict_s*)sBuffer;
        assert(sBuffer != null);
        assert(dict != null);
        if (((nuint)sBuffer & 7) != 0)
            return null;
        if (sBufferSize < neededSpace)
            return null;
        if (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy)
        {
            memcpy(ddict + 1, dict, (uint)dictSize);
            dict = ddict + 1;
        }

        if (
            ERR_isError(
                ZSTD_initDDict_internal(
                    ddict,
                    dict,
                    dictSize,
                    ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef,
                    dictContentType
                )
            )
        )
            return null;
        return ddict;
    }

    /*! ZSTD_freeDDict() :
     *  Function frees memory allocated with ZSTD_createDDict()
     *  If a NULL pointer is passed, no operation is performed. */
    public static nuint ZSTD_freeDDict(ZSTD_DDict_s* ddict)
    {
        if (ddict == null)
            return 0;
        {
            ZSTD_customMem cMem = ddict->cMem;
            ZSTD_customFree(ddict->dictBuffer, cMem);
            ZSTD_customFree(ddict, cMem);
            return 0;
        }
    }

    /*! ZSTD_estimateDDictSize() :
     *  Estimate amount of memory that will be needed to create a dictionary for decompression.
     *  Note : dictionary created by reference using ZSTD_dlm_byRef are smaller */
    public static nuint ZSTD_estimateDDictSize(nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod)
    {
        return (nuint)sizeof(ZSTD_DDict_s)
            + (dictLoadMethod == ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef ? 0 : dictSize);
    }

    public static nuint ZSTD_sizeof_DDict(ZSTD_DDict_s* ddict)
    {
        if (ddict == null)
            return 0;
        return (nuint)sizeof(ZSTD_DDict_s) + (ddict->dictBuffer != null ? ddict->dictSize : 0);
    }

    /*! ZSTD_getDictID_fromDDict() :
     *  Provides the dictID of the dictionary loaded into `ddict`.
     *  If @return == 0, the dictionary is not conformant to Zstandard specification, or empty.
     *  Non-conformant dictionaries can still be loaded, but as content-only dictionaries. */
    public static uint ZSTD_getDictID_fromDDict(ZSTD_DDict_s* ddict)
    {
        if (ddict == null)
            return 0;
        return ddict->dictID;
    }
}
