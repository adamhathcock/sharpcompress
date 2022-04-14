using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static nuint ZSTD_noCompressLiterals(void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            byte* ostart = (byte*)(dst);
            uint flSize = (uint)(1 + ((srcSize > 31) ? 1 : 0) + ((srcSize > 4095) ? 1 : 0));

            if (srcSize + flSize > dstCapacity)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            switch (flSize)
            {
                case 1:
                {
                    ostart[0] = (byte)((uint)(symbolEncodingType_e.set_basic) + (srcSize << 3));
                }

                break;
                case 2:
                {
                    MEM_writeLE16((void*)ostart, (ushort)((uint)(symbolEncodingType_e.set_basic) + (uint)((1 << 2)) + (srcSize << 4)));
                }

                break;
                case 3:
                {
                    MEM_writeLE32((void*)ostart, (uint)((uint)(symbolEncodingType_e.set_basic) + (uint)((3 << 2)) + (srcSize << 4)));
                }

                break;
                default:
                {
                    assert(0 != 0);
                }
                break;
            }

            memcpy((void*)((ostart + flSize)), (src), (srcSize));
            return srcSize + flSize;
        }

        public static nuint ZSTD_compressRleLiteralsBlock(void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            byte* ostart = (byte*)(dst);
            uint flSize = (uint)(1 + ((srcSize > 31) ? 1 : 0) + ((srcSize > 4095) ? 1 : 0));

            switch (flSize)
            {
                case 1:
                {
                    ostart[0] = (byte)((uint)(symbolEncodingType_e.set_rle) + (srcSize << 3));
                }

                break;
                case 2:
                {
                    MEM_writeLE16((void*)ostart, (ushort)((uint)(symbolEncodingType_e.set_rle) + (uint)((1 << 2)) + (srcSize << 4)));
                }

                break;
                case 3:
                {
                    MEM_writeLE32((void*)ostart, (uint)((uint)(symbolEncodingType_e.set_rle) + (uint)((3 << 2)) + (srcSize << 4)));
                }

                break;
                default:
                {
                    assert(0 != 0);
                }
                break;
            }

            ostart[flSize] = *(byte*)(src);
            return flSize + 1;
        }

        public static nuint ZSTD_compressLiterals(ZSTD_hufCTables_t* prevHuf, ZSTD_hufCTables_t* nextHuf, ZSTD_strategy strategy, int disableLiteralCompression, void* dst, nuint dstCapacity, void* src, nuint srcSize, void* entropyWorkspace, nuint entropyWorkspaceSize, int bmi2)
        {
            nuint minGain = ZSTD_minGain(srcSize, strategy);
            nuint lhSize = (nuint)(3 + ((srcSize >= (uint)(1 * (1 << 10))) ? 1 : 0) + ((srcSize >= (uint)(16 * (1 << 10))) ? 1 : 0));
            byte* ostart = (byte*)(dst);
            uint singleStream = ((srcSize < 256) ? 1U : 0U);
            symbolEncodingType_e hType = symbolEncodingType_e.set_compressed;
            nuint cLitSize;

            memcpy((void*)(nextHuf), (void*)(prevHuf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
            if (disableLiteralCompression != 0)
            {
                return ZSTD_noCompressLiterals(dst, dstCapacity, src, srcSize);
            }


            {
                nuint minLitSize = (nuint)((prevHuf->repeatMode == HUF_repeat.HUF_repeat_valid) ? 6 : 63);

                if (srcSize <= minLitSize)
                {
                    return ZSTD_noCompressLiterals(dst, dstCapacity, src, srcSize);
                }
            }

            if (dstCapacity < lhSize + 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }


            {
                HUF_repeat repeat = prevHuf->repeatMode;
                int preferRepeat = strategy < ZSTD_strategy.ZSTD_lazy ? ((srcSize <= 1024) ? 1 : 0) : 0;

                if (repeat == HUF_repeat.HUF_repeat_valid && lhSize == 3)
                {
                    singleStream = 1;
                }

                cLitSize = singleStream != 0 ? HUF_compress1X_repeat((void*)(ostart + lhSize), dstCapacity - lhSize, src, srcSize, 255, 11, entropyWorkspace, entropyWorkspaceSize, (HUF_CElt_s*)(nextHuf->CTable), &repeat, preferRepeat, bmi2) : HUF_compress4X_repeat((void*)(ostart + lhSize), dstCapacity - lhSize, src, srcSize, 255, 11, entropyWorkspace, entropyWorkspaceSize, (HUF_CElt_s*)(nextHuf->CTable), &repeat, preferRepeat, bmi2);
                if (repeat != HUF_repeat.HUF_repeat_none)
                {
                    hType = symbolEncodingType_e.set_repeat;
                }
            }

            if ((cLitSize == 0) || (cLitSize >= srcSize - minGain) || (ERR_isError(cLitSize)) != 0)
            {
                memcpy((void*)(nextHuf), (void*)(prevHuf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
                return ZSTD_noCompressLiterals(dst, dstCapacity, src, srcSize);
            }

            if (cLitSize == 1)
            {
                memcpy((void*)(nextHuf), (void*)(prevHuf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
                return ZSTD_compressRleLiteralsBlock(dst, dstCapacity, src, srcSize);
            }

            if (hType == symbolEncodingType_e.set_compressed)
            {
                nextHuf->repeatMode = HUF_repeat.HUF_repeat_check;
            }

            switch (lhSize)
            {
                case 3:
                {
                    uint lhc = (uint)(hType + ((singleStream == 0 ? 1 : 0) << 2)) + ((uint)(srcSize) << 4) + ((uint)(cLitSize) << 14);

                    MEM_writeLE24((void*)ostart, lhc);
                    break;
                }

                case 4:
                {
                    uint lhc = (uint)(hType + (2 << 2)) + ((uint)(srcSize) << 4) + ((uint)(cLitSize) << 18);

                    MEM_writeLE32((void*)ostart, lhc);
                    break;
                }

                case 5:
                {
                    uint lhc = (uint)(hType + (3 << 2)) + ((uint)(srcSize) << 4) + ((uint)(cLitSize) << 22);

                    MEM_writeLE32((void*)ostart, lhc);
                    ostart[4] = (byte)(cLitSize >> 10);
                    break;
                }

                default:
                {
                    assert(0 != 0);
                }
                break;
            }

            return lhSize + cLitSize;
        }
    }
}
