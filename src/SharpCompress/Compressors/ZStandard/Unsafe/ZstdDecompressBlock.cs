using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*_*******************************************************
     *  Memory operations
     **********************************************************/
    private static void ZSTD_copy4(void* dst, void* src)
    {
        memcpy(dst, src, 4);
    }

    /*-*************************************************************
     *   Block decoding
     ***************************************************************/
    private static nuint ZSTD_blockSizeMax(ZSTD_DCtx_s* dctx)
    {
        nuint blockSizeMax = dctx->isFrameDecompression != 0 ? dctx->fParams.blockSizeMax : 1 << 17;
        assert(blockSizeMax <= 1 << 17);
        return blockSizeMax;
    }

    /*! ZSTD_getcBlockSize() :
     *  Provides the size of compressed block from block header `src` */
    private static nuint ZSTD_getcBlockSize(void* src, nuint srcSize, blockProperties_t* bpPtr)
    {
        if (srcSize < ZSTD_blockHeaderSize)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        {
            uint cBlockHeader = MEM_readLE24(src);
            uint cSize = cBlockHeader >> 3;
            bpPtr->lastBlock = cBlockHeader & 1;
            bpPtr->blockType = (blockType_e)(cBlockHeader >> 1 & 3);
            bpPtr->origSize = cSize;
            if (bpPtr->blockType == blockType_e.bt_rle)
                return 1;
            if (bpPtr->blockType == blockType_e.bt_reserved)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            return cSize;
        }
    }

    /* Allocate buffer for literals, either overlapping current dst, or split between dst and litExtraBuffer, or stored entirely within litExtraBuffer */
    private static void ZSTD_allocateLiteralsBuffer(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint dstCapacity,
        nuint litSize,
        streaming_operation streaming,
        nuint expectedWriteSize,
        uint splitImmediately
    )
    {
        nuint blockSizeMax = ZSTD_blockSizeMax(dctx);
        assert(litSize <= blockSizeMax);
        assert(dctx->isFrameDecompression != 0 || streaming == streaming_operation.not_streaming);
        assert(expectedWriteSize <= blockSizeMax);
        if (
            streaming == streaming_operation.not_streaming
            && dstCapacity > blockSizeMax + 32 + litSize + 32
        )
        {
            dctx->litBuffer = (byte*)dst + blockSizeMax + 32;
            dctx->litBufferEnd = dctx->litBuffer + litSize;
            dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_in_dst;
        }
        else if (litSize <= 1 << 16)
        {
            dctx->litBuffer = dctx->litExtraBuffer;
            dctx->litBufferEnd = dctx->litBuffer + litSize;
            dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_not_in_dst;
        }
        else
        {
            assert(blockSizeMax > 1 << 16);
            if (splitImmediately != 0)
            {
                dctx->litBuffer = (byte*)dst + expectedWriteSize - litSize + (1 << 16) - 32;
                dctx->litBufferEnd = dctx->litBuffer + litSize - (1 << 16);
            }
            else
            {
                dctx->litBuffer = (byte*)dst + expectedWriteSize - litSize;
                dctx->litBufferEnd = (byte*)dst + expectedWriteSize;
            }

            dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_split;
            assert(dctx->litBufferEnd <= (byte*)dst + expectedWriteSize);
        }
    }

    /*! ZSTD_decodeLiteralsBlock() :
     * Where it is possible to do so without being stomped by the output during decompression, the literals block will be stored
     * in the dstBuffer.  If there is room to do so, it will be stored in full in the excess dst space after where the current
     * block will be output.  Otherwise it will be stored at the end of the current dst blockspace, with a small portion being
     * stored in dctx->litExtraBuffer to help keep it "ahead" of the current output write.
     *
     * @return : nb of bytes read from src (< srcSize )
     *  note : symbol not declared but exposed for fullbench */
    private static nuint ZSTD_decodeLiteralsBlock(
        ZSTD_DCtx_s* dctx,
        void* src,
        nuint srcSize,
        void* dst,
        nuint dstCapacity,
        streaming_operation streaming
    )
    {
        if (srcSize < 1 + 1)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        }

        {
            byte* istart = (byte*)src;
            SymbolEncodingType_e litEncType = (SymbolEncodingType_e)(istart[0] & 3);
            nuint blockSizeMax = ZSTD_blockSizeMax(dctx);
            switch (litEncType)
            {
                case SymbolEncodingType_e.set_repeat:
                    if (dctx->litEntropy == 0)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)
                        );
                    }

                    goto case SymbolEncodingType_e.set_compressed;
                case SymbolEncodingType_e.set_compressed:
                    if (srcSize < 5)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                        );
                    }

                    {
                        nuint lhSize,
                            litSize,
                            litCSize;
                        uint singleStream = 0;
                        uint lhlCode = (uint)(istart[0] >> 2 & 3);
                        uint lhc = MEM_readLE32(istart);
                        nuint hufSuccess;
                        nuint expectedWriteSize =
                            blockSizeMax < dstCapacity ? blockSizeMax : dstCapacity;
                        int flags =
                            0
                            | (ZSTD_DCtx_get_bmi2(dctx) != 0 ? (int)HUF_flags_e.HUF_flags_bmi2 : 0)
                            | (
                                dctx->disableHufAsm != 0 ? (int)HUF_flags_e.HUF_flags_disableAsm : 0
                            );
                        switch (lhlCode)
                        {
                            case 0:
                            case 1:
                            default:
                                singleStream = lhlCode == 0 ? 1U : 0U;
                                lhSize = 3;
                                litSize = lhc >> 4 & 0x3FF;
                                litCSize = lhc >> 14 & 0x3FF;
                                break;
                            case 2:
                                lhSize = 4;
                                litSize = lhc >> 4 & 0x3FFF;
                                litCSize = lhc >> 18;
                                break;
                            case 3:
                                lhSize = 5;
                                litSize = lhc >> 4 & 0x3FFFF;
                                litCSize = (lhc >> 22) + ((nuint)istart[4] << 10);
                                break;
                        }

                        if (litSize > 0 && dst == null)
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)
                            );
                        }

                        if (litSize > blockSizeMax)
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                            );
                        }

                        if (singleStream == 0)
                            if (litSize < 6)
                            {
                                return unchecked(
                                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_literals_headerWrong)
                                );
                            }

                        if (litCSize + lhSize > srcSize)
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                            );
                        }

                        if (expectedWriteSize < litSize)
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)
                            );
                        }

                        ZSTD_allocateLiteralsBuffer(
                            dctx,
                            dst,
                            dstCapacity,
                            litSize,
                            streaming,
                            expectedWriteSize,
                            0
                        );
                        if (dctx->ddictIsCold != 0 && litSize > 768)
                        {
                            sbyte* _ptr = (sbyte*)dctx->HUFptr;
                            const nuint _size = sizeof(uint) * 4097;
                            nuint _pos;
                            for (_pos = 0; _pos < _size; _pos += 64)
                            {
#if NETCOREAPP3_0_OR_GREATER
                                if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                                {
                                    System.Runtime.Intrinsics.X86.Sse.Prefetch1(_ptr + _pos);
                                }
#endif
                            }
                        }

                        if (litEncType == SymbolEncodingType_e.set_repeat)
                        {
                            if (singleStream != 0)
                            {
                                hufSuccess = HUF_decompress1X_usingDTable(
                                    dctx->litBuffer,
                                    litSize,
                                    istart + lhSize,
                                    litCSize,
                                    dctx->HUFptr,
                                    flags
                                );
                            }
                            else
                            {
                                assert(litSize >= 6);
                                hufSuccess = HUF_decompress4X_usingDTable(
                                    dctx->litBuffer,
                                    litSize,
                                    istart + lhSize,
                                    litCSize,
                                    dctx->HUFptr,
                                    flags
                                );
                            }
                        }
                        else
                        {
                            if (singleStream != 0)
                            {
                                hufSuccess = HUF_decompress1X1_DCtx_wksp(
                                    dctx->entropy.hufTable,
                                    dctx->litBuffer,
                                    litSize,
                                    istart + lhSize,
                                    litCSize,
                                    dctx->workspace,
                                    sizeof(uint) * 640,
                                    flags
                                );
                            }
                            else
                            {
                                hufSuccess = HUF_decompress4X_hufOnly_wksp(
                                    dctx->entropy.hufTable,
                                    dctx->litBuffer,
                                    litSize,
                                    istart + lhSize,
                                    litCSize,
                                    dctx->workspace,
                                    sizeof(uint) * 640,
                                    flags
                                );
                            }
                        }

                        if (dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split)
                        {
                            assert(litSize > 1 << 16);
                            memcpy(dctx->litExtraBuffer, dctx->litBufferEnd - (1 << 16), 1 << 16);
                            memmove(
                                dctx->litBuffer + (1 << 16) - 32,
                                dctx->litBuffer,
                                litSize - (1 << 16)
                            );
                            dctx->litBuffer += (1 << 16) - 32;
                            dctx->litBufferEnd -= 32;
                            assert(dctx->litBufferEnd <= (byte*)dst + blockSizeMax);
                        }

                        if (ERR_isError(hufSuccess))
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                            );
                        }

                        dctx->litPtr = dctx->litBuffer;
                        dctx->litSize = litSize;
                        dctx->litEntropy = 1;
                        if (litEncType == SymbolEncodingType_e.set_compressed)
                            dctx->HUFptr = dctx->entropy.hufTable;
                        return litCSize + lhSize;
                    }

                case SymbolEncodingType_e.set_basic:
                {
                    nuint litSize,
                        lhSize;
                    uint lhlCode = (uint)(istart[0] >> 2 & 3);
                    nuint expectedWriteSize =
                        blockSizeMax < dstCapacity ? blockSizeMax : dstCapacity;
                    switch (lhlCode)
                    {
                        case 0:
                        case 2:
                        default:
                            lhSize = 1;
                            litSize = (nuint)(istart[0] >> 3);
                            break;
                        case 1:
                            lhSize = 2;
                            litSize = (nuint)(MEM_readLE16(istart) >> 4);
                            break;
                        case 3:
                            lhSize = 3;
                            if (srcSize < 3)
                            {
                                return unchecked(
                                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                                );
                            }

                            litSize = MEM_readLE24(istart) >> 4;
                            break;
                    }

                    if (litSize > 0 && dst == null)
                    {
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                    }

                    if (litSize > blockSizeMax)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                        );
                    }

                    if (expectedWriteSize < litSize)
                    {
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                    }

                    ZSTD_allocateLiteralsBuffer(
                        dctx,
                        dst,
                        dstCapacity,
                        litSize,
                        streaming,
                        expectedWriteSize,
                        1
                    );
                    if (lhSize + litSize + 32 > srcSize)
                    {
                        if (litSize + lhSize > srcSize)
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                            );
                        }

                        if (dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split)
                        {
                            memcpy(dctx->litBuffer, istart + lhSize, (uint)(litSize - (1 << 16)));
                            memcpy(
                                dctx->litExtraBuffer,
                                istart + lhSize + litSize - (1 << 16),
                                1 << 16
                            );
                        }
                        else
                        {
                            memcpy(dctx->litBuffer, istart + lhSize, (uint)litSize);
                        }

                        dctx->litPtr = dctx->litBuffer;
                        dctx->litSize = litSize;
                        return lhSize + litSize;
                    }

                    dctx->litPtr = istart + lhSize;
                    dctx->litSize = litSize;
                    dctx->litBufferEnd = dctx->litPtr + litSize;
                    dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_not_in_dst;
                    return lhSize + litSize;
                }

                case SymbolEncodingType_e.set_rle:
                {
                    uint lhlCode = (uint)(istart[0] >> 2 & 3);
                    nuint litSize,
                        lhSize;
                    nuint expectedWriteSize =
                        blockSizeMax < dstCapacity ? blockSizeMax : dstCapacity;
                    switch (lhlCode)
                    {
                        case 0:
                        case 2:
                        default:
                            lhSize = 1;
                            litSize = (nuint)(istart[0] >> 3);
                            break;
                        case 1:
                            lhSize = 2;
                            if (srcSize < 3)
                            {
                                return unchecked(
                                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                                );
                            }

                            litSize = (nuint)(MEM_readLE16(istart) >> 4);
                            break;
                        case 3:
                            lhSize = 3;
                            if (srcSize < 4)
                            {
                                return unchecked(
                                    (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                                );
                            }

                            litSize = MEM_readLE24(istart) >> 4;
                            break;
                    }

                    if (litSize > 0 && dst == null)
                    {
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                    }

                    if (litSize > blockSizeMax)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                        );
                    }

                    if (expectedWriteSize < litSize)
                    {
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                    }

                    ZSTD_allocateLiteralsBuffer(
                        dctx,
                        dst,
                        dstCapacity,
                        litSize,
                        streaming,
                        expectedWriteSize,
                        1
                    );
                    if (dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split)
                    {
                        memset(dctx->litBuffer, istart[lhSize], (uint)(litSize - (1 << 16)));
                        memset(dctx->litExtraBuffer, istart[lhSize], 1 << 16);
                    }
                    else
                    {
                        memset(dctx->litBuffer, istart[lhSize], (uint)litSize);
                    }

                    dctx->litPtr = dctx->litBuffer;
                    dctx->litSize = litSize;
                    return lhSize + 1;
                }

                default:
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }
        }
    }

    /* Hidden declaration for fullbench */
    private static nuint ZSTD_decodeLiteralsBlock_wrapper(
        ZSTD_DCtx_s* dctx,
        void* src,
        nuint srcSize,
        void* dst,
        nuint dstCapacity
    )
    {
        dctx->isFrameDecompression = 0;
        return ZSTD_decodeLiteralsBlock(
            dctx,
            src,
            srcSize,
            dst,
            dstCapacity,
            streaming_operation.not_streaming
        );
    }

    private static readonly ZSTD_seqSymbol* LL_defaultDTable = GetArrayPointer(
        new ZSTD_seqSymbol[65]
        {
            new ZSTD_seqSymbol(nextState: 1, nbAdditionalBits: 1, nbBits: 1, baseValue: 6),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 4, baseValue: 0),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 0, nbBits: 4, baseValue: 0),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 1),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 3),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 4),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 6),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 7),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 9),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 10),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 12),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 14),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 5, baseValue: 16),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 5, baseValue: 20),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 5, baseValue: 22),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 2, nbBits: 5, baseValue: 28),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 3, nbBits: 5, baseValue: 32),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 4, nbBits: 5, baseValue: 48),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 6, nbBits: 5, baseValue: 64),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 7, nbBits: 5, baseValue: 128),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 8, nbBits: 6, baseValue: 256),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 10, nbBits: 6, baseValue: 1024),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 12, nbBits: 6, baseValue: 4096),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 4, baseValue: 0),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 4, baseValue: 1),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 2),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 4),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 7),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 8),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 10),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 11),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 13),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 1, nbBits: 5, baseValue: 16),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 5, baseValue: 18),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 1, nbBits: 5, baseValue: 22),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 2, nbBits: 5, baseValue: 24),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 3, nbBits: 5, baseValue: 32),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 3, nbBits: 5, baseValue: 40),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 6, nbBits: 4, baseValue: 64),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 6, nbBits: 4, baseValue: 64),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 7, nbBits: 5, baseValue: 128),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 9, nbBits: 6, baseValue: 512),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 11, nbBits: 6, baseValue: 2048),
            new ZSTD_seqSymbol(nextState: 48, nbAdditionalBits: 0, nbBits: 4, baseValue: 0),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 0, nbBits: 4, baseValue: 1),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 2),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 3),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 6),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 8),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 9),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 11),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 12),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 15),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 1, nbBits: 5, baseValue: 18),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 1, nbBits: 5, baseValue: 20),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 2, nbBits: 5, baseValue: 24),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 2, nbBits: 5, baseValue: 28),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 3, nbBits: 5, baseValue: 40),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 4, nbBits: 5, baseValue: 48),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 16, nbBits: 6, baseValue: 65536),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 15, nbBits: 6, baseValue: 32768),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 14, nbBits: 6, baseValue: 16384),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 13, nbBits: 6, baseValue: 8192),
        }
    );
    private static readonly ZSTD_seqSymbol* OF_defaultDTable = GetArrayPointer(
        new ZSTD_seqSymbol[33]
        {
            new ZSTD_seqSymbol(nextState: 1, nbAdditionalBits: 1, nbBits: 1, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 0),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 6, nbBits: 4, baseValue: 61),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 9, nbBits: 5, baseValue: 509),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 15, nbBits: 5, baseValue: 32765),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 21, nbBits: 5, baseValue: 2097149),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 3, nbBits: 5, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 7, nbBits: 4, baseValue: 125),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 12, nbBits: 5, baseValue: 4093),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 18, nbBits: 5, baseValue: 262141),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 23, nbBits: 5, baseValue: 8388605),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 5, nbBits: 5, baseValue: 29),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 8, nbBits: 4, baseValue: 253),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 14, nbBits: 5, baseValue: 16381),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 20, nbBits: 5, baseValue: 1048573),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 2, nbBits: 5, baseValue: 1),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 7, nbBits: 4, baseValue: 125),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 11, nbBits: 5, baseValue: 2045),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 17, nbBits: 5, baseValue: 131069),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 22, nbBits: 5, baseValue: 4194301),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 4, nbBits: 5, baseValue: 13),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 8, nbBits: 4, baseValue: 253),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 13, nbBits: 5, baseValue: 8189),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 19, nbBits: 5, baseValue: 524285),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 5, baseValue: 1),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 6, nbBits: 4, baseValue: 61),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 10, nbBits: 5, baseValue: 1021),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 16, nbBits: 5, baseValue: 65533),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 28, nbBits: 5, baseValue: 268435453),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 27, nbBits: 5, baseValue: 134217725),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 26, nbBits: 5, baseValue: 67108861),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 25, nbBits: 5, baseValue: 33554429),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 24, nbBits: 5, baseValue: 16777213),
        }
    );
    private static readonly ZSTD_seqSymbol* ML_defaultDTable = GetArrayPointer(
        new ZSTD_seqSymbol[65]
        {
            new ZSTD_seqSymbol(nextState: 1, nbAdditionalBits: 1, nbBits: 1, baseValue: 6),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 3),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 4, baseValue: 4),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 6),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 8),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 9),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 11),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 13),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 16),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 19),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 22),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 25),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 28),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 31),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 34),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 6, baseValue: 37),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 6, baseValue: 41),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 2, nbBits: 6, baseValue: 47),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 3, nbBits: 6, baseValue: 59),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 4, nbBits: 6, baseValue: 83),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 7, nbBits: 6, baseValue: 131),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 9, nbBits: 6, baseValue: 515),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 0, nbBits: 4, baseValue: 4),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 4, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 6),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 7),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 9),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 5, baseValue: 10),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 12),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 15),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 18),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 21),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 24),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 27),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 30),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 33),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 6, baseValue: 35),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 1, nbBits: 6, baseValue: 39),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 2, nbBits: 6, baseValue: 43),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 3, nbBits: 6, baseValue: 51),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 4, nbBits: 6, baseValue: 67),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 5, nbBits: 6, baseValue: 99),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 8, nbBits: 6, baseValue: 259),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 4, baseValue: 4),
            new ZSTD_seqSymbol(nextState: 48, nbAdditionalBits: 0, nbBits: 4, baseValue: 4),
            new ZSTD_seqSymbol(nextState: 16, nbAdditionalBits: 0, nbBits: 4, baseValue: 5),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 7),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 8),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 10),
            new ZSTD_seqSymbol(nextState: 32, nbAdditionalBits: 0, nbBits: 5, baseValue: 11),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 14),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 17),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 20),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 23),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 26),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 29),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 0, nbBits: 6, baseValue: 32),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 16, nbBits: 6, baseValue: 65539),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 15, nbBits: 6, baseValue: 32771),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 14, nbBits: 6, baseValue: 16387),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 13, nbBits: 6, baseValue: 8195),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 12, nbBits: 6, baseValue: 4099),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 11, nbBits: 6, baseValue: 2051),
            new ZSTD_seqSymbol(nextState: 0, nbAdditionalBits: 10, nbBits: 6, baseValue: 1027),
        }
    );

    private static void ZSTD_buildSeqTable_rle(ZSTD_seqSymbol* dt, uint baseValue, byte nbAddBits)
    {
        void* ptr = dt;
        ZSTD_seqSymbol_header* DTableH = (ZSTD_seqSymbol_header*)ptr;
        ZSTD_seqSymbol* cell = dt + 1;
        DTableH->tableLog = 0;
        DTableH->fastMode = 0;
        cell->nbBits = 0;
        cell->nextState = 0;
        assert(nbAddBits < 255);
        cell->nbAdditionalBits = nbAddBits;
        cell->baseValue = baseValue;
    }

    /* ZSTD_buildFSETable() :
     * generate FSE decoding table for one symbol (ll, ml or off)
     * cannot fail if input is valid =>
     * all inputs are presumed validated at this stage */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_buildFSETable_body(
        ZSTD_seqSymbol* dt,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint* baseValue,
        byte* nbAdditionalBits,
        uint tableLog,
        void* wksp,
        nuint wkspSize
    )
    {
        ZSTD_seqSymbol* tableDecode = dt + 1;
        uint maxSV1 = maxSymbolValue + 1;
        uint tableSize = (uint)(1 << (int)tableLog);
        ushort* symbolNext = (ushort*)wksp;
        byte* spread = (byte*)(symbolNext + 52 + 1);
        uint highThreshold = tableSize - 1;
        assert(maxSymbolValue <= 52);
        assert(tableLog <= 9);
        assert(wkspSize >= sizeof(short) * (52 + 1) + (1U << 9) + sizeof(ulong));
        {
            ZSTD_seqSymbol_header DTableH;
            DTableH.tableLog = tableLog;
            DTableH.fastMode = 1;
            {
                short largeLimit = (short)(1 << (int)(tableLog - 1));
                uint s;
                for (s = 0; s < maxSV1; s++)
                {
                    if (normalizedCounter[s] == -1)
                    {
                        tableDecode[highThreshold--].baseValue = s;
                        symbolNext[s] = 1;
                    }
                    else
                    {
                        if (normalizedCounter[s] >= largeLimit)
                            DTableH.fastMode = 0;
                        assert(normalizedCounter[s] >= 0);
                        symbolNext[s] = (ushort)normalizedCounter[s];
                    }
                }
            }

            memcpy(dt, &DTableH, (uint)sizeof(ZSTD_seqSymbol_header));
        }

        assert(tableSize <= 512);
        if (highThreshold == tableSize - 1)
        {
            nuint tableMask = tableSize - 1;
            nuint step = (tableSize >> 1) + (tableSize >> 3) + 3;
            {
                const ulong add = 0x0101010101010101UL;
                nuint pos = 0;
                ulong sv = 0;
                uint s;
                for (s = 0; s < maxSV1; ++s, sv += add)
                {
                    int i;
                    int n = normalizedCounter[s];
                    MEM_write64(spread + pos, sv);
                    for (i = 8; i < n; i += 8)
                    {
                        MEM_write64(spread + pos + i, sv);
                    }

                    assert(n >= 0);
                    pos += (nuint)n;
                }
            }

            {
                nuint position = 0;
                nuint s;
                const nuint unroll = 2;
                assert(tableSize % unroll == 0);
                for (s = 0; s < tableSize; s += unroll)
                {
                    nuint u;
                    for (u = 0; u < unroll; ++u)
                    {
                        nuint uPosition = position + u * step & tableMask;
                        tableDecode[uPosition].baseValue = spread[s + u];
                    }

                    position = position + unroll * step & tableMask;
                }

                assert(position == 0);
            }
        }
        else
        {
            uint tableMask = tableSize - 1;
            uint step = (tableSize >> 1) + (tableSize >> 3) + 3;
            uint s,
                position = 0;
            for (s = 0; s < maxSV1; s++)
            {
                int i;
                int n = normalizedCounter[s];
                for (i = 0; i < n; i++)
                {
                    tableDecode[position].baseValue = s;
                    position = position + step & tableMask;
                    while (position > highThreshold)
                        position = position + step & tableMask;
                }
            }

            assert(position == 0);
        }

        {
            uint u;
            for (u = 0; u < tableSize; u++)
            {
                uint symbol = tableDecode[u].baseValue;
                uint nextState = symbolNext[symbol]++;
                tableDecode[u].nbBits = (byte)(tableLog - ZSTD_highbit32(nextState));
                tableDecode[u].nextState = (ushort)(
                    (nextState << tableDecode[u].nbBits) - tableSize
                );
                assert(nbAdditionalBits[symbol] < 255);
                tableDecode[u].nbAdditionalBits = nbAdditionalBits[symbol];
                tableDecode[u].baseValue = baseValue[symbol];
            }
        }
    }

    /* Avoids the FORCE_INLINE of the _body() function. */
    private static void ZSTD_buildFSETable_body_default(
        ZSTD_seqSymbol* dt,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint* baseValue,
        byte* nbAdditionalBits,
        uint tableLog,
        void* wksp,
        nuint wkspSize
    )
    {
        ZSTD_buildFSETable_body(
            dt,
            normalizedCounter,
            maxSymbolValue,
            baseValue,
            nbAdditionalBits,
            tableLog,
            wksp,
            wkspSize
        );
    }

    /* ZSTD_buildFSETable() :
     * generate FSE decoding table for one symbol (ll, ml or off)
     * this function must be called with valid parameters only
     * (dt is large enough, normalizedCounter distribution total is a power of 2, max is within range, etc.)
     * in which case it cannot fail.
     * The workspace must be 4-byte aligned and at least ZSTD_BUILD_FSE_TABLE_WKSP_SIZE bytes, which is
     * defined in zstd_decompress_internal.h.
     * Internal use only.
     */
    private static void ZSTD_buildFSETable(
        ZSTD_seqSymbol* dt,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint* baseValue,
        byte* nbAdditionalBits,
        uint tableLog,
        void* wksp,
        nuint wkspSize,
        int bmi2
    )
    {
        ZSTD_buildFSETable_body_default(
            dt,
            normalizedCounter,
            maxSymbolValue,
            baseValue,
            nbAdditionalBits,
            tableLog,
            wksp,
            wkspSize
        );
    }

    /*! ZSTD_buildSeqTable() :
     * @return : nb bytes read from src,
     *           or an error code if it fails */
    private static nuint ZSTD_buildSeqTable(
        ZSTD_seqSymbol* DTableSpace,
        ZSTD_seqSymbol** DTablePtr,
        SymbolEncodingType_e type,
        uint max,
        uint maxLog,
        void* src,
        nuint srcSize,
        uint* baseValue,
        byte* nbAdditionalBits,
        ZSTD_seqSymbol* defaultTable,
        uint flagRepeatTable,
        int ddictIsCold,
        int nbSeq,
        uint* wksp,
        nuint wkspSize,
        int bmi2
    )
    {
        switch (type)
        {
            case SymbolEncodingType_e.set_rle:
                if (srcSize == 0)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
                }

                if (*(byte*)src > max)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                {
                    uint symbol = *(byte*)src;
                    uint baseline = baseValue[symbol];
                    byte nbBits = nbAdditionalBits[symbol];
                    ZSTD_buildSeqTable_rle(DTableSpace, baseline, nbBits);
                }

                *DTablePtr = DTableSpace;
                return 1;
            case SymbolEncodingType_e.set_basic:
                *DTablePtr = defaultTable;
                return 0;
            case SymbolEncodingType_e.set_repeat:
                if (flagRepeatTable == 0)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                if (ddictIsCold != 0 && nbSeq > 24)
                {
                    void* pStart = *DTablePtr;
                    nuint pSize = (nuint)(sizeof(ZSTD_seqSymbol) * (1 + (1 << (int)maxLog)));
                    {
                        sbyte* _ptr = (sbyte*)pStart;
                        nuint _size = pSize;
                        nuint _pos;
                        for (_pos = 0; _pos < _size; _pos += 64)
                        {
#if NETCOREAPP3_0_OR_GREATER
                            if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
                            {
                                System.Runtime.Intrinsics.X86.Sse.Prefetch1(_ptr + _pos);
                            }
#endif
                        }
                    }
                }

                return 0;
            case SymbolEncodingType_e.set_compressed:
            {
                uint tableLog;
                short* norm = stackalloc short[53];
                nuint headerSize = FSE_readNCount(norm, &max, &tableLog, src, srcSize);
                if (ERR_isError(headerSize))
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                if (tableLog > maxLog)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                ZSTD_buildFSETable(
                    DTableSpace,
                    norm,
                    max,
                    baseValue,
                    nbAdditionalBits,
                    tableLog,
                    wksp,
                    wkspSize,
                    bmi2
                );
                *DTablePtr = DTableSpace;
                return headerSize;
            }

            default:
                assert(0 != 0);
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }
    }

    /*! ZSTD_decodeSeqHeaders() :
     *  decode sequence header from src */
    /*  Used by: zstd_decompress_block, fullbench */
    private static nuint ZSTD_decodeSeqHeaders(
        ZSTD_DCtx_s* dctx,
        int* nbSeqPtr,
        void* src,
        nuint srcSize
    )
    {
        byte* istart = (byte*)src;
        byte* iend = istart + srcSize;
        byte* ip = istart;
        int nbSeq;
        if (srcSize < 1)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        nbSeq = *ip++;
        if (nbSeq > 0x7F)
        {
            if (nbSeq == 0xFF)
            {
                if (ip + 2 > iend)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
                }

                nbSeq = MEM_readLE16(ip) + 0x7F00;
                ip += 2;
            }
            else
            {
                if (ip >= iend)
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
                }

                nbSeq = (nbSeq - 0x80 << 8) + *ip++;
            }
        }

        *nbSeqPtr = nbSeq;
        if (nbSeq == 0)
        {
            if (ip != iend)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            return (nuint)(ip - istart);
        }

        if (ip + 1 > iend)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        if ((*ip & 3) != 0)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        }

        {
            SymbolEncodingType_e LLtype = (SymbolEncodingType_e)(*ip >> 6);
            SymbolEncodingType_e OFtype = (SymbolEncodingType_e)(*ip >> 4 & 3);
            SymbolEncodingType_e MLtype = (SymbolEncodingType_e)(*ip >> 2 & 3);
            ip++;
            {
                nuint llhSize = ZSTD_buildSeqTable(
                    &dctx->entropy.LLTable.e0,
                    &dctx->LLTptr,
                    LLtype,
                    35,
                    9,
                    ip,
                    (nuint)(iend - ip),
                    LL_base,
                    LL_bits,
                    LL_defaultDTable,
                    dctx->fseEntropy,
                    dctx->ddictIsCold,
                    nbSeq,
                    dctx->workspace,
                    sizeof(uint) * 640,
                    ZSTD_DCtx_get_bmi2(dctx)
                );
                if (ERR_isError(llhSize))
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                ip += llhSize;
            }

            {
                nuint ofhSize = ZSTD_buildSeqTable(
                    &dctx->entropy.OFTable.e0,
                    &dctx->OFTptr,
                    OFtype,
                    31,
                    8,
                    ip,
                    (nuint)(iend - ip),
                    OF_base,
                    OF_bits,
                    OF_defaultDTable,
                    dctx->fseEntropy,
                    dctx->ddictIsCold,
                    nbSeq,
                    dctx->workspace,
                    sizeof(uint) * 640,
                    ZSTD_DCtx_get_bmi2(dctx)
                );
                if (ERR_isError(ofhSize))
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                ip += ofhSize;
            }

            {
                nuint mlhSize = ZSTD_buildSeqTable(
                    &dctx->entropy.MLTable.e0,
                    &dctx->MLTptr,
                    MLtype,
                    52,
                    9,
                    ip,
                    (nuint)(iend - ip),
                    ML_base,
                    ML_bits,
                    ML_defaultDTable,
                    dctx->fseEntropy,
                    dctx->ddictIsCold,
                    nbSeq,
                    dctx->workspace,
                    sizeof(uint) * 640,
                    ZSTD_DCtx_get_bmi2(dctx)
                );
                if (ERR_isError(mlhSize))
                {
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                }

                ip += mlhSize;
            }
        }

        return (nuint)(ip - istart);
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_dec32table => new uint[8] { 0, 1, 2, 1, 4, 4, 4, 4 };
    private static uint* dec32table =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_dec32table)
            );
