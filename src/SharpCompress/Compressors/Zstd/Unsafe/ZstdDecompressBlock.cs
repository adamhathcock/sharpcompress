using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /*_*******************************************************
        *  Memory operations
        **********************************************************/
        private static void ZSTD_copy4(void* dst, void* src)
        {
            memcpy((dst), (src), (4));
        }

        /*! ZSTD_getcBlockSize() :
         *  Provides the size of compressed block from block header `src` */
        public static nuint ZSTD_getcBlockSize(void* src, nuint srcSize, blockProperties_t* bpPtr)
        {
            if (srcSize < ZSTD_blockHeaderSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }


            {
                uint cBlockHeader = MEM_readLE24(src);
                uint cSize = cBlockHeader >> 3;

                bpPtr->lastBlock = cBlockHeader & 1;
                bpPtr->blockType = (blockType_e)((cBlockHeader >> 1) & 3);
                bpPtr->origSize = cSize;
                if (bpPtr->blockType == blockType_e.bt_rle)
                {
                    return 1;
                }

                if (bpPtr->blockType == blockType_e.bt_reserved)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                return cSize;
            }
        }

        /*! ZSTD_decodeLiteralsBlock() :
         * @return : nb of bytes read from src (< srcSize )
         *  note : symbol not declared but exposed for fullbench */
        public static nuint ZSTD_decodeLiteralsBlock(ZSTD_DCtx_s* dctx, void* src, nuint srcSize)
        {
            if (srcSize < (uint)((1 + 1 + 1)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }


            {
                byte* istart = (byte*)(src);
                symbolEncodingType_e litEncType = (symbolEncodingType_e)(istart[0] & 3);

                switch (litEncType)
                {
                    case symbolEncodingType_e.set_repeat:
        ;
                    if (dctx->litEntropy == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dictionary_corrupted)));
                    }


                    goto case symbolEncodingType_e.set_compressed;
                    case symbolEncodingType_e.set_compressed:
                    {
                        if (srcSize < 5)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }
                    }


                    {
                        nuint lhSize, litSize, litCSize;
                        uint singleStream = 0;
                        uint lhlCode = (uint)((istart[0] >> 2) & 3);
                        uint lhc = MEM_readLE32((void*)istart);
                        nuint hufSuccess;

                        switch (lhlCode)
                        {
                            case 0:
                            case 1:
                            default:
                            {
                                singleStream = (lhlCode == 0 ? 1U : 0U);
                            }

                            lhSize = 3;
                            litSize = (lhc >> 4) & 0x3FF;
                            litCSize = (lhc >> 14) & 0x3FF;
                            break;
                            case 2:
                            {
                                lhSize = 4;
                            }

                            litSize = (lhc >> 4) & 0x3FFF;
                            litCSize = lhc >> 18;
                            break;
                            case 3:
                            {
                                lhSize = 5;
                            }

                            litSize = (lhc >> 4) & 0x3FFFF;
                            litCSize = (lhc >> 22) + ((nuint)(istart[4]) << 10);
                            break;
                        }

                        if (litSize > (uint)((1 << 17)))
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }

                        if (litCSize + lhSize > srcSize)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }

                        if (dctx->ddictIsCold != 0 && (litSize > 768))
                        {

                            {
                                sbyte* _ptr = (sbyte*)(dctx->HUFptr);
                                nuint _size = (nuint)((nuint)(16388));
                                nuint _pos;

                                for (_pos = 0; _pos < _size; _pos += 64)
                                {
                                    Prefetch1((void*)(_ptr + _pos));
                                }
                            }

                        }

                        if (litEncType == symbolEncodingType_e.set_repeat)
                        {
                            if (singleStream != 0)
                            {
                                hufSuccess = HUF_decompress1X_usingDTable_bmi2((void*)dctx->litBuffer, litSize, (void*)(istart + lhSize), litCSize, dctx->HUFptr, dctx->bmi2);
                            }
                            else
                            {
                                hufSuccess = HUF_decompress4X_usingDTable_bmi2((void*)dctx->litBuffer, litSize, (void*)(istart + lhSize), litCSize, dctx->HUFptr, dctx->bmi2);
                            }
                        }
                        else
                        {
                            if (singleStream != 0)
                            {
                                hufSuccess = HUF_decompress1X1_DCtx_wksp_bmi2((uint*)dctx->entropy.hufTable, (void*)dctx->litBuffer, litSize, (void*)(istart + lhSize), litCSize, (void*)dctx->workspace, (nuint)(sizeof(uint) * 640), dctx->bmi2);
                            }
                            else
                            {
                                hufSuccess = HUF_decompress4X_hufOnly_wksp_bmi2((uint*)dctx->entropy.hufTable, (void*)dctx->litBuffer, litSize, (void*)(istart + lhSize), litCSize, (void*)dctx->workspace, (nuint)(sizeof(uint) * 640), dctx->bmi2);
                            }
                        }

                        if ((ERR_isError(hufSuccess)) != 0)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }

                        dctx->litPtr = dctx->litBuffer;
                        dctx->litSize = litSize;
                        dctx->litEntropy = 1;
                        if (litEncType == symbolEncodingType_e.set_compressed)
                        {
                            dctx->HUFptr = dctx->entropy.hufTable;
                        }

                        memset((void*)((dctx->litBuffer + dctx->litSize)), (0), (32));
                        return litCSize + lhSize;
                    }

                    case symbolEncodingType_e.set_basic:
                    {
                        nuint litSize, lhSize;
                        uint lhlCode = (uint)(((istart[0]) >> 2) & 3);

                        switch (lhlCode)
                        {
                            case 0:
                            case 2:
                            default:
                            {
                                lhSize = 1;
                            }

                            litSize = (nuint)(istart[0] >> 3);
                            break;
                            case 1:
                            {
                                lhSize = 2;
                            }

                            litSize = (nuint)(MEM_readLE16((void*)istart) >> 4);
                            break;
                            case 3:
                            {
                                lhSize = 3;
                            }

                            litSize = MEM_readLE24((void*)istart) >> 4;
                            break;
                        }

                        if (lhSize + litSize + 32 > srcSize)
                        {
                            if (litSize + lhSize > srcSize)
                            {
                                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                            }

                            memcpy((void*)(dctx->litBuffer), (void*)((istart + lhSize)), (litSize));
                            dctx->litPtr = dctx->litBuffer;
                            dctx->litSize = litSize;
                            memset((void*)((dctx->litBuffer + dctx->litSize)), (0), (32));
                            return lhSize + litSize;
                        }

                        dctx->litPtr = istart + lhSize;
                        dctx->litSize = litSize;
                        return lhSize + litSize;
                    }

                    case symbolEncodingType_e.set_rle:
                    {
                        uint lhlCode = (uint)(((istart[0]) >> 2) & 3);
                        nuint litSize, lhSize;

                        switch (lhlCode)
                        {
                            case 0:
                            case 2:
                            default:
                            {
                                lhSize = 1;
                            }

                            litSize = (nuint)(istart[0] >> 3);
                            break;
                            case 1:
                            {
                                lhSize = 2;
                            }

                            litSize = (nuint)(MEM_readLE16((void*)istart) >> 4);
                            break;
                            case 3:
                            {
                                lhSize = 3;
                            }

                            litSize = MEM_readLE24((void*)istart) >> 4;
                            if (srcSize < 4)
                            {
                                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                            }

                            break;
                        }

                        if (litSize > (uint)((1 << 17)))
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }

                        memset((void*)(dctx->litBuffer), (int)((istart[lhSize])), (litSize + 32));
                        dctx->litPtr = dctx->litBuffer;
                        dctx->litSize = litSize;
                        return lhSize + 1;
                    }

                    default:
                    {

                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                        }
                    }

                }
            }
        }

        public static ZSTD_seqSymbol* LL_defaultDTable = GetArrayPointer(new ZSTD_seqSymbol[65]
        {
            new ZSTD_seqSymbol
            {
                nextState = 1,
                nbAdditionalBits = 1,
                nbBits = 1,
                baseValue = 6,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 0,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 0,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 1,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 3,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 4,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 6,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 7,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 9,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 10,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 12,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 14,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 16,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 20,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 22,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 2,
                nbBits = 5,
                baseValue = 28,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 3,
                nbBits = 5,
                baseValue = 32,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 4,
                nbBits = 5,
                baseValue = 48,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 6,
                nbBits = 5,
                baseValue = 64,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 7,
                nbBits = 5,
                baseValue = 128,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 8,
                nbBits = 6,
                baseValue = 256,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 10,
                nbBits = 6,
                baseValue = 1024,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 12,
                nbBits = 6,
                baseValue = 4096,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 0,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 1,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 2,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 4,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 7,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 8,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 10,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 11,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 13,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 16,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 18,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 22,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 2,
                nbBits = 5,
                baseValue = 24,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 3,
                nbBits = 5,
                baseValue = 32,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 3,
                nbBits = 5,
                baseValue = 40,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 6,
                nbBits = 4,
                baseValue = 64,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 6,
                nbBits = 4,
                baseValue = 64,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 7,
                nbBits = 5,
                baseValue = 128,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 9,
                nbBits = 6,
                baseValue = 512,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 11,
                nbBits = 6,
                baseValue = 2048,
            },
            new ZSTD_seqSymbol
            {
                nextState = 48,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 0,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 1,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 2,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 3,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 6,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 8,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 9,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 11,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 12,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 15,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 18,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 20,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 2,
                nbBits = 5,
                baseValue = 24,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 2,
                nbBits = 5,
                baseValue = 28,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 3,
                nbBits = 5,
                baseValue = 40,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 4,
                nbBits = 5,
                baseValue = 48,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 16,
                nbBits = 6,
                baseValue = 65536,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 15,
                nbBits = 6,
                baseValue = 32768,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 14,
                nbBits = 6,
                baseValue = 16384,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 13,
                nbBits = 6,
                baseValue = 8192,
            },
        });

        public static ZSTD_seqSymbol* OF_defaultDTable = GetArrayPointer(new ZSTD_seqSymbol[33]
        {
            new ZSTD_seqSymbol
            {
                nextState = 1,
                nbAdditionalBits = 1,
                nbBits = 1,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 0,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 6,
                nbBits = 4,
                baseValue = 61,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 9,
                nbBits = 5,
                baseValue = 509,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 15,
                nbBits = 5,
                baseValue = 32765,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 21,
                nbBits = 5,
                baseValue = 2097149,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 3,
                nbBits = 5,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 7,
                nbBits = 4,
                baseValue = 125,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 12,
                nbBits = 5,
                baseValue = 4093,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 18,
                nbBits = 5,
                baseValue = 262141,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 23,
                nbBits = 5,
                baseValue = 8388605,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 5,
                nbBits = 5,
                baseValue = 29,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 8,
                nbBits = 4,
                baseValue = 253,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 14,
                nbBits = 5,
                baseValue = 16381,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 20,
                nbBits = 5,
                baseValue = 1048573,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 2,
                nbBits = 5,
                baseValue = 1,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 7,
                nbBits = 4,
                baseValue = 125,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 11,
                nbBits = 5,
                baseValue = 2045,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 17,
                nbBits = 5,
                baseValue = 131069,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 22,
                nbBits = 5,
                baseValue = 4194301,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 4,
                nbBits = 5,
                baseValue = 13,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 8,
                nbBits = 4,
                baseValue = 253,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 13,
                nbBits = 5,
                baseValue = 8189,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 19,
                nbBits = 5,
                baseValue = 524285,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 5,
                baseValue = 1,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 6,
                nbBits = 4,
                baseValue = 61,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 10,
                nbBits = 5,
                baseValue = 1021,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 16,
                nbBits = 5,
                baseValue = 65533,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 28,
                nbBits = 5,
                baseValue = 268435453,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 27,
                nbBits = 5,
                baseValue = 134217725,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 26,
                nbBits = 5,
                baseValue = 67108861,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 25,
                nbBits = 5,
                baseValue = 33554429,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 24,
                nbBits = 5,
                baseValue = 16777213,
            },
        });

        public static ZSTD_seqSymbol* ML_defaultDTable = GetArrayPointer(new ZSTD_seqSymbol[65]
        {
            new ZSTD_seqSymbol
            {
                nextState = 1,
                nbAdditionalBits = 1,
                nbBits = 1,
                baseValue = 6,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 3,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 4,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 6,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 8,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 9,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 11,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 13,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 16,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 19,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 22,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 25,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 28,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 31,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 34,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 6,
                baseValue = 37,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 6,
                baseValue = 41,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 2,
                nbBits = 6,
                baseValue = 47,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 3,
                nbBits = 6,
                baseValue = 59,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 4,
                nbBits = 6,
                baseValue = 83,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 7,
                nbBits = 6,
                baseValue = 131,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 9,
                nbBits = 6,
                baseValue = 515,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 4,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 6,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 7,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 9,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 10,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 12,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 15,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 18,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 21,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 24,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 27,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 30,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 33,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 6,
                baseValue = 35,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 1,
                nbBits = 6,
                baseValue = 39,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 2,
                nbBits = 6,
                baseValue = 43,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 3,
                nbBits = 6,
                baseValue = 51,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 4,
                nbBits = 6,
                baseValue = 67,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 5,
                nbBits = 6,
                baseValue = 99,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 8,
                nbBits = 6,
                baseValue = 259,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 4,
            },
            new ZSTD_seqSymbol
            {
                nextState = 48,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 4,
            },
            new ZSTD_seqSymbol
            {
                nextState = 16,
                nbAdditionalBits = 0,
                nbBits = 4,
                baseValue = 5,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 7,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 8,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 10,
            },
            new ZSTD_seqSymbol
            {
                nextState = 32,
                nbAdditionalBits = 0,
                nbBits = 5,
                baseValue = 11,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 14,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 17,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 20,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 23,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 26,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 29,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 0,
                nbBits = 6,
                baseValue = 32,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 16,
                nbBits = 6,
                baseValue = 65539,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 15,
                nbBits = 6,
                baseValue = 32771,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 14,
                nbBits = 6,
                baseValue = 16387,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 13,
                nbBits = 6,
                baseValue = 8195,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 12,
                nbBits = 6,
                baseValue = 4099,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 11,
                nbBits = 6,
                baseValue = 2051,
            },
            new ZSTD_seqSymbol
            {
                nextState = 0,
                nbAdditionalBits = 10,
                nbBits = 6,
                baseValue = 1027,
            },
        });

        private static void ZSTD_buildSeqTable_rle(ZSTD_seqSymbol* dt, uint baseValue, uint nbAddBits)
        {
            void* ptr = (void*)dt;
            ZSTD_seqSymbol_header* DTableH = (ZSTD_seqSymbol_header*)(ptr);
            ZSTD_seqSymbol* cell = dt + 1;

            DTableH->tableLog = 0;
            DTableH->fastMode = 0;
            cell->nbBits = 0;
            cell->nextState = 0;
            assert(nbAddBits < 255);
            cell->nbAdditionalBits = (byte)(nbAddBits);
            cell->baseValue = baseValue;
        }

        /* ZSTD_buildFSETable() :
         * generate FSE decoding table for one symbol (ll, ml or off)
         * cannot fail if input is valid =>
         * all inputs are presumed validated at this stage */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_buildFSETable_body(ZSTD_seqSymbol* dt, short* normalizedCounter, uint maxSymbolValue, uint* baseValue, uint* nbAdditionalBits, uint tableLog, void* wksp, nuint wkspSize)
        {
            ZSTD_seqSymbol* tableDecode = dt + 1;
            uint maxSV1 = maxSymbolValue + 1;
            uint tableSize = (uint)(1 << (int)tableLog);
            ushort* symbolNext = (ushort*)(wksp);
            byte* spread = (byte*)(symbolNext + ((35) > (52) ? (35) : (52)) + 1);
            uint highThreshold = tableSize - 1;

            assert(maxSymbolValue <= (uint)(((35) > (52) ? (35) : (52))));
            assert(tableLog <= (uint)(((((9) > (9) ? (9) : (9))) > (8) ? (((9) > (9) ? (9) : (9))) : (8))));
            assert(wkspSize >= ((nuint)(sizeof(short)) * (uint)((((35) > (52) ? (35) : (52)) + 1)) + (1U << ((((9) > (9) ? (9) : (9))) > (8) ? (((9) > (9) ? (9) : (9))) : (8))) + (nuint)(sizeof(ulong))));

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
                            {
                                DTableH.fastMode = 0;
                            }

                            assert(normalizedCounter[s] >= 0);
                            symbolNext[s] = (ushort)(normalizedCounter[s]);
                        }
                    }
                }

                memcpy((void*)(dt), (void*)(&DTableH), ((nuint)(sizeof(ZSTD_seqSymbol_header))));
            }

            assert(tableSize <= 512);
            if (highThreshold == tableSize - 1)
            {
                nuint tableMask = tableSize - 1;
                nuint step = (((tableSize) >> 1) + ((tableSize) >> 3) + 3);


                {
                    ulong add = 0x0101010101010101UL;
                    nuint pos = 0;
                    ulong sv = 0;
                    uint s;

                    for (s = 0; s < maxSV1; ++s , sv += add)
                    {
                        int i;
                        int n = normalizedCounter[s];

                        MEM_write64((void*)(spread + pos), sv);
                        for (i = 8; i < n; i += 8)
                        {
                            MEM_write64((void*)(spread + pos + i), sv);
                        }

                        pos += (nuint)n;
                    }
                }


                {
                    nuint position = 0;
                    nuint s;
                    nuint unroll = 2;

                    assert(tableSize % unroll == 0);
                    for (s = 0; s < (nuint)(tableSize); s += unroll)
                    {
                        nuint u;

                        for (u = 0; u < unroll; ++u)
                        {
                            nuint uPosition = (position + (u * step)) & tableMask;

                            tableDecode[uPosition].baseValue = spread[s + u];
                        }

                        position = (position + (unroll * step)) & tableMask;
                    }

                    assert(position == 0);
                }
            }
            else
            {
                uint tableMask = tableSize - 1;
                uint step = (((tableSize) >> 1) + ((tableSize) >> 3) + 3);
                uint s, position = 0;

                for (s = 0; s < maxSV1; s++)
                {
                    int i;
                    int n = normalizedCounter[s];

                    for (i = 0; i < n; i++)
                    {
                        tableDecode[position].baseValue = s;
                        position = (position + step) & tableMask;
                        while (position > highThreshold)
                        {
                            position = (position + step) & tableMask;
                        }
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

                    tableDecode[u].nbBits = (byte)(tableLog - BIT_highbit32(nextState));
                    tableDecode[u].nextState = (ushort)((nextState << (int)(tableDecode[u].nbBits)) - tableSize);
                    assert(nbAdditionalBits[symbol] < 255);
                    tableDecode[u].nbAdditionalBits = (byte)(nbAdditionalBits[symbol]);
                    tableDecode[u].baseValue = baseValue[symbol];
                }
            }
        }

        /* Avoids the FORCE_INLINE of the _body() function. */
        private static void ZSTD_buildFSETable_body_default(ZSTD_seqSymbol* dt, short* normalizedCounter, uint maxSymbolValue, uint* baseValue, uint* nbAdditionalBits, uint tableLog, void* wksp, nuint wkspSize)
        {
            ZSTD_buildFSETable_body(dt, normalizedCounter, maxSymbolValue, baseValue, nbAdditionalBits, tableLog, wksp, wkspSize);
        }

        private static void ZSTD_buildFSETable_body_bmi2(ZSTD_seqSymbol* dt, short* normalizedCounter, uint maxSymbolValue, uint* baseValue, uint* nbAdditionalBits, uint tableLog, void* wksp, nuint wkspSize)
        {
            ZSTD_buildFSETable_body(dt, normalizedCounter, maxSymbolValue, baseValue, nbAdditionalBits, tableLog, wksp, wkspSize);
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
        public static void ZSTD_buildFSETable(ZSTD_seqSymbol* dt, short* normalizedCounter, uint maxSymbolValue, uint* baseValue, uint* nbAdditionalBits, uint tableLog, void* wksp, nuint wkspSize, int bmi2)
        {
            if (bmi2 != 0)
            {
                ZSTD_buildFSETable_body_bmi2(dt, normalizedCounter, maxSymbolValue, baseValue, nbAdditionalBits, tableLog, wksp, wkspSize);
                return;
            }

            ZSTD_buildFSETable_body_default(dt, normalizedCounter, maxSymbolValue, baseValue, nbAdditionalBits, tableLog, wksp, wkspSize);
        }

        /*! ZSTD_buildSeqTable() :
         * @return : nb bytes read from src,
         *           or an error code if it fails */
        private static nuint ZSTD_buildSeqTable(ZSTD_seqSymbol* DTableSpace, ZSTD_seqSymbol** DTablePtr, symbolEncodingType_e type, uint max, uint maxLog, void* src, nuint srcSize, uint* baseValue, uint* nbAdditionalBits, ZSTD_seqSymbol* defaultTable, uint flagRepeatTable, int ddictIsCold, int nbSeq, uint* wksp, nuint wkspSize, int bmi2)
        {
            switch (type)
            {
                case symbolEncodingType_e.set_rle:
                {
                    if (srcSize == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                    }
                }

                if ((*(byte*)(src)) > max)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }


                {
                    uint symbol = *(byte*)(src);
                    uint baseline = baseValue[symbol];
                    uint nbBits = nbAdditionalBits[symbol];

                    ZSTD_buildSeqTable_rle(DTableSpace, baseline, nbBits);
                }

                *DTablePtr = DTableSpace;
                return 1;
                case symbolEncodingType_e.set_basic:
                {
                    *DTablePtr = defaultTable;
                }

                return 0;
                case symbolEncodingType_e.set_repeat:
                {
                    if (flagRepeatTable == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }
                }

                if (ddictIsCold != 0 && (nbSeq > 24))
                {
                    void* pStart = (void*)*DTablePtr;
                    nuint pSize = (nuint)(8) * (uint)(((1 + (1 << (int)(maxLog)))));


                    {
                        sbyte* _ptr = (sbyte*)(pStart);
                        nuint _size = (nuint)(pSize);
                        nuint _pos;

                        for (_pos = 0; _pos < _size; _pos += 64)
                        {
                            Prefetch1((void*)(_ptr + _pos));
                        }
                    }

                }

                return 0;
                case symbolEncodingType_e.set_compressed:
                {
                    uint tableLog;
                    short* norm = stackalloc short[53];
                    nuint headerSize = FSE_readNCount((short*)norm, &max, &tableLog, src, srcSize);

                    if ((ERR_isError(headerSize)) != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    if (tableLog > maxLog)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    ZSTD_buildFSETable(DTableSpace, (short*)norm, max, baseValue, nbAdditionalBits, tableLog, (void*)wksp, wkspSize, bmi2);
                    *DTablePtr = DTableSpace;
                    return headerSize;
                }

                default:
                {
                    assert(0 != 0);
                }


                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                }

            }
        }

        /*! ZSTD_decodeSeqHeaders() :
         *  decode sequence header from src */
        /* Used by: decompress, fullbench (does not get its definition from here) */
        public static nuint ZSTD_decodeSeqHeaders(ZSTD_DCtx_s* dctx, int* nbSeqPtr, void* src, nuint srcSize)
        {
            byte* istart = (byte*)(src);
            byte* iend = istart + srcSize;
            byte* ip = istart;
            int nbSeq;

            if (srcSize < 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            nbSeq = (int)*ip++;
            if (nbSeq == 0)
            {
                *nbSeqPtr = 0;
                if (srcSize != 1)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                }

                return 1;
            }

            if (nbSeq > 0x7F)
            {
                if (nbSeq == 0xFF)
                {
                    if (ip + 2 > iend)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                    }

                    nbSeq = MEM_readLE16((void*)ip) + 0x7F00;
                    ip += 2;
                }
                else
                {
                    if (ip >= iend)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
                    }

                    nbSeq = ((nbSeq - 0x80) << 8) + *ip++;
                }
            }

            *nbSeqPtr = nbSeq;
            if (ip + 1 > iend)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }


            {
                symbolEncodingType_e LLtype = (symbolEncodingType_e)(*ip >> 6);
                symbolEncodingType_e OFtype = (symbolEncodingType_e)((*ip >> 4) & 3);
                symbolEncodingType_e MLtype = (symbolEncodingType_e)((*ip >> 2) & 3);

                ip++;

                {
                    nuint llhSize = ZSTD_buildSeqTable((ZSTD_seqSymbol*)dctx->entropy.LLTable, &dctx->LLTptr, LLtype, 35, 9, (void*)ip, (nuint)(iend - ip), (uint*)LL_base, (uint*)LL_bits, (ZSTD_seqSymbol*)LL_defaultDTable, dctx->fseEntropy, dctx->ddictIsCold, nbSeq, (uint*)dctx->workspace, (nuint)(2560), dctx->bmi2);

                    if ((ERR_isError(llhSize)) != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    ip += llhSize;
                }


                {
                    nuint ofhSize = ZSTD_buildSeqTable((ZSTD_seqSymbol*)dctx->entropy.OFTable, &dctx->OFTptr, OFtype, 31, 8, (void*)ip, (nuint)(iend - ip), (uint*)OF_base, (uint*)OF_bits, (ZSTD_seqSymbol*)OF_defaultDTable, dctx->fseEntropy, dctx->ddictIsCold, nbSeq, (uint*)dctx->workspace, (nuint)(2560), dctx->bmi2);

                    if ((ERR_isError(ofhSize)) != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    ip += ofhSize;
                }


                {
                    nuint mlhSize = ZSTD_buildSeqTable((ZSTD_seqSymbol*)dctx->entropy.MLTable, &dctx->MLTptr, MLtype, 52, 9, (void*)ip, (nuint)(iend - ip), (uint*)ML_base, (uint*)ML_bits, (ZSTD_seqSymbol*)ML_defaultDTable, dctx->fseEntropy, dctx->ddictIsCold, nbSeq, (uint*)dctx->workspace, (nuint)(2560), dctx->bmi2);

                    if ((ERR_isError(mlhSize)) != 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }

                    ip += mlhSize;
                }
            }

            return (nuint)(ip - istart);
        }

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
                ZSTD_copy4((void*)(*op + 4), (void*)*ip);
                *ip -= sub2;
            }
            else
            {
                ZSTD_copy8((void*)*op, (void*)*ip);
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
        private static void ZSTD_safecopy(byte* op, byte* oend_w, byte* ip, nint length, ZSTD_overlap_e ovtype)
        {
            nint diff = (nint)(op - ip);
            byte* oend = op + length;

            assert((ovtype == ZSTD_overlap_e.ZSTD_no_overlap && (diff <= -8 || diff >= 8 || op >= oend_w)) || (ovtype == ZSTD_overlap_e.ZSTD_overlap_src_before_dst && diff >= 0));
            if (length < 8)
            {
                while (op < oend)
                {
                    *op++ = *ip++;
                }

                return;
            }

            if (ovtype == ZSTD_overlap_e.ZSTD_overlap_src_before_dst)
            {
                assert(length >= 8);
                ZSTD_overlapCopy8(&op, &ip, (nuint)diff);
                assert(op - ip >= 8);
                assert(op <= oend);
            }

            if (oend <= oend_w)
            {
                ZSTD_wildcopy((void*)op, (void*)ip, length, ovtype);
                return;
            }

            if (op <= oend_w)
            {
                assert(oend > oend_w);
                ZSTD_wildcopy((void*)op, (void*)ip, (nint)(oend_w - op), ovtype);
                ip += oend_w - op;
                op = oend_w;
            }

            while (op < oend)
            {
                *op++ = *ip++;
            }
        }

        /* ZSTD_execSequenceEnd():
         * This version handles cases that are near the end of the output buffer. It requires
         * more careful checks to make sure there is no overflow. By separating out these hard
         * and unlikely cases, we can speed up the common cases.
         *
         * NOTE: This function needs to be fast for a single long sequence, but doesn't need
         * to be optimized for many small sequences, since those fall into ZSTD_execSequence().
         */
        private static nuint ZSTD_execSequenceEnd(byte* op, byte* oend, seq_t sequence, byte** litPtr, byte* litLimit, byte* prefixStart, byte* virtualStart, byte* dictEnd)
        {
            byte* oLitEnd = op + sequence.litLength;
            nuint sequenceLength = sequence.litLength + sequence.matchLength;
            byte* iLitEnd = *litPtr + sequence.litLength;
            byte* match = oLitEnd - sequence.offset;
            byte* oend_w = oend - 32;

            if (sequenceLength > (nuint)(oend - op))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (sequence.litLength > (nuint)(litLimit - *litPtr))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            assert(op < op + sequenceLength);
            assert(oLitEnd < op + sequenceLength);
            ZSTD_safecopy(op, oend_w, *litPtr, (nint)sequence.litLength, ZSTD_overlap_e.ZSTD_no_overlap);
            op = oLitEnd;
            *litPtr = iLitEnd;
            if (sequence.offset > (nuint)(oLitEnd - prefixStart))
            {
                if (sequence.offset > (nuint)(oLitEnd - virtualStart))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                match = dictEnd - (prefixStart - match);
                if (match + sequence.matchLength <= dictEnd)
                {
                    memmove((void*)(oLitEnd), (void*)(match), (sequence.matchLength));
                    return sequenceLength;
                }


                {
                    nuint length1 = (nuint)(dictEnd - match);

                    memmove((void*)(oLitEnd), (void*)(match), (length1));
                    op = oLitEnd + length1;
                    sequence.matchLength -= length1;
                    match = prefixStart;
                }
            }

            ZSTD_safecopy(op, oend_w, match, (nint)sequence.matchLength, ZSTD_overlap_e.ZSTD_overlap_src_before_dst);
            return sequenceLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_execSequence(byte* op, byte* oend, seq_t sequence, byte** litPtr, byte* litLimit, byte* prefixStart, byte* virtualStart, byte* dictEnd)
        {
            byte* oLitEnd = op + sequence.litLength;
            nuint sequenceLength = sequence.litLength + sequence.matchLength;
            byte* oMatchEnd = op + sequenceLength;
            byte* oend_w = oend - 32;
            byte* iLitEnd = *litPtr + sequence.litLength;
            byte* match = oLitEnd - sequence.offset;

            assert(op != null);
            assert(oend_w < oend);
            if ((iLitEnd > litLimit || oMatchEnd > oend_w || (MEM_32bits && (nuint)(oend - op) < sequenceLength + 32)))
            {
                return ZSTD_execSequenceEnd(op, oend, sequence, litPtr, litLimit, prefixStart, virtualStart, dictEnd);
            }

            assert(op <= oLitEnd);
            assert(oLitEnd < oMatchEnd);
            assert(oMatchEnd <= oend);
            assert(iLitEnd <= litLimit);
            assert(oLitEnd <= oend_w);
            assert(oMatchEnd <= oend_w);
            assert(32 >= 16);
            ZSTD_copy16((void*)op, (void*)(*litPtr));
            if ((sequence.litLength > 16))
            {
                ZSTD_wildcopy((void*)(op + 16), (void*)((*litPtr) + 16), (nint)(sequence.litLength - 16), ZSTD_overlap_e.ZSTD_no_overlap);
            }

            op = oLitEnd;
            *litPtr = iLitEnd;
            if (sequence.offset > (nuint)(oLitEnd - prefixStart))
            {
                if ((sequence.offset > (nuint)(oLitEnd - virtualStart)))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                match = dictEnd + (match - prefixStart);
                if (match + sequence.matchLength <= dictEnd)
                {
                    memmove((void*)(oLitEnd), (void*)(match), (sequence.matchLength));
                    return sequenceLength;
                }


                {
                    nuint length1 = (nuint)(dictEnd - match);

                    memmove((void*)(oLitEnd), (void*)(match), (length1));
                    op = oLitEnd + length1;
                    sequence.matchLength -= length1;
                    match = prefixStart;
                }
            }

            assert(op <= oMatchEnd);
            assert(oMatchEnd <= oend_w);
            assert(match >= prefixStart);
            assert(sequence.matchLength >= 1);
            if ((sequence.offset >= 16))
            {
                ZSTD_wildcopy((void*)op, (void*)match, (nint)(sequence.matchLength), ZSTD_overlap_e.ZSTD_no_overlap);
                return sequenceLength;
            }

            assert(sequence.offset < 16);
            ZSTD_overlapCopy8(&op, &match, sequence.offset);
            if (sequence.matchLength > 8)
            {
                assert(op < oMatchEnd);
                ZSTD_wildcopy((void*)op, (void*)match, (nint)(sequence.matchLength) - 8, ZSTD_overlap_e.ZSTD_overlap_src_before_dst);
            }

            return sequenceLength;
        }

        private static void ZSTD_initFseState(ZSTD_fseState* DStatePtr, BIT_DStream_t* bitD, ZSTD_seqSymbol* dt)
        {
            void* ptr = (void*)dt;
            ZSTD_seqSymbol_header* DTableH = (ZSTD_seqSymbol_header*)(ptr);

            DStatePtr->state = BIT_readBits(bitD, DTableH->tableLog);
            BIT_reloadDStream(bitD);
            DStatePtr->table = dt + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_updateFseState(ZSTD_fseState* DStatePtr, BIT_DStream_t* bitD)
        {
            ZSTD_seqSymbol DInfo = DStatePtr->table[DStatePtr->state];
            uint nbBits = DInfo.nbBits;
            nuint lowBits = BIT_readBits(bitD, nbBits);

            DStatePtr->state = DInfo.nextState + lowBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZSTD_updateFseStateWithDInfo(ZSTD_fseState* DStatePtr, BIT_DStream_t* bitD, ZSTD_seqSymbol DInfo)
        {
            uint nbBits = DInfo.nbBits;
            nuint lowBits = BIT_readBits(bitD, nbBits);

            DStatePtr->state = DInfo.nextState + lowBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        private static seq_t ZSTD_decodeSequence(seqState_t* seqState, ZSTD_longOffset_e longOffsets)
        {
            seq_t seq;
            var _ = &seq;
            ZSTD_seqSymbol llDInfo = seqState->stateLL.table[seqState->stateLL.state];
            ZSTD_seqSymbol mlDInfo = seqState->stateML.table[seqState->stateML.state];
            ZSTD_seqSymbol ofDInfo = seqState->stateOffb.table[seqState->stateOffb.state];
            uint llBase = llDInfo.baseValue;
            uint mlBase = mlDInfo.baseValue;
            uint ofBase = ofDInfo.baseValue;
            byte llBits = llDInfo.nbAdditionalBits;
            byte mlBits = mlDInfo.nbAdditionalBits;
            byte ofBits = ofDInfo.nbAdditionalBits;
            byte totalBits = (byte)(llBits + mlBits + ofBits);


            {
                nuint offset;

                if (ofBits > 1)
                {
                    assert(ofBits <= 31);
                    if (MEM_32bits && longOffsets != default && (ofBits >= 25))
                    {
                        uint extraBits = ofBits - ((ofBits) < (32 - seqState->DStream.bitsConsumed) ? (ofBits) : (32 - seqState->DStream.bitsConsumed));

                        offset = ofBase + (BIT_readBitsFast(&seqState->DStream, ofBits - extraBits) << (int)extraBits);
                        BIT_reloadDStream(&seqState->DStream);
                        if (extraBits != 0)
                        {
                            offset += BIT_readBitsFast(&seqState->DStream, extraBits);
                        }

                        assert(extraBits <= (uint)((30 > 25 ? 30 - 25 : 0)));
                    }
                    else
                    {
                        offset = ofBase + BIT_readBitsFast(&seqState->DStream, ofBits);
                        if (MEM_32bits)
                        {
                            BIT_reloadDStream(&seqState->DStream);
                        }
                    }

                    seqState->prevOffset[2] = seqState->prevOffset[1];
                    seqState->prevOffset[1] = seqState->prevOffset[0];
                    seqState->prevOffset[0] = offset;
                }
                else
                {
                    uint ll0 = (((llBase == 0)) ? 1U : 0U);

                    if (((ofBits == 0)))
                    {
                        if (ll0 == 0)
                        {
                            offset = seqState->prevOffset[0];
                        }
                        else
                        {
                            offset = seqState->prevOffset[1];
                            seqState->prevOffset[1] = seqState->prevOffset[0];
                            seqState->prevOffset[0] = offset;
                        }
                    }
                    else
                    {
                        offset = ofBase + ll0 + BIT_readBitsFast(&seqState->DStream, 1);

                        {
                            nuint temp = (offset == 3) ? seqState->prevOffset[0] - 1 : seqState->prevOffset[offset];

                            temp += (temp == 0 ? 1U : 0U);
                            if (offset != 1)
                            {
                                seqState->prevOffset[2] = seqState->prevOffset[1];
                            }

                            seqState->prevOffset[1] = seqState->prevOffset[0];
                            seqState->prevOffset[0] = offset = temp;
                        }
                    }
                }

                seq.offset = offset;
            }

            seq.matchLength = mlBase;
            if (mlBits > 0)
            {
                seq.matchLength += BIT_readBitsFast(&seqState->DStream, mlBits);
            }

            if (MEM_32bits && (mlBits + llBits >= 25 - (30 > 25 ? 30 - 25 : 0)))
            {
                BIT_reloadDStream(&seqState->DStream);
            }

            if (MEM_64bits && (totalBits >= 57 - (9 + 9 + 8)))
            {
                BIT_reloadDStream(&seqState->DStream);
            }

            seq.litLength = llBase;
            if (llBits > 0)
            {
                seq.litLength += BIT_readBitsFast(&seqState->DStream, llBits);
            }

            if (MEM_32bits)
            {
                BIT_reloadDStream(&seqState->DStream);
            }


            {
                int kUseUpdateFseState = 0;

                if (kUseUpdateFseState != 0)
                {
                    ZSTD_updateFseState(&seqState->stateLL, &seqState->DStream);
                    ZSTD_updateFseState(&seqState->stateML, &seqState->DStream);
                    if (MEM_32bits)
                    {
                        BIT_reloadDStream(&seqState->DStream);
                    }

                    ZSTD_updateFseState(&seqState->stateOffb, &seqState->DStream);
                }
                else
                {
                    ZSTD_updateFseStateWithDInfo(&seqState->stateLL, &seqState->DStream, llDInfo);
                    ZSTD_updateFseStateWithDInfo(&seqState->stateML, &seqState->DStream, mlDInfo);
                    if (MEM_32bits)
                    {
                        BIT_reloadDStream(&seqState->DStream);
                    }

                    ZSTD_updateFseStateWithDInfo(&seqState->stateOffb, &seqState->DStream, ofDInfo);
                }
            }

            return seq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_decompressSequences_body(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            byte* ip = (byte*)(seqStart);
            byte* iend = ip + seqSize;
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + maxDstSize;
            byte* op = ostart;
            byte* litPtr = dctx->litPtr;
            byte* litEnd = litPtr + dctx->litSize;
            byte* prefixStart = (byte*)(dctx->prefixStart);
            byte* vBase = (byte*)(dctx->virtualStart);
            byte* dictEnd = (byte*)(dctx->dictEnd);

            if (nbSeq != 0)
            {
                seqState_t seqState;
				var _ = &seqState;

                dctx->fseEntropy = 1;

                {
                    uint i;

                    for (i = 0; i < 3; i++)
                    {
                        seqState.prevOffset[i] = dctx->entropy.rep[i];
                    }
                }

                if ((ERR_isError(BIT_initDStream(&seqState.DStream, (void*)ip, (nuint)(iend - ip)))) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                ZSTD_initFseState(&seqState.stateLL, &seqState.DStream, dctx->LLTptr);
                ZSTD_initFseState(&seqState.stateOffb, &seqState.DStream, dctx->OFTptr);
                ZSTD_initFseState(&seqState.stateML, &seqState.DStream, dctx->MLTptr);
                assert(dst != null);
                for (;;)
                {
                    seq_t sequence = ZSTD_decodeSequence(&seqState, isLongOffset);
                    nuint oneSeqSize = ZSTD_execSequence(op, oend, sequence, &litPtr, litEnd, prefixStart, vBase, dictEnd);

                    if ((ERR_isError(oneSeqSize)) != 0)
                    {
                        return oneSeqSize;
                    }

                    op += oneSeqSize;
                    if (--nbSeq == 0)
                    {
                        break;
                    }

                    BIT_reloadDStream(&(seqState.DStream));
                }

                if (nbSeq != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                if (BIT_reloadDStream(&seqState.DStream) < BIT_DStream_status.BIT_DStream_completed)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }


                {
                    uint i;

                    for (i = 0; i < 3; i++)
                    {
                        dctx->entropy.rep[i] = (uint)(seqState.prevOffset[i]);
                    }
                }
            }


            {
                nuint lastLLSize = (nuint)(litEnd - litPtr);

                if (lastLLSize > (nuint)(oend - op))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                if (op != null)
                {
                    memcpy((void*)(op), (void*)(litPtr), (lastLLSize));
                    op += lastLLSize;
                }
            }

            return (nuint)(op - ostart);
        }

        private static nuint ZSTD_decompressSequences_default(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            return ZSTD_decompressSequences_body(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_prefetchMatch(nuint prefetchPos, seq_t sequence, byte* prefixStart, byte* dictEnd)
        {
            prefetchPos += sequence.litLength;

            {
                byte* matchBase = (sequence.offset > prefetchPos) ? dictEnd : prefixStart;
                byte* match = matchBase + prefetchPos - sequence.offset;

                Prefetch0((void*)match);
                Prefetch0((void*)(match + 64));
            }

            return prefetchPos + sequence.matchLength;
        }

        /* This decoding function employs prefetching
         * to reduce latency impact of cache misses.
         * It's generally employed when block contains a significant portion of long-distance matches
         * or when coupled with a "cold" dictionary */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_decompressSequencesLong_body(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            byte* ip = (byte*)(seqStart);
            byte* iend = ip + seqSize;
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + maxDstSize;
            byte* op = ostart;
            byte* litPtr = dctx->litPtr;
            byte* litEnd = litPtr + dctx->litSize;
            byte* prefixStart = (byte*)(dctx->prefixStart);
            byte* dictStart = (byte*)(dctx->virtualStart);
            byte* dictEnd = (byte*)(dctx->dictEnd);

            if (nbSeq != 0)
            {
                seq_t* sequences = stackalloc seq_t[8];
                int seqAdvance = ((nbSeq) < (8) ? (nbSeq) : (8));
                seqState_t seqState;
                var _ = &seqState;
                int seqNb;
                nuint prefetchPos = (nuint)(op - prefixStart);

                dctx->fseEntropy = 1;

                {
                    int i;

                    for (i = 0; i < 3; i++)
                    {
                        seqState.prevOffset[i] = dctx->entropy.rep[i];
                    }
                }

                assert(dst != null);
                assert(iend >= ip);
                if ((ERR_isError(BIT_initDStream(&seqState.DStream, (void*)ip, (nuint)(iend - ip)))) != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                ZSTD_initFseState(&seqState.stateLL, &seqState.DStream, dctx->LLTptr);
                ZSTD_initFseState(&seqState.stateOffb, &seqState.DStream, dctx->OFTptr);
                ZSTD_initFseState(&seqState.stateML, &seqState.DStream, dctx->MLTptr);
                for (seqNb = 0; (BIT_reloadDStream(&seqState.DStream) <= BIT_DStream_status.BIT_DStream_completed) && (seqNb < seqAdvance); seqNb++)
                {
                    seq_t sequence = ZSTD_decodeSequence(&seqState, isLongOffset);

                    prefetchPos = ZSTD_prefetchMatch(prefetchPos, sequence, prefixStart, dictEnd);
                    sequences[seqNb] = sequence;
                }

                if (seqNb < seqAdvance)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                for (; (BIT_reloadDStream(&(seqState.DStream)) <= BIT_DStream_status.BIT_DStream_completed) && (seqNb < nbSeq); seqNb++)
                {
                    seq_t sequence = ZSTD_decodeSequence(&seqState, isLongOffset);
                    nuint oneSeqSize = ZSTD_execSequence(op, oend, sequences[(seqNb - 8) & (8 - 1)], &litPtr, litEnd, prefixStart, dictStart, dictEnd);

                    if ((ERR_isError(oneSeqSize)) != 0)
                    {
                        return oneSeqSize;
                    }

                    prefetchPos = ZSTD_prefetchMatch(prefetchPos, sequence, prefixStart, dictEnd);
                    sequences[seqNb & (8 - 1)] = sequence;
                    op += oneSeqSize;
                }

                if (seqNb < nbSeq)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                seqNb -= seqAdvance;
                for (; seqNb < nbSeq; seqNb++)
                {
                    nuint oneSeqSize = ZSTD_execSequence(op, oend, sequences[seqNb & (8 - 1)], &litPtr, litEnd, prefixStart, dictStart, dictEnd);

                    if ((ERR_isError(oneSeqSize)) != 0)
                    {
                        return oneSeqSize;
                    }

                    op += oneSeqSize;
                }


                {
                    uint i;

                    for (i = 0; i < 3; i++)
                    {
                        dctx->entropy.rep[i] = (uint)(seqState.prevOffset[i]);
                    }
                }
            }


            {
                nuint lastLLSize = (nuint)(litEnd - litPtr);

                if (lastLLSize > (nuint)(oend - op))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                if (op != null)
                {
                    memcpy((void*)(op), (void*)(litPtr), (lastLLSize));
                    op += lastLLSize;
                }
            }

            return (nuint)(op - ostart);
        }

        private static nuint ZSTD_decompressSequencesLong_default(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            return ZSTD_decompressSequencesLong_body(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
        }

        private static nuint ZSTD_decompressSequences_bmi2(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            return ZSTD_decompressSequences_body(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
        }

        private static nuint ZSTD_decompressSequencesLong_bmi2(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            return ZSTD_decompressSequencesLong_body(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
        }

        private static nuint ZSTD_decompressSequences(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            if (dctx->bmi2 != 0)
            {
                return ZSTD_decompressSequences_bmi2(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
            }

            return ZSTD_decompressSequences_default(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
        }

        /* ZSTD_decompressSequencesLong() :
         * decompression function triggered when a minimum share of offsets is considered "long",
         * aka out of cache.
         * note : "long" definition seems overloaded here, sometimes meaning "wider than bitstream register", and sometimes meaning "farther than memory cache distance".
         * This function will try to mitigate main memory latency through the use of prefetching */
        private static nuint ZSTD_decompressSequencesLong(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame)
        {
            if (dctx->bmi2 != 0)
            {
                return ZSTD_decompressSequencesLong_bmi2(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
            }

            return ZSTD_decompressSequencesLong_default(dctx, dst, maxDstSize, seqStart, seqSize, nbSeq, isLongOffset, frame);
        }

        /* ZSTD_getLongOffsetsShare() :
         * condition : offTable must be valid
         * @return : "share" of long offsets (arbitrarily defined as > (1<<23))
         *           compared to maximum possible of (1<<OffFSELog) */
        private static uint ZSTD_getLongOffsetsShare(ZSTD_seqSymbol* offTable)
        {
            void* ptr = (void*)offTable;
            uint tableLog = ((ZSTD_seqSymbol_header*)(ptr))[0].tableLog;
            ZSTD_seqSymbol* table = offTable + 1;
            uint max = (uint)(1 << (int)tableLog);
            uint u, total = 0;

            assert(max <= (uint)((1 << 8)));
            for (u = 0; u < max; u++)
            {
                if (table[u].nbAdditionalBits > 22)
                {
                    total += 1;
                }
            }

            assert(tableLog <= 8);
            total <<= (int)(8 - tableLog);
            return total;
        }

        /* ZSTD_decompressBlock_internal() :
         * decompress block, starting at `src`,
         * into destination buffer `dst`.
         * @return : decompressed block size,
         *           or an error code (which can be tested using ZSTD_isError())
         */
        public static nuint ZSTD_decompressBlock_internal(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize, int frame)
        {
            byte* ip = (byte*)(src);
            ZSTD_longOffset_e isLongOffset = (ZSTD_longOffset_e)((MEM_32bits && (frame == 0 || (dctx->fParams.windowSize > (1UL << (int)((uint)(MEM_32bits ? 25 : 57)))))) ? 1 : 0);

            if (srcSize >= (uint)((1 << 17)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }


            {
                nuint litCSize = ZSTD_decodeLiteralsBlock(dctx, src, srcSize);

                if ((ERR_isError(litCSize)) != 0)
                {
                    return litCSize;
                }

                ip += litCSize;
                srcSize -= litCSize;
            }


            {
                int usePrefetchDecoder = dctx->ddictIsCold;
                int nbSeq;
                nuint seqHSize = ZSTD_decodeSeqHeaders(dctx, &nbSeq, (void*)ip, srcSize);

                if ((ERR_isError(seqHSize)) != 0)
                {
                    return seqHSize;
                }

                ip += seqHSize;
                srcSize -= seqHSize;
                if (dst == null && nbSeq > 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                if (usePrefetchDecoder == 0 && (frame == 0 || (dctx->fParams.windowSize > (uint)((1 << 24)))) && (nbSeq > 8))
                {
                    uint shareLongOffsets = ZSTD_getLongOffsetsShare(dctx->OFTptr);
                    uint minShare = (uint)(MEM_64bits ? 7 : 20);

                    usePrefetchDecoder = ((shareLongOffsets >= minShare) ? 1 : 0);
                }

                dctx->ddictIsCold = 0;
                if (usePrefetchDecoder != 0)
                {
                    return ZSTD_decompressSequencesLong(dctx, dst, dstCapacity, (void*)ip, srcSize, nbSeq, isLongOffset, frame);
                }

                return ZSTD_decompressSequences(dctx, dst, dstCapacity, (void*)ip, srcSize, nbSeq, isLongOffset, frame);
            }
        }

        /*! ZSTD_checkContinuity() :
         *  check if next `dst` follows previous position, where decompression ended.
         *  If yes, do nothing (continue on current segment).
         *  If not, classify previous segment as "external dictionary", and start a new segment.
         *  This function cannot fail. */
        public static void ZSTD_checkContinuity(ZSTD_DCtx_s* dctx, void* dst, nuint dstSize)
        {
            if (dst != dctx->previousDstEnd && dstSize > 0)
            {
                dctx->dictEnd = dctx->previousDstEnd;
                dctx->virtualStart = (sbyte*)(dst) - ((sbyte*)(dctx->previousDstEnd) - (sbyte*)(dctx->prefixStart));
                dctx->prefixStart = dst;
                dctx->previousDstEnd = dst;
            }
        }

        public static nuint ZSTD_decompressBlock(ZSTD_DCtx_s* dctx, void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            nuint dSize;

            ZSTD_checkContinuity(dctx, dst, dstCapacity);
            dSize = ZSTD_decompressBlock_internal(dctx, dst, dstCapacity, src, srcSize, 0);
            dctx->previousDstEnd = (sbyte*)(dst) + dSize;
            return dSize;
        }
    }
}
