using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /* Hash function to determine starting position of dict insertion within the table
         * Returns an index between [0, hashSet->ddictPtrTableSize]
         */
        private static nuint ZSTD_DDictHashSet_getIndex(ZSTD_DDictHashSet* hashSet, uint dictID)
        {
            ulong hash = XXH64((void*)&dictID, (nuint)(4), 0);

            return (nuint)(hash & (hashSet->ddictPtrTableSize - 1));
        }

        /* Adds DDict to a hashset without resizing it.
         * If inserting a DDict with a dictID that already exists in the set, replaces the one in the set.
         * Returns 0 if successful, or a zstd error code if something went wrong.
         */
        private static nuint ZSTD_DDictHashSet_emplaceDDict(ZSTD_DDictHashSet* hashSet, ZSTD_DDict_s* ddict)
        {
            uint dictID = ZSTD_getDictID_fromDDict(ddict);
            nuint idx = ZSTD_DDictHashSet_getIndex(hashSet, dictID);
            nuint idxRangeMask = hashSet->ddictPtrTableSize - 1;

            if (hashSet->ddictPtrCount == hashSet->ddictPtrTableSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            while (hashSet->ddictPtrTable[idx] != null)
            {
                if (ZSTD_getDictID_fromDDict(hashSet->ddictPtrTable[idx]) == dictID)
                {
                    hashSet->ddictPtrTable[idx] = ddict;
                    return 0;
                }

                idx &= idxRangeMask;
                idx++;
            }

            hashSet->ddictPtrTable[idx] = ddict;
            hashSet->ddictPtrCount++;
            return 0;
        }

        /* Expands hash table by factor of DDICT_HASHSET_RESIZE_FACTOR and
         * rehashes all values, allocates new table, frees old table.
         * Returns 0 on success, otherwise a zstd error code.
         */
        private static nuint ZSTD_DDictHashSet_expand(ZSTD_DDictHashSet* hashSet, ZSTD_customMem customMem)
        {
            nuint newTableSize = hashSet->ddictPtrTableSize * 2;
            ZSTD_DDict_s** newTable = (ZSTD_DDict_s**)(ZSTD_customCalloc((nuint)(sizeof(ZSTD_DDict_s*)) * newTableSize, customMem));
            ZSTD_DDict_s** oldTable = hashSet->ddictPtrTable;
            nuint oldTableSize = hashSet->ddictPtrTableSize;
            nuint i;

            if (newTable == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }

            hashSet->ddictPtrTable = newTable;
            hashSet->ddictPtrTableSize = newTableSize;
            hashSet->ddictPtrCount = 0;
            for (i = 0; i < oldTableSize; ++i)
            {
                if (oldTable[i] != null)
                {

                    {
                        nuint err_code = (ZSTD_DDictHashSet_emplaceDDict(hashSet, oldTable[i]));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }
            }

            ZSTD_customFree((void*)(oldTable), customMem);
            return 0;
        }

        /* Fetches a DDict with the given dictID
         * Returns the ZSTD_DDict* with the requested dictID. If it doesn't exist, then returns NULL.
         */
        private static ZSTD_DDict_s* ZSTD_DDictHashSet_getDDict(ZSTD_DDictHashSet* hashSet, uint dictID)
        {
            nuint idx = ZSTD_DDictHashSet_getIndex(hashSet, dictID);
            nuint idxRangeMask = hashSet->ddictPtrTableSize - 1;

            for (;;)
            {
                nuint currDictID = ZSTD_getDictID_fromDDict(hashSet->ddictPtrTable[idx]);

                if (currDictID == dictID || currDictID == 0)
                {
                    break;
                }
                else
                {
                    idx &= idxRangeMask;
                    idx++;
                }
            }

            return hashSet->ddictPtrTable[idx];
        }

        /* Allocates space for and returns a ddict hash set
         * The hash set's ZSTD_DDict* table has all values automatically set to NULL to begin with.
         * Returns NULL if allocation failed.
         */
        private static ZSTD_DDictHashSet* ZSTD_createDDictHashSet(ZSTD_customMem customMem)
        {
            ZSTD_DDictHashSet* ret = (ZSTD_DDictHashSet*)(ZSTD_customMalloc((nuint)(sizeof(ZSTD_DDictHashSet)), customMem));

            ret->ddictPtrTable = (ZSTD_DDict_s**)(ZSTD_customCalloc(64 * (nuint)(sizeof(ZSTD_DDict_s*)), customMem));
            ret->ddictPtrTableSize = 64;
            ret->ddictPtrCount = 0;
            if (ret == null || ret->ddictPtrTable == null)
            {
                return (ZSTD_DDictHashSet*)null;
            }

            return ret;
        }

        /* Frees the table of ZSTD_DDict* within a hashset, then frees the hashset itself.
         * Note: The ZSTD_DDict* within the table are NOT freed.
         */
        private static void ZSTD_freeDDictHashSet(ZSTD_DDictHashSet* hashSet, ZSTD_customMem customMem)
        {
            if (hashSet != null && hashSet->ddictPtrTable != null)
            {
                ZSTD_customFree((void*)(hashSet->ddictPtrTable), customMem);
            }

            if (hashSet != null)
            {
                ZSTD_customFree((void*)hashSet, customMem);
            }
        }

        /* Public function: Adds a DDict into the ZSTD_DDictHashSet, possibly triggering a resize of the hash set.
         * Returns 0 on success, or a ZSTD error.
         */
        private static nuint ZSTD_DDictHashSet_addDDict(ZSTD_DDictHashSet* hashSet, ZSTD_DDict_s* ddict, ZSTD_customMem customMem)
        {
            if (hashSet->ddictPtrCount * 4 / hashSet->ddictPtrTableSize * 3 != 0)
            {

                {
                    nuint err_code = (ZSTD_DDictHashSet_expand(hashSet, customMem));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

            }


            {
                nuint err_code = (ZSTD_DDictHashSet_emplaceDDict(hashSet, ddict));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return 0;
        }

        /*-*************************************************************
        *   Context management
        ***************************************************************/
        public static nuint ZSTD_sizeof_DCtx(ZSTD_DCtx_s* dctx)
        {
            if (dctx == null)
            {
                return 0;
            }

            return (nuint)(sizeof(ZSTD_DCtx_s)) + ZSTD_sizeof_DDict(dctx->ddictLocal) + dctx->inBuffSize + dctx->outBuffSize;
        }

        public static nuint ZSTD_estimateDCtxSize()
        {
            return (nuint)(sizeof(ZSTD_DCtx_s));
        }

        private static nuint ZSTD_startingInputLength(ZSTD_format_e format)
        {
            nuint startingInputLength = (nuint)(((format) == ZSTD_format_e.ZSTD_f_zstd1 ? 5 : 1));

            assert((format == ZSTD_format_e.ZSTD_f_zstd1) || (format == ZSTD_format_e.ZSTD_f_zstd1_magicless));
            return startingInputLength;
        }

        private static void ZSTD_DCtx_resetParameters(ZSTD_DCtx_s* dctx)
        {
            assert(dctx->streamStage == ZSTD_dStreamStage.zdss_init);
            dctx->format = ZSTD_format_e.ZSTD_f_zstd1;
            dctx->maxWindowSize = (((uint)(1) << 27) + 1);
            dctx->outBufferMode = ZSTD_bufferMode_e.ZSTD_bm_buffered;
            dctx->forceIgnoreChecksum = ZSTD_forceIgnoreChecksum_e.ZSTD_d_validateChecksum;
            dctx->refMultipleDDicts = ZSTD_refMultipleDDicts_e.ZSTD_rmd_refSingleDDict;
        }

        private static void ZSTD_initDCtx_internal(ZSTD_DCtx_s* dctx)
        {
            dctx->staticSize = 0;
            dctx->ddict = null;
            dctx->ddictLocal = null;
            dctx->dictEnd = null;
            dctx->ddictIsCold = 0;
            dctx->dictUses = ZSTD_dictUses_e.ZSTD_dont_use;
            dctx->inBuff = null;
            dctx->inBuffSize = 0;
            dctx->outBuffSize = 0;
            dctx->streamStage = ZSTD_dStreamStage.zdss_init;
            dctx->legacyContext = null;
            dctx->previousLegacyVersion = 0;
            dctx->noForwardProgress = 0;
            dctx->oversizedDuration = 0;
            dctx->bmi2 = ((IsBmi2Supported) ? 1 : 0);
            dctx->ddictSet = null;
            ZSTD_DCtx_resetParameters(dctx);
        }

        public static ZSTD_DCtx_s* ZSTD_initStaticDCtx(void* workspace, nuint workspaceSize)
        {
            ZSTD_DCtx_s* dctx = (ZSTD_DCtx_s*)(workspace);

            if (((nuint)(workspace) & 7) != 0)
            {
                return (ZSTD_DCtx_s*)null;
            }

            if (workspaceSize < (nuint)(sizeof(ZSTD_DCtx_s)))
            {
                return (ZSTD_DCtx_s*)null;
            }

            ZSTD_initDCtx_internal(dctx);
            dctx->staticSize = workspaceSize;
            dctx->inBuff = (sbyte*)(dctx + 1);
            return dctx;
        }

        public static ZSTD_DCtx_s* ZSTD_createDCtx_advanced(ZSTD_customMem customMem)
        {
            if (((customMem.customAlloc == null ? 1 : 0) ^ (customMem.customFree == null ? 1 : 0)) != 0)
            {
                return (ZSTD_DCtx_s*)null;
            }


            {
                ZSTD_DCtx_s* dctx = (ZSTD_DCtx_s*)(ZSTD_customMalloc((nuint)(sizeof(ZSTD_DCtx_s)), customMem));

                if (dctx == null)
                {
                    return (ZSTD_DCtx_s*)null;
                }

                dctx->customMem = customMem;
                ZSTD_initDCtx_internal(dctx);
                return dctx;
            }
        }

        public static ZSTD_DCtx_s* ZSTD_createDCtx()
        {
            return ZSTD_createDCtx_advanced(ZSTD_defaultCMem);
        }

        private static void ZSTD_clearDict(ZSTD_DCtx_s* dctx)
        {
            ZSTD_freeDDict(dctx->ddictLocal);
            dctx->ddictLocal = null;
            dctx->ddict = null;
            dctx->dictUses = ZSTD_dictUses_e.ZSTD_dont_use;
        }

        public static nuint ZSTD_freeDCtx(ZSTD_DCtx_s* dctx)
        {
            if (dctx == null)
            {
                return 0;
            }

            if (dctx->staticSize != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }


            {
                ZSTD_customMem cMem = dctx->customMem;

                ZSTD_clearDict(dctx);
                ZSTD_customFree((void*)dctx->inBuff, cMem);
                dctx->inBuff = null;
                if (dctx->ddictSet != null)
                {
                    ZSTD_freeDDictHashSet(dctx->ddictSet, cMem);
                    dctx->ddictSet = null;
                }

                ZSTD_customFree((void*)dctx, cMem);
                return 0;
            }
        }

        /* no longer useful */
        public static void ZSTD_copyDCtx(ZSTD_DCtx_s* dstDCtx, ZSTD_DCtx_s* srcDCtx)
        {
            nuint toCopy = (nuint)((sbyte*)(&dstDCtx->inBuff) - (sbyte*)(dstDCtx));

            memcpy((void*)(dstDCtx), (void*)(srcDCtx), (toCopy));
        }

        /* Given a dctx with a digested frame params, re-selects the correct ZSTD_DDict based on
         * the requested dict ID from the frame. If there exists a reference to the correct ZSTD_DDict, then
         * accordingly sets the ddict to be used to decompress the frame.
         *
         * If no DDict is found, then no action is taken, and the ZSTD_DCtx::ddict remains as-is.
         *
         * ZSTD_d_refMultipleDDicts must be enabled for this function to be called.
         */
        private static void ZSTD_DCtx_selectFrameDDict(ZSTD_DCtx_s* dctx)
        {
            assert(dctx->refMultipleDDicts != default && dctx->ddictSet != null);
            if (dctx->ddict != null)
            {
                ZSTD_DDict_s* frameDDict = ZSTD_DDictHashSet_getDDict(dctx->ddictSet, dctx->fParams.dictID);

                if (frameDDict != null)
                {
                    ZSTD_clearDict(dctx);
                    dctx->dictID = dctx->fParams.dictID;
                    dctx->ddict = frameDDict;
                    dctx->dictUses = ZSTD_dictUses_e.ZSTD_use_indefinitely;
                }
            }
        }

        /*! ZSTD_isFrame() :
         *  Tells if the content of `buffer` starts with a valid Frame Identifier.
         *  Note : Frame Identifier is 4 bytes. If `size < 4`, @return will always be 0.
         *  Note 2 : Legacy Frame Identifiers are considered valid only if Legacy Support is enabled.
         *  Note 3 : Skippable Frame Identifiers are considered valid. */
        public static uint ZSTD_isFrame(void* buffer, nuint size)
        {
            if (size < 4)
            {
                return 0;
            }


            {
                uint magic = MEM_readLE32(buffer);

                if (magic == 0xFD2FB528)
                {
                    return 1;
                }

                if ((magic & 0xFFFFFFF0) == 0x184D2A50)
                {
                    return 1;
                }
            }

            return 0;
        }

        /** ZSTD_frameHeaderSize_internal() :
         *  srcSize must be large enough to reach header size fields.
         *  note : only works for formats ZSTD_f_zstd1 and ZSTD_f_zstd1_magicless.
         * @return : size of the Frame Header
         *           or an error code, which can be tested with ZSTD_isError() */
        private static nuint ZSTD_frameHeaderSize_internal(void* src, nuint srcSize, ZSTD_format_e format)
        {
            nuint minInputSize = ZSTD_startingInputLength(format);

            if (srcSize < minInputSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }


            {
                byte fhd = ((byte*)(src))[minInputSize - 1];
                uint dictID = (uint)(fhd & 3);
                uint singleSegment = (uint)((fhd >> 5) & 1);
                uint fcsId = (uint)(fhd >> 6);

                return minInputSize + (uint)(singleSegment == 0 ? 1 : 0) + ZSTD_did_fieldSize[dictID] + ZSTD_fcs_fieldSize[fcsId] + (uint)((((singleSegment != 0 && fcsId == 0)) ? 1 : 0));
            }
        }

        /** ZSTD_frameHeaderSize() :
         *  srcSize must be >= ZSTD_frameHeaderSize_prefix.
         * @return : size of the Frame Header,
         *           or an error code (if srcSize is too small) */
        public static nuint ZSTD_frameHeaderSize(void* src, nuint srcSize)
        {
            return ZSTD_frameHeaderSize_internal(src, srcSize, ZSTD_format_e.ZSTD_f_zstd1);
        }

        /** ZSTD_getFrameHeader_advanced() :
         *  decode Frame Header, or require larger `srcSize`.
         *  note : only works for formats ZSTD_f_zstd1 and ZSTD_f_zstd1_magicless
         * @return : 0, `zfhPtr` is correctly filled,
         *          >0, `srcSize` is too small, value is wanted `srcSize` amount,
         *           or an error code, which can be tested using ZSTD_isError() */
        public static nuint ZSTD_getFrameHeader_advanced(ZSTD_frameHeader* zfhPtr, void* src, nuint srcSize, ZSTD_format_e format)
        {
            byte* ip = (byte*)(src);
            nuint minInputSize = ZSTD_startingInputLength(format);

            memset((void*)(zfhPtr), (0), ((nuint)(sizeof(ZSTD_frameHeader))));
            if (srcSize < minInputSize)
            {
                return minInputSize;
            }

            if (src == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if ((format != ZSTD_format_e.ZSTD_f_zstd1_magicless) && (MEM_readLE32(src) != 0xFD2FB528))
            {
                if ((MEM_readLE32(src) & 0xFFFFFFF0) == 0x184D2A50)
                {
                    if (srcSize < 8)
                    {
                        return 8;
                    }

                    memset((void*)(zfhPtr), (0), ((nuint)(sizeof(ZSTD_frameHeader))));
                    zfhPtr->frameContentSize = MEM_readLE32((void*)((sbyte*)(src) + 4));
                    zfhPtr->frameType = ZSTD_frameType_e.ZSTD_skippableFrame;
                    return 0;
                }


                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_prefix_unknown)));
                }

            }


            {
                nuint fhsize = ZSTD_frameHeaderSize_internal(src, srcSize, format);

                if (srcSize < fhsize)
                {
                    return fhsize;
                }

                zfhPtr->headerSize = (uint)(fhsize);
            }


            {
                byte fhdByte = ip[minInputSize - 1];
                nuint pos = minInputSize;
                uint dictIDSizeCode = (uint)(fhdByte & 3);
                uint checksumFlag = (uint)((fhdByte >> 2) & 1);
                uint singleSegment = (uint)((fhdByte >> 5) & 1);
                uint fcsID = (uint)(fhdByte >> 6);
                ulong windowSize = 0;
                uint dictID = 0;
                ulong frameContentSize = (unchecked(0UL - 1));

                if ((fhdByte & 0x08) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_unsupported)));
                }

                if (singleSegment == 0)
                {
                    byte wlByte = ip[pos++];
                    uint windowLog = (uint)((wlByte >> 3) + 10);

                    if (windowLog > (uint)(((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31))))
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_windowTooLarge)));
                    }

                    windowSize = (1UL << (int)windowLog);
                    windowSize += (windowSize >> 3) * (uint)((wlByte & 7));
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
                        dictID = ip[pos];
                    }

                    pos++;
                    break;
                    case 2:
                    {
                        dictID = MEM_readLE16((void*)(ip + pos));
                    }

                    pos += 2;
                    break;
                    case 3:
                    {
                        dictID = MEM_readLE32((void*)(ip + pos));
                    }

                    pos += 4;
                    break;
                }

                switch (fcsID)
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
                            frameContentSize = ip[pos];
                        }
                    }

                    break;
                    case 1:
                    {
                        frameContentSize = (ulong)(MEM_readLE16((void*)(ip + pos)) + 256);
                    }

                    break;
                    case 2:
                    {
                        frameContentSize = MEM_readLE32((void*)(ip + pos));
                    }

                    break;
                    case 3:
                    {
                        frameContentSize = MEM_readLE64((void*)(ip + pos));
                    }

                    break;
                }

                if (singleSegment != 0)
                {
                    windowSize = unchecked(frameContentSize);
                }

                zfhPtr->frameType = ZSTD_frameType_e.ZSTD_frame;
                zfhPtr->frameContentSize = unchecked(frameContentSize);
                zfhPtr->windowSize = windowSize;
                zfhPtr->blockSizeMax = (uint)((windowSize) < (uint)(((1 << 17))) ? (windowSize) : ((1 << 17)));
                zfhPtr->dictID = dictID;
                zfhPtr->checksumFlag = checksumFlag;
            }

            return 0;
        }

        /** ZSTD_getFrameHeader() :
         *  decode Frame Header, or require larger `srcSize`.
         *  note : this function does not consume input, it only reads it.
         * @return : 0, `zfhPtr` is correctly filled,
         *          >0, `srcSize` is too small, value is wanted `srcSize` amount,
         *           or an error code, which can be tested using ZSTD_isError() */
        public static nuint ZSTD_getFrameHeader(ZSTD_frameHeader* zfhPtr, void* src, nuint srcSize)
        {
            return ZSTD_getFrameHeader_advanced(zfhPtr, src, srcSize, ZSTD_format_e.ZSTD_f_zstd1);
        }

        /** ZSTD_getFrameContentSize() :
         *  compatible with legacy mode
         * @return : decompressed size of the single frame pointed to be `src` if known, otherwise
         *         - ZSTD_CONTENTSIZE_UNKNOWN if the size cannot be determined
         *         - ZSTD_CONTENTSIZE_ERROR if an error occurred (e.g. invalid magic number, srcSize too small) */
        public static ulong ZSTD_getFrameContentSize(void* src, nuint srcSize)
        {

            {
                ZSTD_frameHeader zfh;

                if (ZSTD_getFrameHeader(&zfh, src, srcSize) != 0)
                {
                    return (unchecked(0UL - 2));
                }

                if (zfh.frameType == ZSTD_frameType_e.ZSTD_skippableFrame)
                {
                    return 0;
                }
                else
                {
                    return zfh.frameContentSize;
                }
            }
        }

        private static nuint readSkippableFrameSize(void* src, nuint srcSize)
        {
            nuint skippableHeaderSize = 8;
            uint sizeU32;

            if (srcSize < 8)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            sizeU32 = MEM_readLE32((void*)((byte*)(src) + 4));
            if ((uint)(sizeU32 + 8) < sizeU32)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_unsupported)));
            }


            {
                nuint skippableSize = skippableHeaderSize + sizeU32;

                if (skippableSize > srcSize)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                }

                return skippableSize;
            }
        }

        /** ZSTD_findDecompressedSize() :
         *  compatible with legacy mode
         *  `srcSize` must be the exact length of some number of ZSTD compressed and/or
         *      skippable frames
         *  @return : decompressed size of the frames contained */
        public static ulong ZSTD_findDecompressedSize(void* src, nuint srcSize)
        {
            ulong totalDstSize = 0;

            while (srcSize >= ZSTD_startingInputLength(ZSTD_format_e.ZSTD_f_zstd1))
            {
                uint magicNumber = MEM_readLE32(src);

                if ((magicNumber & 0xFFFFFFF0) == 0x184D2A50)
                {
                    nuint skippableSize = readSkippableFrameSize(src, srcSize);

                    if ((ERR_isError(skippableSize)) != 0)
                    {
                        return (unchecked(0UL - 2));
                    }

                    assert(skippableSize <= srcSize);
                    src = (byte*)(src) + skippableSize;
                    srcSize -= skippableSize;
                    continue;
                }


                {
                    ulong ret = ZSTD_getFrameContentSize(src, srcSize);

                    if (ret >= (unchecked(0UL - 2)))
                    {
                        return ret;
                    }

                    if (totalDstSize + ret < totalDstSize)
                    {
                        return (unchecked(0UL - 2));
                    }

                    totalDstSize += ret;
                }


                {
                    nuint frameSrcSize = ZSTD_findFrameCompressedSize(src, srcSize);

                    if ((ERR_isError(frameSrcSize)) != 0)
                    {
                        return (unchecked(0UL - 2));
                    }

                    src = (byte*)(src) + frameSrcSize;
                    srcSize -= frameSrcSize;
                }
            }

            if (srcSize != 0)
            {
                return (unchecked(0UL - 2));
            }

            return totalDstSize;
        }

        /** ZSTD_getDecompressedSize() :
         *  compatible with legacy mode
         * @return : decompressed size if known, 0 otherwise
                     note : 0 can mean any of the following :
                           - frame content is empty
                           - decompressed size field is not present in frame header
                           - frame header unknown / not supported
                           - frame header not complete (`srcSize` too small) */
        public static ulong ZSTD_getDecompressedSize(void* src, nuint srcSize)
        {
            ulong ret = ZSTD_getFrameContentSize(src, srcSize);

            return (ret >= (unchecked(0UL - 2))) ? 0 : ret;
        }

        /** ZSTD_decodeFrameHeader() :
         * `headerSize` must be the size provided by ZSTD_frameHeaderSize().
         * If multiple DDict references are enabled, also will choose the correct DDict to use.
         * @return : 0 if success, or an error code, which can be tested using ZSTD_isError() */
        private static nuint ZSTD_decodeFrameHeader(ZSTD_DCtx_s* dctx, void* src, nuint headerSize)
        {
            nuint result = ZSTD_getFrameHeader_advanced(&(dctx->fParams), src, headerSize, dctx->format);

            if ((ERR_isError(result)) != 0)
            {
                return result;
            }

            if (result > 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if (dctx->refMultipleDDicts == ZSTD_refMultipleDDicts_e.ZSTD_rmd_refMultipleDDicts && dctx->ddictSet != null)
            {
                ZSTD_DCtx_selectFrameDDict(dctx);
            }

            if (dctx->fParams.dictID != 0 && (dctx->dictID != dctx->fParams.dictID))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_wrong)));
            }

            dctx->validateChecksum = (uint)((dctx->fParams.checksumFlag != 0 && dctx->forceIgnoreChecksum == default) ? 1 : 0);
            if (dctx->validateChecksum != 0)
            {
                XXH64_reset(&dctx->xxhState, 0);
            }

            dctx->processedCSize += (ulong)headerSize;
            return 0;
        }

        private static ZSTD_frameSizeInfo ZSTD_errorFrameSizeInfo(nuint ret)
        {
            ZSTD_frameSizeInfo frameSizeInfo;

            frameSizeInfo.compressedSize = ret;
            frameSizeInfo.decompressedBound = (unchecked(0UL - 2));
            return frameSizeInfo;
        }

        private static ZSTD_frameSizeInfo ZSTD_findFrameSizeInfo(void* src, nuint srcSize)
        {
            ZSTD_frameSizeInfo frameSizeInfo;

            memset((void*)(&frameSizeInfo), (0), ((nuint)(sizeof(ZSTD_frameSizeInfo))));
            if ((srcSize >= 8) && (MEM_readLE32(src) & 0xFFFFFFF0) == 0x184D2A50)
            {
                frameSizeInfo.compressedSize = readSkippableFrameSize(src, srcSize);
                assert((ERR_isError(frameSizeInfo.compressedSize)) != 0 || frameSizeInfo.compressedSize <= srcSize);
                return frameSizeInfo;
            }
            else
            {
                byte* ip = (byte*)(src);
                byte* ipstart = ip;
                nuint remainingSize = srcSize;
                nuint nbBlocks = 0;
                ZSTD_frameHeader zfh;


                {
                    nuint ret = ZSTD_getFrameHeader(&zfh, src, srcSize);

                    if ((ERR_isError(ret)) != 0)
                    {
                        return ZSTD_errorFrameSizeInfo(ret);
                    }

                    if (ret > 0)
                    {
                        return ZSTD_errorFrameSizeInfo((unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong))));
                    }
                }

                ip += zfh.headerSize;
                remainingSize -= zfh.headerSize;
                while (1 != 0)
                {
                    blockProperties_t blockProperties;
                    nuint cBlockSize = ZSTD_getcBlockSize((void*)ip, remainingSize, &blockProperties);

                    if ((ERR_isError(cBlockSize)) != 0)
                    {
                        return ZSTD_errorFrameSizeInfo(cBlockSize);
                    }

                    if (ZSTD_blockHeaderSize + cBlockSize > remainingSize)
                    {
                        return ZSTD_errorFrameSizeInfo((unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong))));
                    }

                    ip += ZSTD_blockHeaderSize + cBlockSize;
                    remainingSize -= ZSTD_blockHeaderSize + cBlockSize;
                    nbBlocks++;
                    if (blockProperties.lastBlock != 0)
                    {
                        break;
                    }
                }

                if (zfh.checksumFlag != 0)
                {
                    if (remainingSize < 4)
                    {
                        return ZSTD_errorFrameSizeInfo((unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong))));
                    }

                    ip += 4;
                }

                frameSizeInfo.compressedSize = (nuint)(ip - ipstart);
                frameSizeInfo.decompressedBound = (zfh.frameContentSize != (unchecked(0UL - 1))) ? zfh.frameContentSize : nbBlocks * zfh.blockSizeMax;
                return frameSizeInfo;
            }
        }

        /** ZSTD_findFrameCompressedSize() :
         *  compatible with legacy mode
         *  `src` must point to the start of a ZSTD frame, ZSTD legacy frame, or skippable frame
         *  `srcSize` must be at least as large as the frame contained
         *  @return : the compressed size of the frame starting at `src` */
        public static nuint ZSTD_findFrameCompressedSize(void* src, nuint srcSize)
        {
            ZSTD_frameSizeInfo frameSizeInfo = ZSTD_findFrameSizeInfo(src, srcSize);

            return frameSizeInfo.compressedSize;
        }

        /** ZSTD_decompressBound() :
         *  compatible with legacy mode
         *  `src` must point to the start of a ZSTD frame or a skippeable frame
         *  `srcSize` must be at least as large as the frame contained
         *  @return : the maximum decompressed size of the compressed source
         */
        public static ulong ZSTD_decompressBound(void* src, nuint srcSize)
        {
            ulong bound = 0;

            while (srcSize > 0)
            {
                ZSTD_frameSizeInfo frameSizeInfo = ZSTD_findFrameSizeInfo(src, srcSize);
                nuint compressedSize = frameSizeInfo.compressedSize;
                ulong decompressedBound = frameSizeInfo.decompressedBound;

                if ((ERR_isError(compressedSize)) != 0 || decompressedBound == (unchecked(0UL - 2)))
                {
                    return (unchecked(0UL - 2));
                }

                assert(srcSize >= compressedSize);
                src = (byte*)(src) + compressedSize;
                srcSize -= compressedSize;
                bound += decompressedBound;
            }

            return bound;
        }

        /** ZSTD_insertBlock() :
         *  insert `src` block into `dctx` history. Useful to track uncompressed blocks. */
        public static nuint ZSTD_insertBlock(ZSTD_DCtx_s* dctx, void* blockStart, nuint blockSize)
        {
            ZSTD_checkContinuity(dctx, blockStart, blockSize);
            dctx->previousDstEnd = (sbyte*)(blockStart) + blockSize;
            return blockSize;
        }

        private static nuint ZSTD_copyRawBlock(void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            if (srcSize > dstCapacity)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (dst == null)
            {
                if (srcSize == 0)
                {
                    return 0;
                }


                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstBuffer_null)));
                }

            }

            memcpy((dst), (src), (srcSize));
            return srcSize;
        }

        private static nuint ZSTD_setRleBlock(void* dst, nuint dstCapacity, byte b, nuint regenSize)
        {
            if (regenSize > dstCapacity)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (dst == null)
            {
                if (regenSize == 0)
                {
                    return 0;
                }


                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstBuffer_null)));
                }

            }

            memset((dst), (int)(b), (regenSize));
            return regenSize;
        }

        private static void ZSTD_DCtx_trace_end(ZSTD_DCtx_s* dctx, ulong uncompressedSize, ulong compressedSize, uint streaming)
        {
        }

        /*! ZSTD_decompressFrame() :
         * @dctx must be properly initialized
         *  will update *srcPtr and *srcSizePtr,
         *  to make *srcPtr progress by one frame. */
        private static nuint ZSTD_decompressFrame(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void** srcPtr, nuint* srcSizePtr)
        {
            byte* istart = (byte*)(*srcPtr);
            byte* ip = istart;
            byte* ostart = (byte*)(dst);
            byte* oend = dstCapacity != 0 ? ostart + dstCapacity : ostart;
            byte* op = ostart;
            nuint remainingSrcSize = *srcSizePtr;

            if (remainingSrcSize < (uint)(((dctx->format) == ZSTD_format_e.ZSTD_f_zstd1 ? 6 : 2)) + ZSTD_blockHeaderSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }


            {
                nuint frameHeaderSize = ZSTD_frameHeaderSize_internal((void*)ip, (nuint)(((dctx->format) == ZSTD_format_e.ZSTD_f_zstd1 ? 5 : 1)), dctx->format);

                if ((ERR_isError(frameHeaderSize)) != 0)
                {
                    return frameHeaderSize;
                }

                if (remainingSrcSize < frameHeaderSize + ZSTD_blockHeaderSize)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                }


                {
                    nuint err_code = (ZSTD_decodeFrameHeader(dctx, (void*)ip, frameHeaderSize));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                ip += frameHeaderSize;
                remainingSrcSize -= frameHeaderSize;
            }

            while (1 != 0)
            {
                nuint decodedSize;
                blockProperties_t blockProperties;
                nuint cBlockSize = ZSTD_getcBlockSize((void*)ip, remainingSrcSize, &blockProperties);

                if ((ERR_isError(cBlockSize)) != 0)
                {
                    return cBlockSize;
                }

                ip += ZSTD_blockHeaderSize;
                remainingSrcSize -= ZSTD_blockHeaderSize;
                if (cBlockSize > remainingSrcSize)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                }

                switch (blockProperties.blockType)
                {
                    case blockType_e.bt_compressed:
                    {
                        decodedSize = ZSTD_decompressBlock_internal(dctx, (void*)op, (nuint)(oend - op), (void*)ip, cBlockSize, 1);
                    }

                    break;
                    case blockType_e.bt_raw:
                    {
                        decodedSize = ZSTD_copyRawBlock((void*)op, (nuint)(oend - op), (void*)ip, cBlockSize);
                    }

                    break;
                    case blockType_e.bt_rle:
                    {
                        decodedSize = ZSTD_setRleBlock((void*)op, (nuint)(oend - op), *ip, blockProperties.origSize);
                    }

                    break;
                    case blockType_e.bt_reserved:
                    default:
                    {

                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }
                    }

                }

                if ((ERR_isError(decodedSize)) != 0)
                {
                    return decodedSize;
                }

                if (dctx->validateChecksum != 0)
                {
                    XXH64_update(&dctx->xxhState, (void*)op, decodedSize);
                }

                if (decodedSize != 0)
                {
                    op += decodedSize;
                }

                assert(ip != null);
                ip += cBlockSize;
                remainingSrcSize -= cBlockSize;
                if (blockProperties.lastBlock != 0)
                {
                    break;
                }
            }

            if (dctx->fParams.frameContentSize != (unchecked(0UL - 1)))
            {
                if ((ulong)(op - ostart) != dctx->fParams.frameContentSize)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

            }

            if (dctx->fParams.checksumFlag != 0)
            {
                if (remainingSrcSize < 4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_checksum_wrong)));
                }

                if (dctx->forceIgnoreChecksum == default)
                {
                    uint checkCalc = (uint)(XXH64_digest(&dctx->xxhState));
                    uint checkRead;

                    checkRead = MEM_readLE32((void*)ip);
                    if (checkRead != checkCalc)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_checksum_wrong)));
                    }

                }

                ip += 4;
                remainingSrcSize -= 4;
            }

            ZSTD_DCtx_trace_end(dctx, (ulong)(op - ostart), (ulong)(ip - istart), 0);
            *srcPtr = ip;
            *srcSizePtr = remainingSrcSize;
            return (nuint)(op - ostart);
        }

        private static nuint ZSTD_decompressMultiFrame(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize, ZSTD_DDict_s* ddict)
        {
            void* dststart = dst;
            int moreThan1Frame = 0;

            assert(dict == null || ddict == null);
            if (ddict != null)
            {
                dict = ZSTD_DDict_dictContent(ddict);
                dictSize = ZSTD_DDict_dictSize(ddict);
            }

            while (srcSize >= ZSTD_startingInputLength(dctx->format))
            {

                {
                    uint magicNumber = MEM_readLE32(src);

                    if ((magicNumber & 0xFFFFFFF0) == 0x184D2A50)
                    {
                        nuint skippableSize = readSkippableFrameSize(src, srcSize);


                        {
                            nuint err_code = (skippableSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        assert(skippableSize <= srcSize);
                        src = (byte*)(src) + skippableSize;
                        srcSize -= skippableSize;
                        continue;
                    }
                }

                if (ddict != null)
                {

                    {
                        nuint err_code = (ZSTD_decompressBegin_usingDDict(dctx, ddict));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }
                else
                {

                    {
                        nuint err_code = (ZSTD_decompressBegin_usingDict(dctx, dict, dictSize));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }

                ZSTD_checkContinuity(dctx, dst, dstCapacity);

                {
                    nuint res = ZSTD_decompressFrame(dctx, dst, dstCapacity, &src, &srcSize);

                    if ((ZSTD_getErrorCode(res) == ZSTD_ErrorCode.ZSTD_error_prefix_unknown) && (moreThan1Frame == 1))
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                    }

                    if ((ERR_isError(res)) != 0)
                    {
                        return res;
                    }

                    assert(res <= dstCapacity);
                    if (res != 0)
                    {
                        dst = (byte*)(dst) + res;
                    }

                    dstCapacity -= res;
                }

                moreThan1Frame = 1;
            }

            if (srcSize != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            return (nuint)((byte*)(dst) - (byte*)(dststart));
        }

        /*! ZSTD_decompress_usingDict() :
         *  Decompression using a known Dictionary.
         *  Dictionary must be identical to the one used during compression.
         *  Note : This function loads the dictionary, resulting in significant startup delay.
         *         It's intended for a dictionary used only once.
         *  Note : When `dict == NULL || dictSize < 8` no dictionary is used. */
        public static nuint ZSTD_decompress_usingDict(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* dict, nuint dictSize)
        {
            return ZSTD_decompressMultiFrame(dctx, dst, dstCapacity, src, srcSize, dict, dictSize, (ZSTD_DDict_s*)null);
        }

        private static ZSTD_DDict_s* ZSTD_getDDict(ZSTD_DCtx_s* dctx)
        {
            switch (dctx->dictUses)
            {
                default:
                {
                    assert(0 != 0);
                }


                goto case ZSTD_dictUses_e.ZSTD_dont_use;
                case ZSTD_dictUses_e.ZSTD_dont_use:
                {
                    ZSTD_clearDict(dctx);
                }

                return (ZSTD_DDict_s*)null;
                case ZSTD_dictUses_e.ZSTD_use_indefinitely:
                {
                    return dctx->ddict;
                }

                case ZSTD_dictUses_e.ZSTD_use_once:
                {
                    dctx->dictUses = ZSTD_dictUses_e.ZSTD_dont_use;
                }

                return dctx->ddict;
            }
        }

        /*! ZSTD_decompressDCtx() :
         *  Same as ZSTD_decompress(),
         *  requires an allocated ZSTD_DCtx.
         *  Compatible with sticky parameters.
         */
        public static nuint ZSTD_decompressDCtx(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            return ZSTD_decompress_usingDDict(dctx, dst, dstCapacity, src, srcSize, ZSTD_getDDict(dctx));
        }

        /*! ZSTD_decompress() :
         *  `compressedSize` : must be the _exact_ size of some number of compressed and/or skippable frames.
         *  `dstCapacity` is an upper bound of originalSize to regenerate.
         *  If user cannot imply a maximum upper bound, it's better to use streaming mode to decompress data.
         *  @return : the number of bytes decompressed into `dst` (<= `dstCapacity`),
         *            or an errorCode if it fails (which can be tested using ZSTD_isError()). */
        public static nuint ZSTD_decompress(void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            nuint regenSize;
            ZSTD_DCtx_s* dctx = ZSTD_createDCtx();

            if (dctx == null)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
            }

            regenSize = ZSTD_decompressDCtx(dctx, dst, dstCapacity, src, srcSize);
            ZSTD_freeDCtx(dctx);
            return regenSize;
        }

        /*-**************************************
        *   Advanced Streaming Decompression API
        *   Bufferless and synchronous
        ****************************************/
        public static nuint ZSTD_nextSrcSizeToDecompress(ZSTD_DCtx_s* dctx)
        {
            return dctx->expected;
        }

        /**
         * Similar to ZSTD_nextSrcSizeToDecompress(), but when when a block input can be streamed,
         * we allow taking a partial block as the input. Currently only raw uncompressed blocks can
         * be streamed.
         *
         * For blocks that can be streamed, this allows us to reduce the latency until we produce
         * output, and avoid copying the input.
         *
         * @param inputSize - The total amount of input that the caller currently has.
         */
        private static nuint ZSTD_nextSrcSizeToDecompressWithInputSize(ZSTD_DCtx_s* dctx, nuint inputSize)
        {
            if (!(dctx->stage == ZSTD_dStage.ZSTDds_decompressBlock || dctx->stage == ZSTD_dStage.ZSTDds_decompressLastBlock))
            {
                return dctx->expected;
            }

            if (dctx->bType != blockType_e.bt_raw)
            {
                return dctx->expected;
            }

            return ((((inputSize) > (1) ? (inputSize) : (1))) < (dctx->expected) ? (((inputSize) > (1) ? (inputSize) : (1))) : (dctx->expected));
        }

        public static ZSTD_nextInputType_e ZSTD_nextInputType(ZSTD_DCtx_s* dctx)
        {
            switch (dctx->stage)
            {
                default:
                {
                    assert(0 != 0);
                }


                goto case ZSTD_dStage.ZSTDds_getFrameHeaderSize;
                case ZSTD_dStage.ZSTDds_getFrameHeaderSize:
                case ZSTD_dStage.ZSTDds_decodeFrameHeader:
                {
                    return ZSTD_nextInputType_e.ZSTDnit_frameHeader;
                }

                case ZSTD_dStage.ZSTDds_decodeBlockHeader:
                {
                    return ZSTD_nextInputType_e.ZSTDnit_blockHeader;
                }

                case ZSTD_dStage.ZSTDds_decompressBlock:
                {
                    return ZSTD_nextInputType_e.ZSTDnit_block;
                }

                case ZSTD_dStage.ZSTDds_decompressLastBlock:
                {
                    return ZSTD_nextInputType_e.ZSTDnit_lastBlock;
                }

                case ZSTD_dStage.ZSTDds_checkChecksum:
                {
                    return ZSTD_nextInputType_e.ZSTDnit_checksum;
                }

                case ZSTD_dStage.ZSTDds_decodeSkippableHeader:
                case ZSTD_dStage.ZSTDds_skipFrame:
                {
                    return ZSTD_nextInputType_e.ZSTDnit_skippableFrame;
                }
            }
        }

        private static int ZSTD_isSkipFrame(ZSTD_DCtx_s* dctx)
        {
            return ((dctx->stage == ZSTD_dStage.ZSTDds_skipFrame) ? 1 : 0);
        }

        /** ZSTD_decompressContinue() :
         *  srcSize : must be the exact nb of bytes expected (see ZSTD_nextSrcSizeToDecompress())
         *  @return : nb of bytes generated into `dst` (necessarily <= `dstCapacity)
         *            or an error code, which can be tested using ZSTD_isError() */
        public static nuint ZSTD_decompressContinue(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            if (srcSize != ZSTD_nextSrcSizeToDecompressWithInputSize(dctx, srcSize))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            ZSTD_checkContinuity(dctx, dst, dstCapacity);
            dctx->processedCSize += (ulong)srcSize;
            switch (dctx->stage)
            {
                case ZSTD_dStage.ZSTDds_getFrameHeaderSize:
                {
                    assert(src != null);
                }

                if (dctx->format == ZSTD_format_e.ZSTD_f_zstd1)
                {
                    assert(srcSize >= 4);
                    if ((MEM_readLE32(src) & 0xFFFFFFF0) == 0x184D2A50)
                    {
                        memcpy((void*)(dctx->headerBuffer), (src), (srcSize));
                        dctx->expected = 8 - srcSize;
                        dctx->stage = ZSTD_dStage.ZSTDds_decodeSkippableHeader;
                        return 0;
                    }
                }

                dctx->headerSize = ZSTD_frameHeaderSize_internal(src, srcSize, dctx->format);
                if ((ERR_isError(dctx->headerSize)) != 0)
                {
                    return dctx->headerSize;
                }

                memcpy((void*)(dctx->headerBuffer), (src), (srcSize));
                dctx->expected = dctx->headerSize - srcSize;
                dctx->stage = ZSTD_dStage.ZSTDds_decodeFrameHeader;
                return 0;
                case ZSTD_dStage.ZSTDds_decodeFrameHeader:
                {
                    assert(src != null);
                }

                memcpy((void*)((dctx->headerBuffer + (dctx->headerSize - srcSize))), (src), (srcSize));

                {
                    nuint err_code = (ZSTD_decodeFrameHeader(dctx, (void*)dctx->headerBuffer, dctx->headerSize));

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                dctx->expected = ZSTD_blockHeaderSize;
                dctx->stage = ZSTD_dStage.ZSTDds_decodeBlockHeader;
                return 0;
                case ZSTD_dStage.ZSTDds_decodeBlockHeader:
                {
                    blockProperties_t bp;
                    nuint cBlockSize = ZSTD_getcBlockSize(src, ZSTD_blockHeaderSize, &bp);

                    if ((ERR_isError(cBlockSize)) != 0)
                    {
                        return cBlockSize;
                    }

                    if (cBlockSize > dctx->fParams.blockSizeMax)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    dctx->expected = cBlockSize;
                    dctx->bType = bp.blockType;
                    dctx->rleSize = bp.origSize;
                    if (cBlockSize != 0)
                    {
                        dctx->stage = bp.lastBlock != 0 ? ZSTD_dStage.ZSTDds_decompressLastBlock : ZSTD_dStage.ZSTDds_decompressBlock;
                        return 0;
                    }

                    if (bp.lastBlock != 0)
                    {
                        if (dctx->fParams.checksumFlag != 0)
                        {
                            dctx->expected = 4;
                            dctx->stage = ZSTD_dStage.ZSTDds_checkChecksum;
                        }
                        else
                        {
                            dctx->expected = 0;
                            dctx->stage = ZSTD_dStage.ZSTDds_getFrameHeaderSize;
                        }
                    }
                    else
                    {
                        dctx->expected = ZSTD_blockHeaderSize;
                        dctx->stage = ZSTD_dStage.ZSTDds_decodeBlockHeader;
                    }

                    return 0;
                }

                case ZSTD_dStage.ZSTDds_decompressLastBlock:
                case ZSTD_dStage.ZSTDds_decompressBlock:
        ;

                {
                    nuint rSize;

                    switch (dctx->bType)
                    {
                        case blockType_e.bt_compressed:
        ;
                        rSize = ZSTD_decompressBlock_internal(dctx, dst, dstCapacity, src, srcSize, 1);
                        dctx->expected = 0;
                        break;
                        case blockType_e.bt_raw:
                        {
                            assert(srcSize <= dctx->expected);
                        }

                        rSize = ZSTD_copyRawBlock(dst, dstCapacity, src, srcSize);

                        {
                            nuint err_code = (rSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        assert(rSize == srcSize);
                        dctx->expected -= rSize;
                        break;
                        case blockType_e.bt_rle:
                        {
                            rSize = ZSTD_setRleBlock(dst, dstCapacity, *(byte*)(src), dctx->rleSize);
                        }

                        dctx->expected = 0;
                        break;
                        case blockType_e.bt_reserved:
                        default:
                        {

                            {
                                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                            }
                        }

                    }


                    {
                        nuint err_code = (rSize);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    if (rSize > dctx->fParams.blockSizeMax)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    dctx->decodedSize += (ulong)rSize;
                    if (dctx->validateChecksum != 0)
                    {
                        XXH64_update(&dctx->xxhState, dst, rSize);
                    }

                    dctx->previousDstEnd = (sbyte*)(dst) + rSize;
                    if (dctx->expected > 0)
                    {
                        return rSize;
                    }

                    if (dctx->stage == ZSTD_dStage.ZSTDds_decompressLastBlock)
                    {
                        if (dctx->fParams.frameContentSize != (unchecked(0UL - 1)) && dctx->decodedSize != dctx->fParams.frameContentSize)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }

                        if (dctx->fParams.checksumFlag != 0)
                        {
                            dctx->expected = 4;
                            dctx->stage = ZSTD_dStage.ZSTDds_checkChecksum;
                        }
                        else
                        {
                            ZSTD_DCtx_trace_end(dctx, dctx->decodedSize, dctx->processedCSize, 1);
                            dctx->expected = 0;
                            dctx->stage = ZSTD_dStage.ZSTDds_getFrameHeaderSize;
                        }
                    }
                    else
                    {
                        dctx->stage = ZSTD_dStage.ZSTDds_decodeBlockHeader;
                        dctx->expected = ZSTD_blockHeaderSize;
                    }

                    return rSize;
                }

                case ZSTD_dStage.ZSTDds_checkChecksum:
                {
                    assert(srcSize == 4);
                }


                {
                    if (dctx->validateChecksum != 0)
                    {
                        uint h32 = (uint)(XXH64_digest(&dctx->xxhState));
                        uint check32 = MEM_readLE32(src);

                        if (check32 != h32)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_checksum_wrong)));
                        }

                    }

                    ZSTD_DCtx_trace_end(dctx, dctx->decodedSize, dctx->processedCSize, 1);
                    dctx->expected = 0;
                    dctx->stage = ZSTD_dStage.ZSTDds_getFrameHeaderSize;
                    return 0;
                }

                case ZSTD_dStage.ZSTDds_decodeSkippableHeader:
                {
                    assert(src != null);
                }

                assert(srcSize <= 8);
                memcpy((void*)((dctx->headerBuffer + (8 - srcSize))), (src), (srcSize));
                dctx->expected = MEM_readLE32((void*)(dctx->headerBuffer + 4));
                dctx->stage = ZSTD_dStage.ZSTDds_skipFrame;
                return 0;
                case ZSTD_dStage.ZSTDds_skipFrame:
                {
                    dctx->expected = 0;
                }

                dctx->stage = ZSTD_dStage.ZSTDds_getFrameHeaderSize;
                return 0;
                default:
                {
                    assert(0 != 0);
                }


                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                }

            }
        }

        private static nuint ZSTD_refDictContent(ZSTD_DCtx_s* dctx, void* dict, nuint dictSize)
        {
            dctx->dictEnd = dctx->previousDstEnd;
            dctx->virtualStart = (sbyte*)(dict) - ((sbyte*)(dctx->previousDstEnd) - (sbyte*)(dctx->prefixStart));
            dctx->prefixStart = dict;
            dctx->previousDstEnd = (sbyte*)(dict) + dictSize;
            return 0;
        }

        /*! ZSTD_loadDEntropy() :
         *  dict : must point at beginning of a valid zstd dictionary.
         * @return : size of entropy tables read */
        public static nuint ZSTD_loadDEntropy(ZSTD_entropyDTables_t* entropy, void* dict, nuint dictSize)
        {
            byte* dictPtr = (byte*)(dict);
            byte* dictEnd = dictPtr + dictSize;

            if (dictSize <= 8)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
            }

            assert(MEM_readLE32(dict) == 0xEC30A437);
            dictPtr += 8;

            {
                void* workspace = (void*)&entropy->LLTable;
                nuint workspaceSize = (nuint)(4104) + (nuint)(2056) + (nuint)(4104);
                nuint hSize = HUF_readDTableX2_wksp((uint*)entropy->hufTable, (void*)dictPtr, (nuint)(dictEnd - dictPtr), workspace, workspaceSize);

                if ((ERR_isError(hSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                dictPtr += hSize;
            }


            {
                short* offcodeNCount = stackalloc short[32];
                uint offcodeMaxValue = 31, offcodeLog;
                nuint offcodeHeaderSize = FSE_readNCount((short*)offcodeNCount, &offcodeMaxValue, &offcodeLog, (void*)dictPtr, (nuint)(dictEnd - dictPtr));

                if ((ERR_isError(offcodeHeaderSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (offcodeMaxValue > 31)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (offcodeLog > 8)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                ZSTD_buildFSETable((ZSTD_seqSymbol*)entropy->OFTable, (short*)offcodeNCount, offcodeMaxValue, (uint*)OF_base, (uint*)OF_bits, offcodeLog, (void*)entropy->workspace, (nuint)(sizeof(uint) * 157), 0);
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

                if (matchlengthMaxValue > 52)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (matchlengthLog > 9)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                ZSTD_buildFSETable((ZSTD_seqSymbol*)entropy->MLTable, (short*)matchlengthNCount, matchlengthMaxValue, (uint*)ML_base, (uint*)ML_bits, matchlengthLog, (void*)entropy->workspace, (nuint)(sizeof(uint) * 157), 0);
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

                if (litlengthMaxValue > 35)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                if (litlengthLog > 9)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                ZSTD_buildFSETable((ZSTD_seqSymbol*)entropy->LLTable, (short*)litlengthNCount, litlengthMaxValue, (uint*)LL_base, (uint*)LL_bits, litlengthLog, (void*)entropy->workspace, (nuint)(sizeof(uint) * 157), 0);
                dictPtr += litlengthHeaderSize;
            }

            if (dictPtr + 12 > dictEnd)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
            }


            {
                int i;
                nuint dictContentSize = (nuint)(dictEnd - (dictPtr + 12));

                for (i = 0; i < 3; i++)
                {
                    uint rep = MEM_readLE32((void*)dictPtr);

                    dictPtr += 4;
                    if (rep == 0 || rep > dictContentSize)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                    }

                    entropy->rep[i] = rep;
                }
            }

            return (nuint)(dictPtr - (byte*)(dict));
        }

        private static nuint ZSTD_decompress_insertDictionary(ZSTD_DCtx_s* dctx, void* dict, nuint dictSize)
        {
            if (dictSize < 8)
            {
                return ZSTD_refDictContent(dctx, dict, dictSize);
            }


            {
                uint magic = MEM_readLE32(dict);

                if (magic != 0xEC30A437)
                {
                    return ZSTD_refDictContent(dctx, dict, dictSize);
                }
            }

            dctx->dictID = MEM_readLE32((void*)((sbyte*)(dict) + 4));

            {
                nuint eSize = ZSTD_loadDEntropy(&dctx->entropy, dict, dictSize);

                if ((ERR_isError(eSize)) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }

                dict = (sbyte*)(dict) + eSize;
                dictSize -= eSize;
            }

            dctx->litEntropy = dctx->fseEntropy = 1;
            return ZSTD_refDictContent(dctx, dict, dictSize);
        }

        public static nuint ZSTD_decompressBegin(ZSTD_DCtx_s* dctx)
        {
            assert(dctx != null);
            dctx->expected = ZSTD_startingInputLength(dctx->format);
            dctx->stage = ZSTD_dStage.ZSTDds_getFrameHeaderSize;
            dctx->processedCSize = 0;
            dctx->decodedSize = 0;
            dctx->previousDstEnd = null;
            dctx->prefixStart = null;
            dctx->virtualStart = null;
            dctx->dictEnd = null;
            dctx->entropy.hufTable[0] = (uint)((12) * 0x1000001);
            dctx->litEntropy = dctx->fseEntropy = 0;
            dctx->dictID = 0;
            dctx->bType = blockType_e.bt_reserved;
            memcpy((void*)(dctx->entropy.rep), (void*)(repStartValue), ((nuint)(sizeof(uint) * 3)));
            dctx->LLTptr = dctx->entropy.LLTable;
            dctx->MLTptr = dctx->entropy.MLTable;
            dctx->OFTptr = dctx->entropy.OFTable;
            dctx->HUFptr = dctx->entropy.hufTable;
            return 0;
        }

        public static nuint ZSTD_decompressBegin_usingDict(ZSTD_DCtx_s* dctx, void* dict, nuint dictSize)
        {

            {
                nuint err_code = (ZSTD_decompressBegin(dctx));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            if (dict != null && dictSize != 0)
            {
                if ((ERR_isError(ZSTD_decompress_insertDictionary(dctx, dict, dictSize))) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                }
            }

            return 0;
        }

        /* ======   ZSTD_DDict   ====== */
        public static nuint ZSTD_decompressBegin_usingDDict(ZSTD_DCtx_s* dctx, ZSTD_DDict_s* ddict)
        {
            assert(dctx != null);
            if (ddict != null)
            {
                sbyte* dictStart = (sbyte*)(ZSTD_DDict_dictContent(ddict));
                nuint dictSize = ZSTD_DDict_dictSize(ddict);
                void* dictEnd = (void*)(dictStart + dictSize);

                dctx->ddictIsCold = ((dctx->dictEnd != dictEnd) ? 1 : 0);
            }


            {
                nuint err_code = (ZSTD_decompressBegin(dctx));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            if (ddict != null)
            {
                ZSTD_copyDDictParameters(dctx, ddict);
            }

            return 0;
        }

        /*! ZSTD_getDictID_fromDict() :
         *  Provides the dictID stored within dictionary.
         *  if @return == 0, the dictionary is not conformant with Zstandard specification.
         *  It can still be loaded, but as a content-only dictionary. */
        public static uint ZSTD_getDictID_fromDict(void* dict, nuint dictSize)
        {
            if (dictSize < 8)
            {
                return 0;
            }

            if (MEM_readLE32(dict) != 0xEC30A437)
            {
                return 0;
            }

            return MEM_readLE32((void*)((sbyte*)(dict) + 4));
        }

        /*! ZSTD_getDictID_fromFrame() :
         *  Provides the dictID required to decompress frame stored within `src`.
         *  If @return == 0, the dictID could not be decoded.
         *  This could for one of the following reasons :
         *  - The frame does not require a dictionary (most common case).
         *  - The frame was built with dictID intentionally removed.
         *    Needed dictionary is a hidden information.
         *    Note : this use case also happens when using a non-conformant dictionary.
         *  - `srcSize` is too small, and as a result, frame header could not be decoded.
         *    Note : possible if `srcSize < ZSTD_FRAMEHEADERSIZE_MAX`.
         *  - This is not a Zstandard frame.
         *  When identifying the exact failure cause, it's possible to use
         *  ZSTD_getFrameHeader(), which will provide a more precise error code. */
        public static uint ZSTD_getDictID_fromFrame(void* src, nuint srcSize)
        {
            ZSTD_frameHeader zfp = new ZSTD_frameHeader
            {
                frameContentSize = 0,
                windowSize = 0,
                blockSizeMax = 0,
                frameType = ZSTD_frameType_e.ZSTD_frame,
                headerSize = 0,
                dictID = 0,
                checksumFlag = 0,
            };
            nuint hError = ZSTD_getFrameHeader(&zfp, src, srcSize);

            if ((ERR_isError(hError)) != 0)
            {
                return 0;
            }

            return zfp.dictID;
        }

        /*! ZSTD_decompress_usingDDict() :
        *   Decompression using a pre-digested Dictionary
        *   Use dictionary without significant overhead. */
        public static nuint ZSTD_decompress_usingDDict(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, ZSTD_DDict_s* ddict)
        {
            return ZSTD_decompressMultiFrame(dctx, dst, dstCapacity, src, srcSize, null, 0, ddict);
        }

        /*=====================================
        *   Streaming decompression
        *====================================*/
        public static ZSTD_DCtx_s* ZSTD_createDStream()
        {
            return ZSTD_createDStream_advanced(ZSTD_defaultCMem);
        }

        public static ZSTD_DCtx_s* ZSTD_initStaticDStream(void* workspace, nuint workspaceSize)
        {
            return ZSTD_initStaticDCtx(workspace, workspaceSize);
        }

        public static ZSTD_DCtx_s* ZSTD_createDStream_advanced(ZSTD_customMem customMem)
        {
            return ZSTD_createDCtx_advanced(customMem);
        }

        public static nuint ZSTD_freeDStream(ZSTD_DCtx_s* zds)
        {
            return ZSTD_freeDCtx(zds);
        }

        /* ***  Initialization  *** */
        public static nuint ZSTD_DStreamInSize()
        {
            return (uint)((1 << 17)) + ZSTD_blockHeaderSize;
        }

        public static nuint ZSTD_DStreamOutSize()
        {
            return (nuint)((1 << 17));
        }

        /*! ZSTD_DCtx_loadDictionary_advanced() :
         *  Same as ZSTD_DCtx_loadDictionary(),
         *  but gives direct control over
         *  how to load the dictionary (by copy ? by reference ?)
         *  and how to interpret it (automatic ? force raw mode ? full mode only ?). */
        public static nuint ZSTD_DCtx_loadDictionary_advanced(ZSTD_DCtx_s* dctx, void* dict, nuint dictSize, ZSTD_dictLoadMethod_e dictLoadMethod, ZSTD_dictContentType_e dictContentType)
        {
            if (dctx->streamStage != ZSTD_dStreamStage.zdss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            ZSTD_clearDict(dctx);
            if (dict != null && dictSize != 0)
            {
                dctx->ddictLocal = ZSTD_createDDict_advanced(dict, dictSize, dictLoadMethod, dictContentType, dctx->customMem);
                if (dctx->ddictLocal == null)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                }

                dctx->ddict = dctx->ddictLocal;
                dctx->dictUses = ZSTD_dictUses_e.ZSTD_use_indefinitely;
            }

            return 0;
        }

        /*! ZSTD_DCtx_loadDictionary_byReference() :
         *  Same as ZSTD_DCtx_loadDictionary(),
         *  but references `dict` content instead of copying it into `dctx`.
         *  This saves memory if `dict` remains around.,
         *  However, it's imperative that `dict` remains accessible (and unmodified) while being used, so it must outlive decompression. */
        public static nuint ZSTD_DCtx_loadDictionary_byReference(ZSTD_DCtx_s* dctx, void* dict, nuint dictSize)
        {
            return ZSTD_DCtx_loadDictionary_advanced(dctx, dict, dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef, ZSTD_dictContentType_e.ZSTD_dct_auto);
        }

        /*! ZSTD_DCtx_loadDictionary() : Requires v1.4.0+
         *  Create an internal DDict from dict buffer,
         *  to be used to decompress next frames.
         *  The dictionary remains valid for all future frames, until explicitly invalidated.
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Special : Adding a NULL (or 0-size) dictionary invalidates any previous dictionary,
         *            meaning "return to no-dictionary mode".
         *  Note 1 : Loading a dictionary involves building tables,
         *           which has a non-negligible impact on CPU usage and latency.
         *           It's recommended to "load once, use many times", to amortize the cost
         *  Note 2 :`dict` content will be copied internally, so `dict` can be released after loading.
         *           Use ZSTD_DCtx_loadDictionary_byReference() to reference dictionary content instead.
         *  Note 3 : Use ZSTD_DCtx_loadDictionary_advanced() to take control of
         *           how dictionary content is loaded and interpreted.
         */
        public static nuint ZSTD_DCtx_loadDictionary(ZSTD_DCtx_s* dctx, void* dict, nuint dictSize)
        {
            return ZSTD_DCtx_loadDictionary_advanced(dctx, dict, dictSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byCopy, ZSTD_dictContentType_e.ZSTD_dct_auto);
        }

        /*! ZSTD_DCtx_refPrefix_advanced() :
         *  Same as ZSTD_DCtx_refPrefix(), but gives finer control over
         *  how to interpret prefix content (automatic ? force raw mode (default) ? full mode only ?) */
        public static nuint ZSTD_DCtx_refPrefix_advanced(ZSTD_DCtx_s* dctx, void* prefix, nuint prefixSize, ZSTD_dictContentType_e dictContentType)
        {

            {
                nuint err_code = (ZSTD_DCtx_loadDictionary_advanced(dctx, prefix, prefixSize, ZSTD_dictLoadMethod_e.ZSTD_dlm_byRef, dictContentType));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            dctx->dictUses = ZSTD_dictUses_e.ZSTD_use_once;
            return 0;
        }

        /*! ZSTD_DCtx_refPrefix() : Requires v1.4.0+
         *  Reference a prefix (single-usage dictionary) to decompress next frame.
         *  This is the reverse operation of ZSTD_CCtx_refPrefix(),
         *  and must use the same prefix as the one used during compression.
         *  Prefix is **only used once**. Reference is discarded at end of frame.
         *  End of frame is reached when ZSTD_decompressStream() returns 0.
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Note 1 : Adding any prefix (including NULL) invalidates any previously set prefix or dictionary
         *  Note 2 : Prefix buffer is referenced. It **must** outlive decompression.
         *           Prefix buffer must remain unmodified up to the end of frame,
         *           reached when ZSTD_decompressStream() returns 0.
         *  Note 3 : By default, the prefix is treated as raw content (ZSTD_dct_rawContent).
         *           Use ZSTD_CCtx_refPrefix_advanced() to alter dictMode (Experimental section)
         *  Note 4 : Referencing a raw content prefix has almost no cpu nor memory cost.
         *           A full dictionary is more costly, as it requires building tables.
         */
        public static nuint ZSTD_DCtx_refPrefix(ZSTD_DCtx_s* dctx, void* prefix, nuint prefixSize)
        {
            return ZSTD_DCtx_refPrefix_advanced(dctx, prefix, prefixSize, ZSTD_dictContentType_e.ZSTD_dct_rawContent);
        }

        /* ZSTD_initDStream_usingDict() :
         * return : expected size, aka ZSTD_startingInputLength().
         * this function cannot fail */
        public static nuint ZSTD_initDStream_usingDict(ZSTD_DCtx_s* zds, void* dict, nuint dictSize)
        {

            {
                nuint err_code = (ZSTD_DCtx_reset(zds, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_DCtx_loadDictionary(zds, dict, dictSize));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_startingInputLength(zds->format);
        }

        /* note : this variant can't fail */
        public static nuint ZSTD_initDStream(ZSTD_DCtx_s* zds)
        {
            return ZSTD_initDStream_usingDDict(zds, (ZSTD_DDict_s*)null);
        }

        /* ZSTD_initDStream_usingDDict() :
         * ddict will just be referenced, and must outlive decompression session
         * this function cannot fail */
        public static nuint ZSTD_initDStream_usingDDict(ZSTD_DCtx_s* dctx, ZSTD_DDict_s* ddict)
        {

            {
                nuint err_code = (ZSTD_DCtx_reset(dctx, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }


            {
                nuint err_code = (ZSTD_DCtx_refDDict(dctx, ddict));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_startingInputLength(dctx->format);
        }

        /* ZSTD_resetDStream() :
         * return : expected size, aka ZSTD_startingInputLength().
         * this function cannot fail */
        public static nuint ZSTD_resetDStream(ZSTD_DCtx_s* dctx)
        {

            {
                nuint err_code = (ZSTD_DCtx_reset(dctx, ZSTD_ResetDirective.ZSTD_reset_session_only));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_startingInputLength(dctx->format);
        }

        /*! ZSTD_DCtx_refDDict() : Requires v1.4.0+
         *  Reference a prepared dictionary, to be used to decompress next frames.
         *  The dictionary remains active for decompression of future frames using same DCtx.
         *
         *  If called with ZSTD_d_refMultipleDDicts enabled, repeated calls of this function
         *  will store the DDict references in a table, and the DDict used for decompression
         *  will be determined at decompression time, as per the dict ID in the frame.
         *  The memory for the table is allocated on the first call to refDDict, and can be
         *  freed with ZSTD_freeDCtx().
         *
         * @result : 0, or an error code (which can be tested with ZSTD_isError()).
         *  Note 1 : Currently, only one dictionary can be managed.
         *           Referencing a new dictionary effectively "discards" any previous one.
         *  Special: referencing a NULL DDict means "return to no-dictionary mode".
         *  Note 2 : DDict is just referenced, its lifetime must outlive its usage from DCtx.
         */
        public static nuint ZSTD_DCtx_refDDict(ZSTD_DCtx_s* dctx, ZSTD_DDict_s* ddict)
        {
            if (dctx->streamStage != ZSTD_dStreamStage.zdss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            ZSTD_clearDict(dctx);
            if (ddict != null)
            {
                dctx->ddict = ddict;
                dctx->dictUses = ZSTD_dictUses_e.ZSTD_use_indefinitely;
                if (dctx->refMultipleDDicts == ZSTD_refMultipleDDicts_e.ZSTD_rmd_refMultipleDDicts)
                {
                    if (dctx->ddictSet == null)
                    {
                        dctx->ddictSet = ZSTD_createDDictHashSet(dctx->customMem);
                        if (dctx->ddictSet == null)
                        {

                            {
                                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                            }

                        }
                    }

                    assert(dctx->staticSize == 0);

                    {
                        nuint err_code = (ZSTD_DDictHashSet_addDDict(dctx->ddictSet, ddict, dctx->customMem));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                }
            }

            return 0;
        }

        /* ZSTD_DCtx_setMaxWindowSize() :
         * note : no direct equivalence in ZSTD_DCtx_setParameter,
         * since this version sets windowSize, and the other sets windowLog */
        public static nuint ZSTD_DCtx_setMaxWindowSize(ZSTD_DCtx_s* dctx, nuint maxWindowSize)
        {
            ZSTD_bounds bounds = ZSTD_dParam_getBounds(ZSTD_dParameter.ZSTD_d_windowLogMax);
            nuint min = (nuint)(1) << bounds.lowerBound;
            nuint max = (nuint)(1) << bounds.upperBound;

            if (dctx->streamStage != ZSTD_dStreamStage.zdss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            if (maxWindowSize < min)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
            }

            if (maxWindowSize > max)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
            }

            dctx->maxWindowSize = maxWindowSize;
            return 0;
        }

        /*! ZSTD_DCtx_setFormat() :
         *  This function is REDUNDANT. Prefer ZSTD_DCtx_setParameter().
         *  Instruct the decoder context about what kind of data to decode next.
         *  This instruction is mandatory to decode data without a fully-formed header,
         *  such ZSTD_f_zstd1_magicless for example.
         * @return : 0, or an error code (which can be tested using ZSTD_isError()). */
        public static nuint ZSTD_DCtx_setFormat(ZSTD_DCtx_s* dctx, ZSTD_format_e format)
        {
            return ZSTD_DCtx_setParameter(dctx, ZSTD_dParameter.ZSTD_d_experimentalParam1, (int)(format));
        }

        /*! ZSTD_dParam_getBounds() :
         *  All parameters must belong to an interval with lower and upper bounds,
         *  otherwise they will either trigger an error or be automatically clamped.
         * @return : a structure, ZSTD_bounds, which contains
         *         - an error status field, which must be tested using ZSTD_isError()
         *         - both lower and upper bounds, inclusive
         */
        public static ZSTD_bounds ZSTD_dParam_getBounds(ZSTD_dParameter dParam)
        {
            ZSTD_bounds bounds = new ZSTD_bounds
            {
                error = 0,
                lowerBound = 0,
                upperBound = 0,
            };

            switch (dParam)
            {
                case ZSTD_dParameter.ZSTD_d_windowLogMax:
                {
                    bounds.lowerBound = 10;
                }

                bounds.upperBound = ((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31));
                return bounds;
                case ZSTD_dParameter.ZSTD_d_experimentalParam1:
                {
                    bounds.lowerBound = (int)(ZSTD_format_e.ZSTD_f_zstd1);
                }

                bounds.upperBound = (int)(ZSTD_format_e.ZSTD_f_zstd1_magicless);
                return bounds;
                case ZSTD_dParameter.ZSTD_d_experimentalParam2:
                {
                    bounds.lowerBound = (int)(ZSTD_bufferMode_e.ZSTD_bm_buffered);
                }

                bounds.upperBound = (int)(ZSTD_bufferMode_e.ZSTD_bm_stable);
                return bounds;
                case ZSTD_dParameter.ZSTD_d_experimentalParam3:
                {
                    bounds.lowerBound = (int)(ZSTD_forceIgnoreChecksum_e.ZSTD_d_validateChecksum);
                }

                bounds.upperBound = (int)(ZSTD_forceIgnoreChecksum_e.ZSTD_d_ignoreChecksum);
                return bounds;
                case ZSTD_dParameter.ZSTD_d_experimentalParam4:
                {
                    bounds.lowerBound = (int)(ZSTD_refMultipleDDicts_e.ZSTD_rmd_refSingleDDict);
                }

                bounds.upperBound = (int)(ZSTD_refMultipleDDicts_e.ZSTD_rmd_refMultipleDDicts);
                return bounds;
                default:
                {
                    ;
                }
                break;
            }

            bounds.error = (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
            return bounds;
        }

        /* ZSTD_dParam_withinBounds:
         * @return 1 if value is within dParam bounds,
         * 0 otherwise */
        private static int ZSTD_dParam_withinBounds(ZSTD_dParameter dParam, int value)
        {
            ZSTD_bounds bounds = ZSTD_dParam_getBounds(dParam);

            if ((ERR_isError(bounds.error)) != 0)
            {
                return 0;
            }

            if (value < bounds.lowerBound)
            {
                return 0;
            }

            if (value > bounds.upperBound)
            {
                return 0;
            }

            return 1;
        }

        /*! ZSTD_DCtx_getParameter() :
         *  Get the requested decompression parameter value, selected by enum ZSTD_dParameter,
         *  and store it into int* value.
         * @return : 0, or an error code (which can be tested with ZSTD_isError()).
         */
        public static nuint ZSTD_DCtx_getParameter(ZSTD_DCtx_s* dctx, ZSTD_dParameter param, int* value)
        {
            switch (param)
            {
                case ZSTD_dParameter.ZSTD_d_windowLogMax:
                {
                    *value = (int)(ZSTD_highbit32((uint)(dctx->maxWindowSize)));
                }

                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam1:
                {
                    *value = (int)(dctx->format);
                }

                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam2:
                {
                    *value = (int)(dctx->outBufferMode);
                }

                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam3:
                {
                    *value = (int)(dctx->forceIgnoreChecksum);
                }

                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam4:
                {
                    *value = (int)(dctx->refMultipleDDicts);
                }

                return 0;
                default:
                {
                    ;
                }
                break;
            }


            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
            }

        }

        /*! ZSTD_DCtx_setParameter() :
         *  Set one compression parameter, selected by enum ZSTD_dParameter.
         *  All parameters have valid bounds. Bounds can be queried using ZSTD_dParam_getBounds().
         *  Providing a value beyond bound will either clamp it, or trigger an error (depending on parameter).
         *  Setting a parameter is only possible during frame initialization (before starting decompression).
         * @return : 0, or an error code (which can be tested using ZSTD_isError()).
         */
        public static nuint ZSTD_DCtx_setParameter(ZSTD_DCtx_s* dctx, ZSTD_dParameter dParam, int value)
        {
            if (dctx->streamStage != ZSTD_dStreamStage.zdss_init)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
            }

            switch (dParam)
            {
                case ZSTD_dParameter.ZSTD_d_windowLogMax:
                {
                    if (value == 0)
                    {
                        value = 27;
                    }
                }


                {
                    if ((ZSTD_dParam_withinBounds(ZSTD_dParameter.ZSTD_d_windowLogMax, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                dctx->maxWindowSize = ((nuint)(1)) << value;
                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam1:
                {
                    if ((ZSTD_dParam_withinBounds(ZSTD_dParameter.ZSTD_d_experimentalParam1, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                dctx->format = (ZSTD_format_e)(value);
                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam2:
                {
                    if ((ZSTD_dParam_withinBounds(ZSTD_dParameter.ZSTD_d_experimentalParam2, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                dctx->outBufferMode = (ZSTD_bufferMode_e)(value);
                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam3:
                {
                    if ((ZSTD_dParam_withinBounds(ZSTD_dParameter.ZSTD_d_experimentalParam3, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                dctx->forceIgnoreChecksum = (ZSTD_forceIgnoreChecksum_e)(value);
                return 0;
                case ZSTD_dParameter.ZSTD_d_experimentalParam4:
                {
                    if ((ZSTD_dParam_withinBounds(ZSTD_dParameter.ZSTD_d_experimentalParam4, value)) == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_outOfBound)));
                    }

                }

                if (dctx->staticSize != 0)
                {

                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
                    }

                }

                dctx->refMultipleDDicts = (ZSTD_refMultipleDDicts_e)(value);
                return 0;
                default:
                {
                    ;
                }
                break;
            }


            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_parameter_unsupported)));
            }

        }

        /*! ZSTD_DCtx_reset() :
         *  Return a DCtx to clean state.
         *  Session and parameters can be reset jointly or separately.
         *  Parameters can only be reset when no active frame is being decompressed.
         * @return : 0, or an error code, which can be tested with ZSTD_isError()
         */
        public static nuint ZSTD_DCtx_reset(ZSTD_DCtx_s* dctx, ZSTD_ResetDirective reset)
        {
            if ((reset == ZSTD_ResetDirective.ZSTD_reset_session_only) || (reset == ZSTD_ResetDirective.ZSTD_reset_session_and_parameters))
            {
                dctx->streamStage = ZSTD_dStreamStage.zdss_init;
                dctx->noForwardProgress = 0;
            }

            if ((reset == ZSTD_ResetDirective.ZSTD_reset_parameters) || (reset == ZSTD_ResetDirective.ZSTD_reset_session_and_parameters))
            {
                if (dctx->streamStage != ZSTD_dStreamStage.zdss_init)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_stage_wrong)));
                }

                ZSTD_clearDict(dctx);
                ZSTD_DCtx_resetParameters(dctx);
            }

            return 0;
        }

        public static nuint ZSTD_sizeof_DStream(ZSTD_DCtx_s* dctx)
        {
            return ZSTD_sizeof_DCtx(dctx);
        }

        public static nuint ZSTD_decodingBufferSize_min(ulong windowSize, ulong frameContentSize)
        {
            nuint blockSize = (nuint)((windowSize) < (uint)(((1 << 17))) ? (windowSize) : ((1 << 17)));
            ulong neededRBSize = windowSize + blockSize + (uint)((32 * 2));
            ulong neededSize = ((frameContentSize) < (neededRBSize) ? (frameContentSize) : (neededRBSize));
            nuint minRBSize = (nuint)(neededSize);

            if ((ulong)(minRBSize) != neededSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_windowTooLarge)));
            }

            return minRBSize;
        }

        public static nuint ZSTD_estimateDStreamSize(nuint windowSize)
        {
            nuint blockSize = (nuint)((windowSize) < (uint)(((1 << 17))) ? (windowSize) : ((1 << 17)));
            nuint inBuffSize = blockSize;
            nuint outBuffSize = ZSTD_decodingBufferSize_min((ulong)windowSize, (unchecked(0UL - 1)));

            return ZSTD_estimateDCtxSize() + inBuffSize + outBuffSize;
        }

        public static nuint ZSTD_estimateDStreamSize_fromFrame(void* src, nuint srcSize)
        {
            uint windowSizeMax = 1U << (unchecked((int)((nuint)(sizeof(nuint)) == 4 ? 30 : 31)));
            ZSTD_frameHeader zfh;
            nuint err = ZSTD_getFrameHeader(&zfh, src, srcSize);

            if ((ERR_isError(err)) != 0)
            {
                return err;
            }

            if (err > 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if (zfh.windowSize > windowSizeMax)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_windowTooLarge)));
            }

            return ZSTD_estimateDStreamSize((nuint)(zfh.windowSize));
        }

        /* *****   Decompression   ***** */
        private static int ZSTD_DCtx_isOverflow(ZSTD_DCtx_s* zds, nuint neededInBuffSize, nuint neededOutBuffSize)
        {
            return (((zds->inBuffSize + zds->outBuffSize) >= (neededInBuffSize + neededOutBuffSize) * 3) ? 1 : 0);
        }

        private static void ZSTD_DCtx_updateOversizedDuration(ZSTD_DCtx_s* zds, nuint neededInBuffSize, nuint neededOutBuffSize)
        {
            if ((ZSTD_DCtx_isOverflow(zds, neededInBuffSize, neededOutBuffSize)) != 0)
            {
                zds->oversizedDuration++;
            }
            else
            {
                zds->oversizedDuration = 0;
            }
        }

        private static int ZSTD_DCtx_isOversizedTooLong(ZSTD_DCtx_s* zds)
        {
            return ((zds->oversizedDuration >= 128) ? 1 : 0);
        }

        /* Checks that the output buffer hasn't changed if ZSTD_obm_stable is used. */
        private static nuint ZSTD_checkOutBuffer(ZSTD_DCtx_s* zds, ZSTD_outBuffer_s* output)
        {
            ZSTD_outBuffer_s expect = zds->expectedOutBuffer;

            if (zds->outBufferMode != ZSTD_bufferMode_e.ZSTD_bm_stable)
            {
                return 0;
            }

            if (zds->streamStage == ZSTD_dStreamStage.zdss_init)
            {
                return 0;
            }

            if (expect.dst == output->dst && expect.pos == output->pos && expect.size == output->size)
            {
                return 0;
            }


            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstBuffer_wrong)));
            }

        }

        /* Calls ZSTD_decompressContinue() with the right parameters for ZSTD_decompressStream()
         * and updates the stage and the output buffer state. This call is extracted so it can be
         * used both when reading directly from the ZSTD_inBuffer, and in buffered input mode.
         * NOTE: You must break after calling this function since the streamStage is modified.
         */
        private static nuint ZSTD_decompressContinueStream(ZSTD_DCtx_s* zds, sbyte** op, sbyte* oend, void* src, nuint srcSize)
        {
            int isSkipFrame = ZSTD_isSkipFrame(zds);

            if (zds->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered)
            {
                nuint dstSize = isSkipFrame != 0 ? 0 : zds->outBuffSize - zds->outStart;
                nuint decodedSize = ZSTD_decompressContinue(zds, (void*)(zds->outBuff + zds->outStart), dstSize, src, srcSize);


                {
                    nuint err_code = (decodedSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (decodedSize == 0 && isSkipFrame == 0)
                {
                    zds->streamStage = ZSTD_dStreamStage.zdss_read;
                }
                else
                {
                    zds->outEnd = zds->outStart + decodedSize;
                    zds->streamStage = ZSTD_dStreamStage.zdss_flush;
                }
            }
            else
            {
                nuint dstSize = isSkipFrame != 0 ? 0 : (nuint)(oend - *op);
                nuint decodedSize = ZSTD_decompressContinue(zds, (void*)*op, dstSize, src, srcSize);


                {
                    nuint err_code = (decodedSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                *op += decodedSize;
                zds->streamStage = ZSTD_dStreamStage.zdss_read;
                assert(*op <= oend);
                assert(zds->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable);
            }

            return 0;
        }

        public static nuint ZSTD_decompressStream(ZSTD_DCtx_s* zds, ZSTD_outBuffer_s* output, ZSTD_inBuffer_s* input)
        {
            sbyte* src = (sbyte*)(input->src);
            sbyte* istart = input->pos != 0 ? src + input->pos : src;
            sbyte* iend = input->size != 0 ? src + input->size : src;
            sbyte* ip = istart;
            sbyte* dst = (sbyte*)(output->dst);
            sbyte* ostart = output->pos != 0 ? dst + output->pos : dst;
            sbyte* oend = output->size != 0 ? dst + output->size : dst;
            sbyte* op = ostart;
            uint someMoreWork = 1;

            if (input->pos > input->size)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if (output->pos > output->size)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }


            {
                nuint err_code = (ZSTD_checkOutBuffer(zds, output));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            while (someMoreWork != 0)
            {
                switch (zds->streamStage)
                {
                    case ZSTD_dStreamStage.zdss_init:
        ;
                    zds->streamStage = ZSTD_dStreamStage.zdss_loadHeader;
                    zds->lhSize = zds->inPos = zds->outStart = zds->outEnd = 0;
                    zds->legacyVersion = 0;
                    zds->hostageByte = 0;
                    zds->expectedOutBuffer = *output;

                    goto case ZSTD_dStreamStage.zdss_loadHeader;
                    case ZSTD_dStreamStage.zdss_loadHeader:
        ;

                    {
                        nuint hSize = ZSTD_getFrameHeader_advanced(&zds->fParams, (void*)zds->headerBuffer, zds->lhSize, zds->format);

                        if (zds->refMultipleDDicts != default && zds->ddictSet != null)
                        {
                            ZSTD_DCtx_selectFrameDDict(zds);
                        }

                        if ((ERR_isError(hSize)) != 0)
                        {
                            return hSize;
                        }

                        if (hSize != 0)
                        {
                            nuint toLoad = hSize - zds->lhSize;
                            nuint remainingInput = (nuint)(iend - ip);

                            assert(iend >= ip);
                            if (toLoad > remainingInput)
                            {
                                if (remainingInput > 0)
                                {
                                    memcpy((void*)((zds->headerBuffer + zds->lhSize)), (void*)(ip), (remainingInput));
                                    zds->lhSize += remainingInput;
                                }

                                input->pos = input->size;
                                return ((((nuint)((zds->format) == ZSTD_format_e.ZSTD_f_zstd1 ? 6 : 2)) > (hSize) ? ((nuint)((zds->format) == ZSTD_format_e.ZSTD_f_zstd1 ? 6 : 2)) : (hSize)) - zds->lhSize) + ZSTD_blockHeaderSize;
                            }

                            assert(ip != null);
                            memcpy((void*)((zds->headerBuffer + zds->lhSize)), (void*)(ip), (toLoad));
                            zds->lhSize = hSize;
                            ip += toLoad;
                            break;
                        }
                    }

                    if (zds->fParams.frameContentSize != (unchecked(0UL - 1)) && zds->fParams.frameType != ZSTD_frameType_e.ZSTD_skippableFrame && (ulong)((nuint)(oend - op)) >= zds->fParams.frameContentSize)
                    {
                        nuint cSize = ZSTD_findFrameCompressedSize((void*)istart, (nuint)(iend - istart));

                        if (cSize <= (nuint)(iend - istart))
                        {
                            nuint decompressedSize = ZSTD_decompress_usingDDict(zds, (void*)op, (nuint)(oend - op), (void*)istart, cSize, ZSTD_getDDict(zds));

                            if ((ERR_isError(decompressedSize)) != 0)
                            {
                                return decompressedSize;
                            }

                            ip = istart + cSize;
                            op += decompressedSize;
                            zds->expected = 0;
                            zds->streamStage = ZSTD_dStreamStage.zdss_init;
                            someMoreWork = 0;
                            break;
                        }
                    }

                    if (zds->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_stable && zds->fParams.frameType != ZSTD_frameType_e.ZSTD_skippableFrame && zds->fParams.frameContentSize != (unchecked(0UL - 1)) && (ulong)((nuint)(oend - op)) < zds->fParams.frameContentSize)
                    {

                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                        }

                    }


                    {
                        nuint err_code = (ZSTD_decompressBegin_usingDDict(zds, ZSTD_getDDict(zds)));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    if ((MEM_readLE32((void*)zds->headerBuffer) & 0xFFFFFFF0) == 0x184D2A50)
                    {
                        zds->expected = MEM_readLE32((void*)(zds->headerBuffer + 4));
                        zds->stage = ZSTD_dStage.ZSTDds_skipFrame;
                    }
                    else
                    {

                        {
                            nuint err_code = (ZSTD_decodeFrameHeader(zds, (void*)zds->headerBuffer, zds->lhSize));

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        zds->expected = ZSTD_blockHeaderSize;
                        zds->stage = ZSTD_dStage.ZSTDds_decodeBlockHeader;
                    }

                    zds->fParams.windowSize = ((zds->fParams.windowSize) > (1U << 10) ? (zds->fParams.windowSize) : (1U << 10));
                    if (zds->fParams.windowSize > zds->maxWindowSize)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_frameParameter_windowTooLarge)));
                    }


                    {
                        nuint neededInBuffSize = ((zds->fParams.blockSizeMax) > (4) ? (zds->fParams.blockSizeMax) : (4));
                        nuint neededOutBuffSize = zds->outBufferMode == ZSTD_bufferMode_e.ZSTD_bm_buffered ? ZSTD_decodingBufferSize_min(zds->fParams.windowSize, zds->fParams.frameContentSize) : 0;

                        ZSTD_DCtx_updateOversizedDuration(zds, neededInBuffSize, neededOutBuffSize);

                        {
                            int tooSmall = (((zds->inBuffSize < neededInBuffSize) || (zds->outBuffSize < neededOutBuffSize)) ? 1 : 0);
                            int tooLarge = ZSTD_DCtx_isOversizedTooLong(zds);

                            if (tooSmall != 0 || tooLarge != 0)
                            {
                                nuint bufferSize = neededInBuffSize + neededOutBuffSize;

                                if (zds->staticSize != 0)
                                {
                                    assert(zds->staticSize >= (nuint)(sizeof(ZSTD_DCtx_s)));
                                    if (bufferSize > zds->staticSize - (nuint)(sizeof(ZSTD_DCtx_s)))
                                    {
                                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                                    }

                                }
                                else
                                {
                                    ZSTD_customFree((void*)zds->inBuff, zds->customMem);
                                    zds->inBuffSize = 0;
                                    zds->outBuffSize = 0;
                                    zds->inBuff = (sbyte*)(ZSTD_customMalloc(bufferSize, zds->customMem));
                                    if (zds->inBuff == null)
                                    {
                                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)));
                                    }

                                }

                                zds->inBuffSize = neededInBuffSize;
                                zds->outBuff = zds->inBuff + zds->inBuffSize;
                                zds->outBuffSize = neededOutBuffSize;
                            }
                        }
                    }

                    zds->streamStage = ZSTD_dStreamStage.zdss_read;

                    goto case ZSTD_dStreamStage.zdss_read;
                    case ZSTD_dStreamStage.zdss_read:
        ;

                    {
                        nuint neededInSize = ZSTD_nextSrcSizeToDecompressWithInputSize(zds, (nuint)(iend - ip));

                        if (neededInSize == 0)
                        {
                            zds->streamStage = ZSTD_dStreamStage.zdss_init;
                            someMoreWork = 0;
                            break;
                        }

                        if ((nuint)(iend - ip) >= neededInSize)
                        {

                            {
                                nuint err_code = (ZSTD_decompressContinueStream(zds, &op, oend, (void*)ip, neededInSize));

                                if ((ERR_isError(err_code)) != 0)
                                {
                                    return err_code;
                                }
                            }

                            ip += neededInSize;
                            break;
                        }
                    }

                    if (ip == iend)
                    {
                        someMoreWork = 0;
                        break;
                    }

                    zds->streamStage = ZSTD_dStreamStage.zdss_load;

                    goto case ZSTD_dStreamStage.zdss_load;
                    case ZSTD_dStreamStage.zdss_load:
                    {
                        nuint neededInSize = ZSTD_nextSrcSizeToDecompress(zds);
                        nuint toLoad = neededInSize - zds->inPos;
                        int isSkipFrame = ZSTD_isSkipFrame(zds);
                        nuint loadedSize;

                        assert(neededInSize == ZSTD_nextSrcSizeToDecompressWithInputSize(zds, (nuint)(iend - ip)));
                        if (isSkipFrame != 0)
                        {
                            loadedSize = ((toLoad) < ((nuint)(iend - ip)) ? (toLoad) : ((nuint)(iend - ip)));
                        }
                        else
                        {
                            if (toLoad > zds->inBuffSize - zds->inPos)
                            {
                                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                            }

                            loadedSize = ZSTD_limitCopy((void*)(zds->inBuff + zds->inPos), toLoad, (void*)ip, (nuint)(iend - ip));
                        }

                        ip += loadedSize;
                        zds->inPos += loadedSize;
                        if (loadedSize < toLoad)
                        {
                            someMoreWork = 0;
                            break;
                        }

                        zds->inPos = 0;

                        {
                            nuint err_code = (ZSTD_decompressContinueStream(zds, &op, oend, (void*)zds->inBuff, neededInSize));

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        break;
                    }

                    case ZSTD_dStreamStage.zdss_flush:
                    {
                        nuint toFlushSize = zds->outEnd - zds->outStart;
                        nuint flushedSize = ZSTD_limitCopy((void*)op, (nuint)(oend - op), (void*)(zds->outBuff + zds->outStart), toFlushSize);

                        op += flushedSize;
                        zds->outStart += flushedSize;
                        if (flushedSize == toFlushSize)
                        {
                            zds->streamStage = ZSTD_dStreamStage.zdss_read;
                            if ((zds->outBuffSize < zds->fParams.frameContentSize) && (zds->outStart + zds->fParams.blockSizeMax > zds->outBuffSize))
                            {
                                zds->outStart = zds->outEnd = 0;
                            }

                            break;
                        }
                    }

                    someMoreWork = 0;
                    break;
                    default:
                    {
                        assert(0 != 0);
                    }


                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                    }

                }
            }

            input->pos = (nuint)(ip - (sbyte*)(input->src));
            output->pos = (nuint)(op - (sbyte*)(output->dst));
            zds->expectedOutBuffer = *output;
            if ((ip == istart) && (op == ostart))
            {
                zds->noForwardProgress++;
                if (zds->noForwardProgress >= 16)
                {
                    if (op == oend)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                    }

                    if (ip == iend)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                    }

                    assert(0 != 0);
                }
            }
            else
            {
                zds->noForwardProgress = 0;
            }


            {
                nuint nextSrcSizeHint = ZSTD_nextSrcSizeToDecompress(zds);

                if (nextSrcSizeHint == 0)
                {
                    if (zds->outEnd == zds->outStart)
                    {
                        if (zds->hostageByte != 0)
                        {
                            if (input->pos >= input->size)
                            {
                                zds->streamStage = ZSTD_dStreamStage.zdss_read;
                                return 1;
                            }

                            input->pos++;
                        }

                        return 0;
                    }

                    if (zds->hostageByte == 0)
                    {
                        input->pos--;
                        zds->hostageByte = 1;
                    }

                    return 1;
                }

                nextSrcSizeHint += ZSTD_blockHeaderSize * (uint)((((ZSTD_nextInputType(zds) == ZSTD_nextInputType_e.ZSTDnit_block)) ? 1 : 0));
                assert(zds->inPos <= nextSrcSizeHint);
                nextSrcSizeHint -= zds->inPos;
                return nextSrcSizeHint;
            }
        }

        /*! ZSTD_decompressStream_simpleArgs() :
         *  Same as ZSTD_decompressStream(),
         *  but using only integral types as arguments.
         *  This can be helpful for binders from dynamic languages
         *  which have troubles handling structures containing memory pointers.
         */
        public static nuint ZSTD_decompressStream_simpleArgs(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, nuint* dstPos, void* src, nuint srcSize, nuint* srcPos)
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
            nuint cErr = ZSTD_decompressStream(dctx, &output, &input);

            *dstPos = output.pos;
            *srcPos = input.pos;
            return cErr;
        }
    }
}