#else

    private static readonly uint* dec32table = GetArrayPointer(
        new uint[8] { 0, 1, 2, 1, 4, 4, 4, 4 }
    );
#endif
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<int> Span_dec64table => new int[8] { 8, 8, 8, 7, 8, 9, 10, 11 };
    private static int* dec64table =>
        (int*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_dec64table)
            );
#else

    private static readonly int* dec64table = GetArrayPointer(
        new int[8] { 8, 8, 8, 7, 8, 9, 10, 11 }
    );
#endif
    /*! ZSTD_overlapCopy8() :
     *  Copies 8 bytes from ip to op and updates op and ip where ip <= op.
     *  If the offset is < 8 then the offset is spread to at least 8 bytes.
     *
     *  Precondition: *ip <= *op
     *  Postcondition: *op - *op >= 8
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_overlapCopy8(byte** op, byte** ip, nuint offset)
    {
        assert(*ip <= *op);
        if (offset < 8)
        {
            int sub2 = dec64table[offset];
            (*op)[0] = (*ip)[0];
            (*op)[1] = (*ip)[1];
            (*op)[2] = (*ip)[2];
            (*op)[3] = (*ip)[3];
            *ip += dec32table[offset];
            ZSTD_copy4(*op + 4, *ip);
            *ip -= sub2;
        }
        else
        {
            ZSTD_copy8(*op, *ip);
        }

        *ip += 8;
        *op += 8;
        assert(*op - *ip >= 8);
    }

    /*! ZSTD_safecopy() :
     *  Specialized version of memcpy() that is allowed to READ up to WILDCOPY_OVERLENGTH past the input buffer
     *  and write up to 16 bytes past oend_w (op >= oend_w is allowed).
     *  This function is only called in the uncommon case where the sequence is near the end of the block. It
     *  should be fast for a single long sequence, but can be slow for several short sequences.
     *
     *  @param ovtype controls the overlap detection
     *         - ZSTD_no_overlap: The source and destination are guaranteed to be at least WILDCOPY_VECLEN bytes apart.
     *         - ZSTD_overlap_src_before_dst: The src and dst may overlap and may be any distance apart.
     *           The src buffer must be before the dst buffer.
     */
    private static void ZSTD_safecopy(
        byte* op,
        byte* oend_w,
        byte* ip,
        nint length,
        ZSTD_overlap_e ovtype
    )
    {
        nint diff = (nint)(op - ip);
        byte* oend = op + length;
        assert(
            ovtype == ZSTD_overlap_e.ZSTD_no_overlap && (diff <= -8 || diff >= 8 || op >= oend_w)
                || ovtype == ZSTD_overlap_e.ZSTD_overlap_src_before_dst && diff >= 0
        );
        if (length < 8)
        {
            while (op < oend)
                *op++ = *ip++;
            return;
        }

        if (ovtype == ZSTD_overlap_e.ZSTD_overlap_src_before_dst)
        {
            assert(length >= 8);
            ZSTD_overlapCopy8(&op, &ip, (nuint)diff);
            length -= 8;
            assert(op - ip >= 8);
            assert(op <= oend);
        }

        if (oend <= oend_w)
        {
            ZSTD_wildcopy(op, ip, length, ovtype);
            return;
        }

        if (op <= oend_w)
        {
            assert(oend > oend_w);
            ZSTD_wildcopy(op, ip, (nint)(oend_w - op), ovtype);
            ip += oend_w - op;
            op += oend_w - op;
        }

        while (op < oend)
            *op++ = *ip++;
    }

    /* ZSTD_safecopyDstBeforeSrc():
     * This version allows overlap with dst before src, or handles the non-overlap case with dst after src
     * Kept separate from more common ZSTD_safecopy case to avoid performance impact to the safecopy common case */
    private static void ZSTD_safecopyDstBeforeSrc(byte* op, byte* ip, nint length)
    {
        nint diff = (nint)(op - ip);
        byte* oend = op + length;
        if (length < 8 || diff > -8)
        {
            while (op < oend)
                *op++ = *ip++;
            return;
        }

        if (op <= oend - 32 && diff < -16)
        {
            ZSTD_wildcopy(op, ip, (nint)(oend - 32 - op), ZSTD_overlap_e.ZSTD_no_overlap);
            ip += oend - 32 - op;
            op += oend - 32 - op;
        }

        while (op < oend)
            *op++ = *ip++;
    }

    /* ZSTD_execSequenceEnd():
     * This version handles cases that are near the end of the output buffer. It requires
     * more careful checks to make sure there is no overflow. By separating out these hard
     * and unlikely cases, we can speed up the common cases.
     *
     * NOTE: This function needs to be fast for a single long sequence, but doesn't need
     * to be optimized for many small sequences, since those fall into ZSTD_execSequence().
     */
    private static nuint ZSTD_execSequenceEnd(
        byte* op,
        byte* oend,
        seq_t sequence,
        byte** litPtr,
        byte* litLimit,
        byte* prefixStart,
        byte* virtualStart,
        byte* dictEnd
    )
    {
        byte* oLitEnd = op + sequence.litLength;
        nuint sequenceLength = sequence.litLength + sequence.matchLength;
        byte* iLitEnd = *litPtr + sequence.litLength;
        byte* match = oLitEnd - sequence.offset;
        byte* oend_w = oend - 32;
        if (sequenceLength > (nuint)(oend - op))
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (sequence.litLength > (nuint)(litLimit - *litPtr))
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        }

        assert(op < op + sequenceLength);
        assert(oLitEnd < op + sequenceLength);
        ZSTD_safecopy(
            op,
            oend_w,
            *litPtr,
            (nint)sequence.litLength,
            ZSTD_overlap_e.ZSTD_no_overlap
        );
        op = oLitEnd;
        *litPtr = iLitEnd;
        if (sequence.offset > (nuint)(oLitEnd - prefixStart))
        {
            if (sequence.offset > (nuint)(oLitEnd - virtualStart))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            match = dictEnd - (prefixStart - match);
            if (match + sequence.matchLength <= dictEnd)
            {
                memmove(oLitEnd, match, sequence.matchLength);
                return sequenceLength;
            }

            {
                nuint length1 = (nuint)(dictEnd - match);
                memmove(oLitEnd, match, length1);
                op = oLitEnd + length1;
                sequence.matchLength -= length1;
                match = prefixStart;
            }
        }

        ZSTD_safecopy(
            op,
            oend_w,
            match,
            (nint)sequence.matchLength,
            ZSTD_overlap_e.ZSTD_overlap_src_before_dst
        );
        return sequenceLength;
    }

    /* ZSTD_execSequenceEndSplitLitBuffer():
     * This version is intended to be used during instances where the litBuffer is still split.  It is kept separate to avoid performance impact for the good case.
     */
    private static nuint ZSTD_execSequenceEndSplitLitBuffer(
        byte* op,
        byte* oend,
        byte* oend_w,
        seq_t sequence,
        byte** litPtr,
        byte* litLimit,
        byte* prefixStart,
        byte* virtualStart,
        byte* dictEnd
    )
    {
        byte* oLitEnd = op + sequence.litLength;
        nuint sequenceLength = sequence.litLength + sequence.matchLength;
        byte* iLitEnd = *litPtr + sequence.litLength;
        byte* match = oLitEnd - sequence.offset;
        if (sequenceLength > (nuint)(oend - op))
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (sequence.litLength > (nuint)(litLimit - *litPtr))
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        }

        assert(op < op + sequenceLength);
        assert(oLitEnd < op + sequenceLength);
        if (op > *litPtr && op < *litPtr + sequence.litLength)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        ZSTD_safecopyDstBeforeSrc(op, *litPtr, (nint)sequence.litLength);
        op = oLitEnd;
        *litPtr = iLitEnd;
        if (sequence.offset > (nuint)(oLitEnd - prefixStart))
        {
            if (sequence.offset > (nuint)(oLitEnd - virtualStart))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            match = dictEnd - (prefixStart - match);
            if (match + sequence.matchLength <= dictEnd)
            {
                memmove(oLitEnd, match, sequence.matchLength);
                return sequenceLength;
            }

            {
                nuint length1 = (nuint)(dictEnd - match);
                memmove(oLitEnd, match, length1);
                op = oLitEnd + length1;
                sequence.matchLength -= length1;
                match = prefixStart;
            }
        }

        ZSTD_safecopy(
            op,
            oend_w,
            match,
            (nint)sequence.matchLength,
            ZSTD_overlap_e.ZSTD_overlap_src_before_dst
        );
        return sequenceLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_execSequence(
        byte* op,
        byte* oend,
        seq_t sequence,
        byte** litPtr,
        byte* litLimit,
        byte* prefixStart,
        byte* virtualStart,
        byte* dictEnd
    )
    {
        var sequence_litLength = sequence.litLength;
        var sequence_matchLength = sequence.matchLength;
        var sequence_offset = sequence.offset;
        byte* oLitEnd = op + sequence_litLength;
        nuint sequenceLength = sequence_litLength + sequence_matchLength;
        /* risk : address space overflow (32-bits) */
        byte* oMatchEnd = op + sequenceLength;
        /* risk : address space underflow on oend=NULL */
        byte* oend_w = oend - 32;
        byte* iLitEnd = *litPtr + sequence_litLength;
        byte* match = oLitEnd - sequence_offset;
        assert(op != null);
        assert(oend_w < oend);
        if (
            iLitEnd > litLimit
            || oMatchEnd > oend_w
            || MEM_32bits && (nuint)(oend - op) < sequenceLength + 32
        )
            return ZSTD_execSequenceEnd(
                op,
                oend,
                new seq_t
                {
                    litLength = sequence_litLength,
                    matchLength = sequence_matchLength,
                    offset = sequence_offset,
                },
                litPtr,
                litLimit,
                prefixStart,
                virtualStart,
                dictEnd
            );
        assert(op <= oLitEnd);
        assert(oLitEnd < oMatchEnd);
        assert(oMatchEnd <= oend);
        assert(iLitEnd <= litLimit);
        assert(oLitEnd <= oend_w);
        assert(oMatchEnd <= oend_w);
        assert(32 >= 16);
        ZSTD_copy16(op, *litPtr);
        if (sequence_litLength > 16)
        {
            ZSTD_wildcopy(
                op + 16,
                *litPtr + 16,
                (nint)(sequence_litLength - 16),
                ZSTD_overlap_e.ZSTD_no_overlap
            );
        }

        op = oLitEnd;
        *litPtr = iLitEnd;
        if (sequence_offset > (nuint)(oLitEnd - prefixStart))
        {
            if (sequence_offset > (nuint)(oLitEnd - virtualStart))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            match = dictEnd + (match - prefixStart);
            if (match + sequence_matchLength <= dictEnd)
            {
                memmove(oLitEnd, match, sequence_matchLength);
                return sequenceLength;
            }

            {
                nuint length1 = (nuint)(dictEnd - match);
                memmove(oLitEnd, match, length1);
                op = oLitEnd + length1;
                sequence_matchLength -= length1;
                match = prefixStart;
            }
        }

        assert(op <= oMatchEnd);
        assert(oMatchEnd <= oend_w);
        assert(match >= prefixStart);
        assert(sequence_matchLength >= 1);
        if (sequence_offset >= 16)
        {
            ZSTD_wildcopy(op, match, (nint)sequence_matchLength, ZSTD_overlap_e.ZSTD_no_overlap);
            return sequenceLength;
        }

        assert(sequence_offset < 16);
        ZSTD_overlapCopy8(ref op, ref match, sequence_offset);
        if (sequence_matchLength > 8)
        {
            assert(op < oMatchEnd);
            ZSTD_wildcopy(
                op,
                match,
                (nint)sequence_matchLength - 8,
                ZSTD_overlap_e.ZSTD_overlap_src_before_dst
            );
        }

        return sequenceLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_execSequenceSplitLitBuffer(
        byte* op,
        byte* oend,
        byte* oend_w,
        seq_t sequence,
        byte** litPtr,
        byte* litLimit,
        byte* prefixStart,
        byte* virtualStart,
        byte* dictEnd
    )
    {
        byte* oLitEnd = op + sequence.litLength;
        nuint sequenceLength = sequence.litLength + sequence.matchLength;
        /* risk : address space overflow (32-bits) */
        byte* oMatchEnd = op + sequenceLength;
        byte* iLitEnd = *litPtr + sequence.litLength;
        byte* match = oLitEnd - sequence.offset;
        assert(op != null);
        assert(oend_w < oend);
        if (
            iLitEnd > litLimit
            || oMatchEnd > oend_w
            || MEM_32bits && (nuint)(oend - op) < sequenceLength + 32
        )
            return ZSTD_execSequenceEndSplitLitBuffer(
                op,
                oend,
                oend_w,
                sequence,
                litPtr,
                litLimit,
                prefixStart,
                virtualStart,
                dictEnd
            );
        assert(op <= oLitEnd);
        assert(oLitEnd < oMatchEnd);
        assert(oMatchEnd <= oend);
        assert(iLitEnd <= litLimit);
        assert(oLitEnd <= oend_w);
        assert(oMatchEnd <= oend_w);
        assert(32 >= 16);
        ZSTD_copy16(op, *litPtr);
        if (sequence.litLength > 16)
        {
            ZSTD_wildcopy(
                op + 16,
                *litPtr + 16,
                (nint)(sequence.litLength - 16),
                ZSTD_overlap_e.ZSTD_no_overlap
            );
        }

        op = oLitEnd;
        *litPtr = iLitEnd;
        if (sequence.offset > (nuint)(oLitEnd - prefixStart))
        {
            if (sequence.offset > (nuint)(oLitEnd - virtualStart))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            match = dictEnd + (match - prefixStart);
            if (match + sequence.matchLength <= dictEnd)
            {
                memmove(oLitEnd, match, sequence.matchLength);
                return sequenceLength;
            }

            {
                nuint length1 = (nuint)(dictEnd - match);
                memmove(oLitEnd, match, length1);
                op = oLitEnd + length1;
                sequence.matchLength -= length1;
                match = prefixStart;
            }
        }

        assert(op <= oMatchEnd);
        assert(oMatchEnd <= oend_w);
        assert(match >= prefixStart);
        assert(sequence.matchLength >= 1);
        if (sequence.offset >= 16)
        {
            ZSTD_wildcopy(op, match, (nint)sequence.matchLength, ZSTD_overlap_e.ZSTD_no_overlap);
            return sequenceLength;
        }

        assert(sequence.offset < 16);
        ZSTD_overlapCopy8(&op, &match, sequence.offset);
        if (sequence.matchLength > 8)
        {
            assert(op < oMatchEnd);
            ZSTD_wildcopy(
                op,
                match,
                (nint)sequence.matchLength - 8,
                ZSTD_overlap_e.ZSTD_overlap_src_before_dst
            );
        }

        return sequenceLength;
    }

    private static void ZSTD_initFseState(
        ZSTD_fseState* DStatePtr,
        BIT_DStream_t* bitD,
        ZSTD_seqSymbol* dt
    )
    {
        void* ptr = dt;
        ZSTD_seqSymbol_header* DTableH = (ZSTD_seqSymbol_header*)ptr;
        DStatePtr->state = BIT_readBits(bitD, DTableH->tableLog);
        BIT_reloadDStream(bitD);
        DStatePtr->table = dt + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_updateFseStateWithDInfo(
        ZSTD_fseState* DStatePtr,
        BIT_DStream_t* bitD,
        ushort nextState,
        uint nbBits
    )
    {
        nuint lowBits = BIT_readBits(bitD, nbBits);
        DStatePtr->state = nextState + lowBits;
    }

    /**
     * ZSTD_decodeSequence():
     * @p longOffsets : tells the decoder to reload more bit while decoding large offsets
     *                  only used in 32-bit mode
     * @return : Sequence (litL + matchL + offset)
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static seq_t ZSTD_decodeSequence(
        seqState_t* seqState,
        ZSTD_longOffset_e longOffsets,
        int isLastSeq
    )
    {
        seq_t seq;
        ZSTD_seqSymbol* llDInfo = seqState->stateLL.table + seqState->stateLL.state;
        ZSTD_seqSymbol* mlDInfo = seqState->stateML.table + seqState->stateML.state;
        ZSTD_seqSymbol* ofDInfo = seqState->stateOffb.table + seqState->stateOffb.state;
        seq.matchLength = mlDInfo->baseValue;
        seq.litLength = llDInfo->baseValue;
        {
            uint ofBase = ofDInfo->baseValue;
            byte llBits = llDInfo->nbAdditionalBits;
            byte mlBits = mlDInfo->nbAdditionalBits;
            byte ofBits = ofDInfo->nbAdditionalBits;
            byte totalBits = (byte)(llBits + mlBits + ofBits);
            ushort llNext = llDInfo->nextState;
            ushort mlNext = mlDInfo->nextState;
            ushort ofNext = ofDInfo->nextState;
            uint llnbBits = llDInfo->nbBits;
            uint mlnbBits = mlDInfo->nbBits;
            uint ofnbBits = ofDInfo->nbBits;
            assert(llBits <= 16);
            assert(mlBits <= 16);
            assert(ofBits <= 31);
            {
                nuint offset;
                if (ofBits > 1)
                {
                    if (MEM_32bits && longOffsets != default && ofBits >= 25)
                    {
                        /* Always read extra bits, this keeps the logic simple,
                         * avoids branches, and avoids accidentally reading 0 bits.
                         */
                        const uint extraBits = 30 - 25;
                        offset =
                            ofBase
                            + (
                                BIT_readBitsFast(&seqState->DStream, ofBits - extraBits)
                                << (int)extraBits
                            );
                        BIT_reloadDStream(&seqState->DStream);
                        offset += BIT_readBitsFast(&seqState->DStream, extraBits);
                    }
                    else
                    {
                        offset = ofBase + BIT_readBitsFast(&seqState->DStream, ofBits);
                        if (MEM_32bits)
                            BIT_reloadDStream(&seqState->DStream);
                    }

                    seqState->prevOffset.e2 = seqState->prevOffset.e1;
                    seqState->prevOffset.e1 = seqState->prevOffset.e0;
                    seqState->prevOffset.e0 = offset;
                }
                else
                {
                    uint ll0 = llDInfo->baseValue == 0 ? 1U : 0U;
                    if (ofBits == 0)
                    {
                        offset = (&seqState->prevOffset.e0)[ll0];
                        seqState->prevOffset.e1 = (&seqState->prevOffset.e0)[ll0 == 0 ? 1 : 0];
                        seqState->prevOffset.e0 = offset;
                    }
                    else
                    {
                        offset = ofBase + ll0 + BIT_readBitsFast(&seqState->DStream, 1);
                        {
                            nuint temp =
                                offset == 3
                                    ? seqState->prevOffset.e0 - 1
                                    : (&seqState->prevOffset.e0)[offset];
                            temp -= temp == 0 ? 1U : 0U;
                            if (offset != 1)
                                seqState->prevOffset.e2 = seqState->prevOffset.e1;
                            seqState->prevOffset.e1 = seqState->prevOffset.e0;
                            seqState->prevOffset.e0 = offset = temp;
                        }
                    }
                }

                seq.offset = offset;
            }

            if (mlBits > 0)
                seq.matchLength += BIT_readBitsFast(&seqState->DStream, mlBits);
            if (MEM_32bits && mlBits + llBits >= 25 - (30 - 25))
                BIT_reloadDStream(&seqState->DStream);
            if (MEM_64bits && totalBits >= 57 - (9 + 9 + 8))
                BIT_reloadDStream(&seqState->DStream);
            if (llBits > 0)
                seq.litLength += BIT_readBitsFast(&seqState->DStream, llBits);
            if (MEM_32bits)
                BIT_reloadDStream(&seqState->DStream);
            if (isLastSeq == 0)
            {
                ZSTD_updateFseStateWithDInfo(
                    &seqState->stateLL,
                    &seqState->DStream,
                    llNext,
                    llnbBits
                );
                ZSTD_updateFseStateWithDInfo(
                    &seqState->stateML,
                    &seqState->DStream,
                    mlNext,
                    mlnbBits
                );
                if (MEM_32bits)
                    BIT_reloadDStream(&seqState->DStream);
                ZSTD_updateFseStateWithDInfo(
                    &seqState->stateOffb,
                    &seqState->DStream,
                    ofNext,
                    ofnbBits
                );
                BIT_reloadDStream(&seqState->DStream);
            }
        }

        return seq;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_decompressSequences_bodySplitLitBuffer(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        byte* ip = (byte*)seqStart;
        byte* iend = ip + seqSize;
        byte* ostart = (byte*)dst;
        byte* oend = ZSTD_maybeNullPtrAdd(ostart, (nint)maxDstSize);
        byte* op = ostart;
        byte* litPtr = dctx->litPtr;
        byte* litBufferEnd = dctx->litBufferEnd;
        byte* prefixStart = (byte*)dctx->prefixStart;
        byte* vBase = (byte*)dctx->virtualStart;
        byte* dictEnd = (byte*)dctx->dictEnd;
        if (nbSeq != 0)
        {
            seqState_t seqState;
            dctx->fseEntropy = 1;
            {
                uint i;
                for (i = 0; i < 3; i++)
                    (&seqState.prevOffset.e0)[i] = dctx->entropy.rep[i];
            }

            if (ERR_isError(BIT_initDStream(&seqState.DStream, ip, (nuint)(iend - ip))))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            ZSTD_initFseState(&seqState.stateLL, &seqState.DStream, dctx->LLTptr);
            ZSTD_initFseState(&seqState.stateOffb, &seqState.DStream, dctx->OFTptr);
            ZSTD_initFseState(&seqState.stateML, &seqState.DStream, dctx->MLTptr);
            assert(dst != null);
            {
                /* some static analyzer believe that @sequence is not initialized (it necessarily is, since for(;;) loop as at least one iteration) */
                seq_t sequence = new seq_t
                {
                    litLength = 0,
                    matchLength = 0,
                    offset = 0,
                };
                for (; nbSeq != 0; nbSeq--)
                {
                    sequence = ZSTD_decodeSequence(&seqState, isLongOffset, nbSeq == 1 ? 1 : 0);
                    if (litPtr + sequence.litLength > dctx->litBufferEnd)
                        break;
                    {
                        nuint oneSeqSize = ZSTD_execSequenceSplitLitBuffer(
                            op,
                            oend,
                            litPtr + sequence.litLength - 32,
                            sequence,
                            &litPtr,
                            litBufferEnd,
                            prefixStart,
                            vBase,
                            dictEnd
                        );
                        if (ERR_isError(oneSeqSize))
                            return oneSeqSize;
                        op += oneSeqSize;
                    }
                }

                if (nbSeq > 0)
                {
                    nuint leftoverLit = (nuint)(dctx->litBufferEnd - litPtr);
                    if (leftoverLit != 0)
                    {
                        if (leftoverLit > (nuint)(oend - op))
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)
                            );
                        }

                        ZSTD_safecopyDstBeforeSrc(op, litPtr, (nint)leftoverLit);
                        sequence.litLength -= leftoverLit;
                        op += leftoverLit;
                    }

                    litPtr = dctx->litExtraBuffer;
                    litBufferEnd = dctx->litExtraBuffer + (1 << 16);
                    dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_not_in_dst;
                    {
                        nuint oneSeqSize = ZSTD_execSequence(
                            op,
                            oend,
                            sequence,
                            &litPtr,
                            litBufferEnd,
                            prefixStart,
                            vBase,
                            dictEnd
                        );
                        if (ERR_isError(oneSeqSize))
                            return oneSeqSize;
                        op += oneSeqSize;
                    }

                    nbSeq--;
                }
            }

            if (nbSeq > 0)
            {
                for (; nbSeq != 0; nbSeq--)
                {
                    seq_t sequence = ZSTD_decodeSequence(
                        &seqState,
                        isLongOffset,
                        nbSeq == 1 ? 1 : 0
                    );
                    nuint oneSeqSize = ZSTD_execSequence(
                        op,
                        oend,
                        sequence,
                        &litPtr,
                        litBufferEnd,
                        prefixStart,
                        vBase,
                        dictEnd
                    );
                    if (ERR_isError(oneSeqSize))
                        return oneSeqSize;
                    op += oneSeqSize;
                }
            }

            if (nbSeq != 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            if (BIT_endOfDStream(&seqState.DStream) == 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            {
                uint i;
                for (i = 0; i < 3; i++)
                    dctx->entropy.rep[i] = (uint)(&seqState.prevOffset.e0)[i];
            }
        }

        if (dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split)
        {
            /* split hasn't been reached yet, first get dst then copy litExtraBuffer */
            nuint lastLLSize = (nuint)(litBufferEnd - litPtr);
            if (lastLLSize > (nuint)(oend - op))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (op != null)
            {
                memmove(op, litPtr, lastLLSize);
                op += lastLLSize;
            }

            litPtr = dctx->litExtraBuffer;
            litBufferEnd = dctx->litExtraBuffer + (1 << 16);
            dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_not_in_dst;
        }

        {
            nuint lastLLSize = (nuint)(litBufferEnd - litPtr);
            if (lastLLSize > (nuint)(oend - op))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (op != null)
            {
                memcpy(op, litPtr, (uint)lastLLSize);
                op += lastLLSize;
            }
        }

        return (nuint)(op - ostart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_decompressSequences_body(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        // HACK, force nbSeq to stack (better register usage)
        System.Threading.Volatile.Read(ref nbSeq);
        byte* ip = (byte*)seqStart;
        byte* iend = ip + seqSize;
        byte* ostart = (byte*)dst;
        byte* oend =
            dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_not_in_dst
                ? ZSTD_maybeNullPtrAdd(ostart, (nint)maxDstSize)
                : dctx->litBuffer;
        byte* op = ostart;
        byte* litPtr = dctx->litPtr;
        byte* litEnd = litPtr + dctx->litSize;
        byte* prefixStart = (byte*)dctx->prefixStart;
        byte* vBase = (byte*)dctx->virtualStart;
        byte* dictEnd = (byte*)dctx->dictEnd;
        if (nbSeq != 0)
        {
            seqState_t seqState;
            System.Runtime.CompilerServices.Unsafe.SkipInit(out seqState);
            dctx->fseEntropy = 1;
            {
                uint i;
                for (i = 0; i < 3; i++)
                    System.Runtime.CompilerServices.Unsafe.Add(ref seqState.prevOffset.e0, (int)i) =
                        dctx->entropy.rep[i];
            }

            if (ERR_isError(BIT_initDStream(ref seqState.DStream, ip, (nuint)(iend - ip))))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            ZSTD_initFseState(ref seqState.stateLL, ref seqState.DStream, dctx->LLTptr);
            ZSTD_initFseState(ref seqState.stateOffb, ref seqState.DStream, dctx->OFTptr);
            ZSTD_initFseState(ref seqState.stateML, ref seqState.DStream, dctx->MLTptr);
            assert(dst != null);
            nuint seqState_DStream_bitContainer = seqState.DStream.bitContainer;
            uint seqState_DStream_bitsConsumed = seqState.DStream.bitsConsumed;
            sbyte* seqState_DStream_ptr = seqState.DStream.ptr;
            sbyte* seqState_DStream_start = seqState.DStream.start;
            sbyte* seqState_DStream_limitPtr = seqState.DStream.limitPtr;
            for (; nbSeq != 0; nbSeq--)
            {
                nuint sequence_litLength;
                nuint sequence_matchLength;
                nuint sequence_offset;
                ZSTD_seqSymbol* llDInfo = seqState.stateLL.table + seqState.stateLL.state;
                ZSTD_seqSymbol* mlDInfo = seqState.stateML.table + seqState.stateML.state;
                ZSTD_seqSymbol* ofDInfo = seqState.stateOffb.table + seqState.stateOffb.state;
                sequence_matchLength = mlDInfo->baseValue;
                sequence_litLength = llDInfo->baseValue;
                {
                    uint ofBase = ofDInfo->baseValue;
                    byte llBits = llDInfo->nbAdditionalBits;
                    byte mlBits = mlDInfo->nbAdditionalBits;
                    byte ofBits = ofDInfo->nbAdditionalBits;
                    byte totalBits = (byte)(llBits + mlBits + ofBits);
                    ushort llNext = llDInfo->nextState;
                    ushort mlNext = mlDInfo->nextState;
                    ushort ofNext = ofDInfo->nextState;
                    uint llnbBits = llDInfo->nbBits;
                    uint mlnbBits = mlDInfo->nbBits;
                    uint ofnbBits = ofDInfo->nbBits;
                    assert(llBits <= 16);
                    assert(mlBits <= 16);
                    assert(ofBits <= 31);
                    {
                        nuint offset;
                        if (ofBits > 1)
                        {
                            if (MEM_32bits && isLongOffset != default && ofBits >= 25)
                            {
                                /* Always read extra bits, this keeps the logic simple,
                                 * avoids branches, and avoids accidentally reading 0 bits.
                                 */
                                const uint extraBits = 30 - 25;
                                offset =
                                    ofBase
                                    + (
                                        BIT_readBitsFast(
                                            seqState_DStream_bitContainer,
                                            ref seqState_DStream_bitsConsumed,
                                            ofBits - extraBits
                                        ) << (int)extraBits
                                    );
                                BIT_reloadDStream(
                                    ref seqState_DStream_bitContainer,
                                    ref seqState_DStream_bitsConsumed,
                                    ref seqState_DStream_ptr,
                                    seqState_DStream_start,
                                    seqState_DStream_limitPtr
                                );
                                offset += BIT_readBitsFast(
                                    seqState_DStream_bitContainer,
                                    ref seqState_DStream_bitsConsumed,
                                    extraBits
                                );
                            }
                            else
                            {
                                offset =
                                    ofBase
                                    + BIT_readBitsFast(
                                        seqState_DStream_bitContainer,
                                        ref seqState_DStream_bitsConsumed,
                                        ofBits
                                    );
                                if (MEM_32bits)
                                    BIT_reloadDStream(
                                        ref seqState_DStream_bitContainer,
                                        ref seqState_DStream_bitsConsumed,
                                        ref seqState_DStream_ptr,
                                        seqState_DStream_start,
                                        seqState_DStream_limitPtr
                                    );
                            }

                            seqState.prevOffset.e2 = seqState.prevOffset.e1;
                            seqState.prevOffset.e1 = seqState.prevOffset.e0;
                            seqState.prevOffset.e0 = offset;
                        }
                        else
                        {
                            uint ll0 = llDInfo->baseValue == 0 ? 1U : 0U;
                            if (ofBits == 0)
                            {
                                offset = System.Runtime.CompilerServices.Unsafe.Add(
                                    ref seqState.prevOffset.e0,
                                    (int)ll0
                                );
                                seqState.prevOffset.e1 = System.Runtime.CompilerServices.Unsafe.Add(
                                    ref seqState.prevOffset.e0,
                                    ll0 == 0 ? 1 : 0
                                );
                                seqState.prevOffset.e0 = offset;
                            }
                            else
                            {
                                offset =
                                    ofBase
                                    + ll0
                                    + BIT_readBitsFast(
                                        seqState_DStream_bitContainer,
                                        ref seqState_DStream_bitsConsumed,
                                        1
                                    );
                                {
                                    nuint temp =
                                        offset == 3
                                            ? seqState.prevOffset.e0 - 1
                                            : System.Runtime.CompilerServices.Unsafe.Add(
                                                ref seqState.prevOffset.e0,
                                                (int)offset
                                            );
                                    temp -= temp == 0 ? 1U : 0U;
                                    if (offset != 1)
                                        seqState.prevOffset.e2 = seqState.prevOffset.e1;
                                    seqState.prevOffset.e1 = seqState.prevOffset.e0;
                                    seqState.prevOffset.e0 = offset = temp;
                                }
                            }
                        }

                        sequence_offset = offset;
                    }

                    if (mlBits > 0)
                        sequence_matchLength += BIT_readBitsFast(
                            seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            mlBits
                        );
                    if (MEM_32bits && mlBits + llBits >= 25 - (30 - 25))
                        BIT_reloadDStream(
                            ref seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            ref seqState_DStream_ptr,
                            seqState_DStream_start,
                            seqState_DStream_limitPtr
                        );
                    if (MEM_64bits && totalBits >= 57 - (9 + 9 + 8))
                        BIT_reloadDStream(
                            ref seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            ref seqState_DStream_ptr,
                            seqState_DStream_start,
                            seqState_DStream_limitPtr
                        );
                    if (llBits > 0)
                        sequence_litLength += BIT_readBitsFast(
                            seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            llBits
                        );
                    if (MEM_32bits)
                        BIT_reloadDStream(
                            ref seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            ref seqState_DStream_ptr,
                            seqState_DStream_start,
                            seqState_DStream_limitPtr
                        );
                    if ((nbSeq == 1 ? 1 : 0) == 0)
                    {
                        ZSTD_updateFseStateWithDInfo(
                            ref seqState.stateLL,
                            seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            llNext,
                            llnbBits
                        );
                        ZSTD_updateFseStateWithDInfo(
                            ref seqState.stateML,
                            seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            mlNext,
                            mlnbBits
                        );
                        if (MEM_32bits)
                            BIT_reloadDStream(
                                ref seqState_DStream_bitContainer,
                                ref seqState_DStream_bitsConsumed,
                                ref seqState_DStream_ptr,
                                seqState_DStream_start,
                                seqState_DStream_limitPtr
                            );
                        ZSTD_updateFseStateWithDInfo(
                            ref seqState.stateOffb,
                            seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            ofNext,
                            ofnbBits
                        );
                        BIT_reloadDStream(
                            ref seqState_DStream_bitContainer,
                            ref seqState_DStream_bitsConsumed,
                            ref seqState_DStream_ptr,
                            seqState_DStream_start,
                            seqState_DStream_limitPtr
                        );
                    }
                }

                nuint oneSeqSize;
                {
                    byte* oLitEnd = op + sequence_litLength;
                    oneSeqSize = sequence_litLength + sequence_matchLength;
                    /* risk : address space overflow (32-bits) */
                    byte* oMatchEnd = op + oneSeqSize;
                    /* risk : address space underflow on oend=NULL */
                    byte* oend_w = oend - 32;
                    byte* iLitEnd = litPtr + sequence_litLength;
                    byte* match = oLitEnd - sequence_offset;
                    assert(op != null);
                    assert(oend_w < oend);
                    if (
                        iLitEnd > litEnd
                        || oMatchEnd > oend_w
                        || MEM_32bits && (nuint)(oend - op) < oneSeqSize + 32
                    )
                    {
                        oneSeqSize = ZSTD_execSequenceEnd(
                            op,
                            oend,
                            new seq_t
                            {
                                litLength = sequence_litLength,
                                matchLength = sequence_matchLength,
                                offset = sequence_offset,
                            },
                            &litPtr,
                            litEnd,
                            prefixStart,
                            vBase,
                            dictEnd
                        );
                        goto returnOneSeqSize;
                    }

                    assert(op <= oLitEnd);
                    assert(oLitEnd < oMatchEnd);
                    assert(oMatchEnd <= oend);
                    assert(iLitEnd <= litEnd);
                    assert(oLitEnd <= oend_w);
                    assert(oMatchEnd <= oend_w);
                    assert(32 >= 16);
                    ZSTD_copy16(op, litPtr);
                    if (sequence_litLength > 16)
                    {
                        ZSTD_wildcopy(
                            op + 16,
                            litPtr + 16,
                            (nint)(sequence_litLength - 16),
                            ZSTD_overlap_e.ZSTD_no_overlap
                        );
                    }

                    byte* opInner = oLitEnd;
                    litPtr = iLitEnd;
                    if (sequence_offset > (nuint)(oLitEnd - prefixStart))
                    {
                        if (sequence_offset > (nuint)(oLitEnd - vBase))
                        {
                            oneSeqSize = unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                            );
                            goto returnOneSeqSize;
                        }

                        match = dictEnd + (match - prefixStart);
                        if (match + sequence_matchLength <= dictEnd)
                        {
                            memmove(oLitEnd, match, sequence_matchLength);
                            goto returnOneSeqSize;
                        }

                        {
                            nuint length1 = (nuint)(dictEnd - match);
                            memmove(oLitEnd, match, length1);
                            opInner = oLitEnd + length1;
                            sequence_matchLength -= length1;
                            match = prefixStart;
                        }
                    }

                    assert(opInner <= oMatchEnd);
                    assert(oMatchEnd <= oend_w);
                    assert(match >= prefixStart);
                    assert(sequence_matchLength >= 1);
                    if (sequence_offset >= 16)
                    {
                        ZSTD_wildcopy(
                            opInner,
                            match,
                            (nint)sequence_matchLength,
                            ZSTD_overlap_e.ZSTD_no_overlap
                        );
                        goto returnOneSeqSize;
                    }

                    assert(sequence_offset < 16);
                    ZSTD_overlapCopy8(ref opInner, ref match, sequence_offset);
                    if (sequence_matchLength > 8)
                    {
                        assert(opInner < oMatchEnd);
                        ZSTD_wildcopy(
                            opInner,
                            match,
                            (nint)sequence_matchLength - 8,
                            ZSTD_overlap_e.ZSTD_overlap_src_before_dst
                        );
                    }

                    returnOneSeqSize:
                    ;
                }

                if (ERR_isError(oneSeqSize))
                    return oneSeqSize;
                op += oneSeqSize;
            }

            assert(nbSeq == 0);
            if (
                BIT_endOfDStream(
                    seqState_DStream_bitsConsumed,
                    seqState_DStream_ptr,
                    seqState_DStream_start
                ) == 0
            )
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            {
                uint i;
                for (i = 0; i < 3; i++)
                    dctx->entropy.rep[i] = (uint)
                        System.Runtime.CompilerServices.Unsafe.Add(
                            ref seqState.prevOffset.e0,
                            (int)i
                        );
            }
        }

        {
            nuint lastLLSize = (nuint)(litEnd - litPtr);
            if (lastLLSize > (nuint)(oend - op))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (op != null)
            {
                memcpy(op, litPtr, (uint)lastLLSize);
                op += lastLLSize;
            }
        }

        return (nuint)(op - ostart);
    }

    private static nuint ZSTD_decompressSequences_default(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        return ZSTD_decompressSequences_body(
            dctx,
            dst,
            maxDstSize,
            seqStart,
            seqSize,
            nbSeq,
            isLongOffset
        );
    }

    private static nuint ZSTD_decompressSequencesSplitLitBuffer_default(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        return ZSTD_decompressSequences_bodySplitLitBuffer(
            dctx,
            dst,
            maxDstSize,
            seqStart,
            seqSize,
            nbSeq,
            isLongOffset
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_prefetchMatch(
        nuint prefetchPos,
        seq_t sequence,
        byte* prefixStart,
        byte* dictEnd
    )
    {
        prefetchPos += sequence.litLength;
        {
            byte* matchBase = sequence.offset > prefetchPos ? dictEnd : prefixStart;
            /* note : this operation can overflow when seq.offset is really too large, which can only happen when input is corrupted.
             * No consequence though : memory address is only used for prefetching, not for dereferencing */
            byte* match = ZSTD_wrappedPtrSub(
                ZSTD_wrappedPtrAdd(matchBase, (nint)prefetchPos),
                (nint)sequence.offset
            );
#if NETCOREAPP3_0_OR_GREATER
            if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
            {
                System.Runtime.Intrinsics.X86.Sse.Prefetch0(match);
                System.Runtime.Intrinsics.X86.Sse.Prefetch0(match + 64);
            }
#endif
        }

        return prefetchPos + sequence.matchLength;
    }

    /* This decoding function employs prefetching
     * to reduce latency impact of cache misses.
     * It's generally employed when block contains a significant portion of long-distance matches
     * or when coupled with a "cold" dictionary */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_decompressSequencesLong_body(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        byte* ip = (byte*)seqStart;
        byte* iend = ip + seqSize;
        byte* ostart = (byte*)dst;
        byte* oend =
            dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_in_dst
                ? dctx->litBuffer
                : ZSTD_maybeNullPtrAdd(ostart, (nint)maxDstSize);
        byte* op = ostart;
        byte* litPtr = dctx->litPtr;
        byte* litBufferEnd = dctx->litBufferEnd;
        byte* prefixStart = (byte*)dctx->prefixStart;
        byte* dictStart = (byte*)dctx->virtualStart;
        byte* dictEnd = (byte*)dctx->dictEnd;
        if (nbSeq != 0)
        {
            seq_t* sequences = stackalloc seq_t[8];
            int seqAdvance = nbSeq < 8 ? nbSeq : 8;
            seqState_t seqState;
            int seqNb;
            /* track position relative to prefixStart */
            nuint prefetchPos = (nuint)(op - prefixStart);
            dctx->fseEntropy = 1;
            {
                int i;
                for (i = 0; i < 3; i++)
                    (&seqState.prevOffset.e0)[i] = dctx->entropy.rep[i];
            }

            assert(dst != null);
            assert(iend >= ip);
            if (ERR_isError(BIT_initDStream(&seqState.DStream, ip, (nuint)(iend - ip))))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            ZSTD_initFseState(&seqState.stateLL, &seqState.DStream, dctx->LLTptr);
            ZSTD_initFseState(&seqState.stateOffb, &seqState.DStream, dctx->OFTptr);
            ZSTD_initFseState(&seqState.stateML, &seqState.DStream, dctx->MLTptr);
            for (seqNb = 0; seqNb < seqAdvance; seqNb++)
            {
                seq_t sequence = ZSTD_decodeSequence(
                    &seqState,
                    isLongOffset,
                    seqNb == nbSeq - 1 ? 1 : 0
                );
                prefetchPos = ZSTD_prefetchMatch(prefetchPos, sequence, prefixStart, dictEnd);
                sequences[seqNb] = sequence;
            }

            for (; seqNb < nbSeq; seqNb++)
            {
                seq_t sequence = ZSTD_decodeSequence(
                    &seqState,
                    isLongOffset,
                    seqNb == nbSeq - 1 ? 1 : 0
                );
                if (
                    dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split
                    && litPtr + sequences[seqNb - 8 & 8 - 1].litLength > dctx->litBufferEnd
                )
                {
                    /* lit buffer is reaching split point, empty out the first buffer and transition to litExtraBuffer */
                    nuint leftoverLit = (nuint)(dctx->litBufferEnd - litPtr);
                    if (leftoverLit != 0)
                    {
                        if (leftoverLit > (nuint)(oend - op))
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)
                            );
                        }

                        ZSTD_safecopyDstBeforeSrc(op, litPtr, (nint)leftoverLit);
                        sequences[seqNb - 8 & 8 - 1].litLength -= leftoverLit;
                        op += leftoverLit;
                    }

                    litPtr = dctx->litExtraBuffer;
                    litBufferEnd = dctx->litExtraBuffer + (1 << 16);
                    dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_not_in_dst;
                    {
                        nuint oneSeqSize = ZSTD_execSequence(
                            op,
                            oend,
                            sequences[seqNb - 8 & 8 - 1],
                            &litPtr,
                            litBufferEnd,
                            prefixStart,
                            dictStart,
                            dictEnd
                        );
                        if (ERR_isError(oneSeqSize))
                            return oneSeqSize;
                        prefetchPos = ZSTD_prefetchMatch(
                            prefetchPos,
                            sequence,
                            prefixStart,
                            dictEnd
                        );
                        sequences[seqNb & 8 - 1] = sequence;
                        op += oneSeqSize;
                    }
                }
                else
                {
                    /* lit buffer is either wholly contained in first or second split, or not split at all*/
                    nuint oneSeqSize =
                        dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split
                            ? ZSTD_execSequenceSplitLitBuffer(
                                op,
                                oend,
                                litPtr + sequences[seqNb - 8 & 8 - 1].litLength - 32,
                                sequences[seqNb - 8 & 8 - 1],
                                &litPtr,
                                litBufferEnd,
                                prefixStart,
                                dictStart,
                                dictEnd
                            )
                            : ZSTD_execSequence(
                                op,
                                oend,
                                sequences[seqNb - 8 & 8 - 1],
                                &litPtr,
                                litBufferEnd,
                                prefixStart,
                                dictStart,
                                dictEnd
                            );
                    if (ERR_isError(oneSeqSize))
                        return oneSeqSize;
                    prefetchPos = ZSTD_prefetchMatch(prefetchPos, sequence, prefixStart, dictEnd);
                    sequences[seqNb & 8 - 1] = sequence;
                    op += oneSeqSize;
                }
            }

            if (BIT_endOfDStream(&seqState.DStream) == 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            seqNb -= seqAdvance;
            for (; seqNb < nbSeq; seqNb++)
            {
                seq_t* sequence = &sequences[seqNb & 8 - 1];
                if (
                    dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split
                    && litPtr + sequence->litLength > dctx->litBufferEnd
                )
                {
                    nuint leftoverLit = (nuint)(dctx->litBufferEnd - litPtr);
                    if (leftoverLit != 0)
                    {
                        if (leftoverLit > (nuint)(oend - op))
                        {
                            return unchecked(
                                (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)
                            );
                        }

                        ZSTD_safecopyDstBeforeSrc(op, litPtr, (nint)leftoverLit);
                        sequence->litLength -= leftoverLit;
                        op += leftoverLit;
                    }

                    litPtr = dctx->litExtraBuffer;
                    litBufferEnd = dctx->litExtraBuffer + (1 << 16);
                    dctx->litBufferLocation = ZSTD_litLocation_e.ZSTD_not_in_dst;
                    {
                        nuint oneSeqSize = ZSTD_execSequence(
                            op,
                            oend,
                            *sequence,
                            &litPtr,
                            litBufferEnd,
                            prefixStart,
                            dictStart,
                            dictEnd
                        );
                        if (ERR_isError(oneSeqSize))
                            return oneSeqSize;
                        op += oneSeqSize;
                    }
                }
                else
                {
                    nuint oneSeqSize =
                        dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split
                            ? ZSTD_execSequenceSplitLitBuffer(
                                op,
                                oend,
                                litPtr + sequence->litLength - 32,
                                *sequence,
                                &litPtr,
                                litBufferEnd,
                                prefixStart,
                                dictStart,
                                dictEnd
                            )
                            : ZSTD_execSequence(
                                op,
                                oend,
                                *sequence,
                                &litPtr,
                                litBufferEnd,
                                prefixStart,
                                dictStart,
                                dictEnd
                            );
                    if (ERR_isError(oneSeqSize))
                        return oneSeqSize;
                    op += oneSeqSize;
                }
            }

            {
                uint i;
                for (i = 0; i < 3; i++)
                    dctx->entropy.rep[i] = (uint)(&seqState.prevOffset.e0)[i];
            }
        }

        if (dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split)
        {
            nuint lastLLSize = (nuint)(litBufferEnd - litPtr);
            if (lastLLSize > (nuint)(oend - op))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (op != null)
            {
                memmove(op, litPtr, lastLLSize);
                op += lastLLSize;
            }

            litPtr = dctx->litExtraBuffer;
            litBufferEnd = dctx->litExtraBuffer + (1 << 16);
        }

        {
            nuint lastLLSize = (nuint)(litBufferEnd - litPtr);
            if (lastLLSize > (nuint)(oend - op))
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (op != null)
            {
                memmove(op, litPtr, lastLLSize);
                op += lastLLSize;
            }
        }

        return (nuint)(op - ostart);
    }

    private static nuint ZSTD_decompressSequencesLong_default(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        return ZSTD_decompressSequencesLong_body(
            dctx,
            dst,
            maxDstSize,
            seqStart,
            seqSize,
            nbSeq,
            isLongOffset
        );
    }

    private static nuint ZSTD_decompressSequences(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        return ZSTD_decompressSequences_default(
            dctx,
            dst,
            maxDstSize,
            seqStart,
            seqSize,
            nbSeq,
            isLongOffset
        );
    }

    private static nuint ZSTD_decompressSequencesSplitLitBuffer(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        return ZSTD_decompressSequencesSplitLitBuffer_default(
            dctx,
            dst,
            maxDstSize,
            seqStart,
            seqSize,
            nbSeq,
            isLongOffset
        );
    }

    /* ZSTD_decompressSequencesLong() :
     * decompression function triggered when a minimum share of offsets is considered "long",
     * aka out of cache.
     * note : "long" definition seems overloaded here, sometimes meaning "wider than bitstream register", and sometimes meaning "farther than memory cache distance".
     * This function will try to mitigate main memory latency through the use of prefetching */
    private static nuint ZSTD_decompressSequencesLong(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint maxDstSize,
        void* seqStart,
        nuint seqSize,
        int nbSeq,
        ZSTD_longOffset_e isLongOffset
    )
    {
        return ZSTD_decompressSequencesLong_default(
            dctx,
            dst,
            maxDstSize,
            seqStart,
            seqSize,
            nbSeq,
            isLongOffset
        );
    }

    /**
     * @returns The total size of the history referenceable by zstd, including
     * both the prefix and the extDict. At @p op any offset larger than this
     * is invalid.
     */
    private static nuint ZSTD_totalHistorySize(byte* op, byte* virtualStart)
    {
        return (nuint)(op - virtualStart);
    }

    /* ZSTD_getOffsetInfo() :
     * condition : offTable must be valid
     * @return : "share" of long offsets (arbitrarily defined as > (1<<23))
     *           compared to maximum possible of (1<<OffFSELog),
     *           as well as the maximum number additional bits required.
     */
    private static ZSTD_OffsetInfo ZSTD_getOffsetInfo(ZSTD_seqSymbol* offTable, int nbSeq)
    {
        ZSTD_OffsetInfo info = new ZSTD_OffsetInfo { longOffsetShare = 0, maxNbAdditionalBits = 0 };
        if (nbSeq != 0)
        {
            void* ptr = offTable;
            uint tableLog = ((ZSTD_seqSymbol_header*)ptr)[0].tableLog;
            ZSTD_seqSymbol* table = offTable + 1;
            uint max = (uint)(1 << (int)tableLog);
            uint u;
            assert(max <= 1 << 8);
            for (u = 0; u < max; u++)
            {
                info.maxNbAdditionalBits =
                    info.maxNbAdditionalBits > table[u].nbAdditionalBits
                        ? info.maxNbAdditionalBits
                        : table[u].nbAdditionalBits;
                if (table[u].nbAdditionalBits > 22)
                    info.longOffsetShare += 1;
            }

            assert(tableLog <= 8);
            info.longOffsetShare <<= (int)(8 - tableLog);
        }

        return info;
    }

    /**
     * @returns The maximum offset we can decode in one read of our bitstream, without
     * reloading more bits in the middle of the offset bits read. Any offsets larger
     * than this must use the long offset decoder.
     */
    private static nuint ZSTD_maxShortOffset()
    {
        if (MEM_64bits)
        {
            return unchecked((nuint)(-1));
        }
        else
        {
            /* The maximum offBase is (1 << (STREAM_ACCUMULATOR_MIN + 1)) - 1.
             * This offBase would require STREAM_ACCUMULATOR_MIN extra bits.
             * Then we have to subtract ZSTD_REP_NUM to get the maximum possible offset.
             */
            nuint maxOffbase = ((nuint)1 << (int)((uint)(MEM_32bits ? 25 : 57) + 1)) - 1;
            nuint maxOffset = maxOffbase - 3;
            assert(ZSTD_highbit32((uint)maxOffbase) == (uint)(MEM_32bits ? 25 : 57));
            return maxOffset;
        }
    }

    /* ZSTD_decompressBlock_internal() :
     * decompress block, starting at `src`,
     * into destination buffer `dst`.
     * @return : decompressed block size,
     *           or an error code (which can be tested using ZSTD_isError())
     */
    private static nuint ZSTD_decompressBlock_internal(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        streaming_operation streaming
    )
    {
        byte* ip = (byte*)src;
        if (srcSize > ZSTD_blockSizeMax(dctx))
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        }

        {
            nuint litCSize = ZSTD_decodeLiteralsBlock(
                dctx,
                src,
                srcSize,
                dst,
                dstCapacity,
                streaming
            );
            if (ERR_isError(litCSize))
                return litCSize;
            ip += litCSize;
            srcSize -= litCSize;
        }

        {
            /* Compute the maximum block size, which must also work when !frame and fParams are unset.
             * Additionally, take the min with dstCapacity to ensure that the totalHistorySize fits in a size_t.
             */
            nuint blockSizeMax =
                dstCapacity < ZSTD_blockSizeMax(dctx) ? dstCapacity : ZSTD_blockSizeMax(dctx);
            nuint totalHistorySize = ZSTD_totalHistorySize(
                ZSTD_maybeNullPtrAdd((byte*)dst, (nint)blockSizeMax),
                (byte*)dctx->virtualStart
            );
            /* isLongOffset must be true if there are long offsets.
             * Offsets are long if they are larger than ZSTD_maxShortOffset().
             * We don't expect that to be the case in 64-bit mode.
             *
             * We check here to see if our history is large enough to allow long offsets.
             * If it isn't, then we can't possible have (valid) long offsets. If the offset
             * is invalid, then it is okay to read it incorrectly.
             *
             * If isLongOffsets is true, then we will later check our decoding table to see
             * if it is even possible to generate long offsets.
             */
            ZSTD_longOffset_e isLongOffset = (ZSTD_longOffset_e)(
                MEM_32bits && totalHistorySize > ZSTD_maxShortOffset() ? 1 : 0
            );
            int usePrefetchDecoder = dctx->ddictIsCold;
            int nbSeq;
            nuint seqHSize = ZSTD_decodeSeqHeaders(dctx, &nbSeq, ip, srcSize);
            if (ERR_isError(seqHSize))
                return seqHSize;
            ip += seqHSize;
            srcSize -= seqHSize;
            if ((dst == null || dstCapacity == 0) && nbSeq > 0)
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (
                MEM_64bits
                && sizeof(nuint) == sizeof(void*)
                && unchecked((nuint)(-1)) - (nuint)dst < 1 << 20
            )
            {
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            }

            if (
                isLongOffset != default
                || usePrefetchDecoder == 0 && totalHistorySize > 1U << 24 && nbSeq > 8
            )
            {
                ZSTD_OffsetInfo info = ZSTD_getOffsetInfo(dctx->OFTptr, nbSeq);
                if (
                    isLongOffset != default
                    && info.maxNbAdditionalBits <= (uint)(MEM_32bits ? 25 : 57)
                )
                {
                    isLongOffset = ZSTD_longOffset_e.ZSTD_lo_isRegularOffset;
                }

                if (usePrefetchDecoder == 0)
                {
                    /* heuristic values, correspond to 2.73% and 7.81% */
                    uint minShare = (uint)(MEM_64bits ? 7 : 20);
                    usePrefetchDecoder = info.longOffsetShare >= minShare ? 1 : 0;
                }
            }

            dctx->ddictIsCold = 0;
            if (usePrefetchDecoder != 0)
            {
                return ZSTD_decompressSequencesLong(
                    dctx,
                    dst,
                    dstCapacity,
                    ip,
                    srcSize,
                    nbSeq,
                    isLongOffset
                );
            }

            if (dctx->litBufferLocation == ZSTD_litLocation_e.ZSTD_split)
                return ZSTD_decompressSequencesSplitLitBuffer(
                    dctx,
                    dst,
                    dstCapacity,
                    ip,
                    srcSize,
                    nbSeq,
                    isLongOffset
                );
            else
                return ZSTD_decompressSequences(
                    dctx,
                    dst,
                    dstCapacity,
                    ip,
                    srcSize,
                    nbSeq,
                    isLongOffset
                );
        }
    }

    /*! ZSTD_checkContinuity() :
     *  check if next `dst` follows previous position, where decompression ended.
     *  If yes, do nothing (continue on current segment).
     *  If not, classify previous segment as "external dictionary", and start a new segment.
     *  This function cannot fail. */
    private static void ZSTD_checkContinuity(ZSTD_DCtx_s* dctx, void* dst, nuint dstSize)
    {
        if (dst != dctx->previousDstEnd && dstSize > 0)
        {
            dctx->dictEnd = dctx->previousDstEnd;
            dctx->virtualStart =
                (sbyte*)dst - ((sbyte*)dctx->previousDstEnd - (sbyte*)dctx->prefixStart);
            dctx->prefixStart = dst;
            dctx->previousDstEnd = dst;
        }
    }

    /* Internal definition of ZSTD_decompressBlock() to avoid deprecation warnings. */
    private static nuint ZSTD_decompressBlock_deprecated(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        nuint dSize;
        dctx->isFrameDecompression = 0;
        ZSTD_checkContinuity(dctx, dst, dstCapacity);
        dSize = ZSTD_decompressBlock_internal(
            dctx,
            dst,
            dstCapacity,
            src,
            srcSize,
            streaming_operation.not_streaming
        );
        {
            nuint err_code = dSize;
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        dctx->previousDstEnd = (sbyte*)dst + dSize;
        return dSize;
    }

    /* NOTE: Must just wrap ZSTD_decompressBlock_deprecated() */
    public static nuint ZSTD_decompressBlock(
        ZSTD_DCtx_s* dctx,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        return ZSTD_decompressBlock_deprecated(dctx, dst, dstCapacity, src, srcSize);
    }

    private static void ZSTD_initFseState(
        ref ZSTD_fseState DStatePtr,
        ref BIT_DStream_t bitD,
        ZSTD_seqSymbol* dt
    )
    {
        void* ptr = dt;
        ZSTD_seqSymbol_header* DTableH = (ZSTD_seqSymbol_header*)ptr;
        DStatePtr.state = BIT_readBits(bitD.bitContainer, ref bitD.bitsConsumed, DTableH->tableLog);
        BIT_reloadDStream(
            ref bitD.bitContainer,
            ref bitD.bitsConsumed,
            ref bitD.ptr,
            bitD.start,
            bitD.limitPtr
        );
        DStatePtr.table = dt + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_updateFseStateWithDInfo(
        ref ZSTD_fseState DStatePtr,
        nuint bitD_bitContainer,
        ref uint bitD_bitsConsumed,
        ushort nextState,
        uint nbBits
    )
    {
        nuint lowBits = BIT_readBits(bitD_bitContainer, ref bitD_bitsConsumed, nbBits);
        DStatePtr.state = nextState + lowBits;
    }

    /*! ZSTD_overlapCopy8() :
     *  Copies 8 bytes from ip to op and updates op and ip where ip <= op.
     *  If the offset is < 8 then the offset is spread to at least 8 bytes.
     *
     *  Precondition: *ip <= *op
     *  Postcondition: *op - *op >= 8
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_overlapCopy8(ref byte* op, ref byte* ip, nuint offset)
    {
        assert(ip <= op);
        if (offset < 8)
        {
            int sub2 = dec64table[offset];
            op[0] = ip[0];
            op[1] = ip[1];
            op[2] = ip[2];
            op[3] = ip[3];
            ip += dec32table[offset];
            ZSTD_copy4(op + 4, ip);
            ip -= sub2;
        }
        else
        {
            ZSTD_copy8(op, ip);
        }

        ip += 8;
        op += 8;
        assert(op - ip >= 8);
    }
}
