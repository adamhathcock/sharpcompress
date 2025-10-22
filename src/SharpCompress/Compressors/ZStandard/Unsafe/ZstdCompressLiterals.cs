using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /* **************************************************************
     *  Literals compression - special cases
     ****************************************************************/
    private static nuint ZSTD_noCompressLiterals(
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        byte* ostart = (byte*)dst;
        uint flSize = (uint)(1 + (srcSize > 31 ? 1 : 0) + (srcSize > 4095 ? 1 : 0));
        if (srcSize + flSize > dstCapacity)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        switch (flSize)
        {
            case 1:
                ostart[0] = (byte)((uint)SymbolEncodingType_e.set_basic + (srcSize << 3));
                break;
            case 2:
                MEM_writeLE16(
                    ostart,
                    (ushort)((uint)SymbolEncodingType_e.set_basic + (1 << 2) + (srcSize << 4))
                );
                break;
            case 3:
                MEM_writeLE32(
                    ostart,
                    (uint)((uint)SymbolEncodingType_e.set_basic + (3 << 2) + (srcSize << 4))
                );
                break;
            default:
                assert(0 != 0);
                break;
        }

        memcpy(ostart + flSize, src, (uint)srcSize);
        return srcSize + flSize;
    }

    private static int allBytesIdentical(void* src, nuint srcSize)
    {
        assert(srcSize >= 1);
        assert(src != null);
        {
            byte b = ((byte*)src)[0];
            nuint p;
            for (p = 1; p < srcSize; p++)
            {
                if (((byte*)src)[p] != b)
                    return 0;
            }

            return 1;
        }
    }

    /* ZSTD_compressRleLiteralsBlock() :
     * Conditions :
     * - All bytes in @src are identical
     * - dstCapacity >= 4 */
    private static nuint ZSTD_compressRleLiteralsBlock(
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize
    )
    {
        byte* ostart = (byte*)dst;
        uint flSize = (uint)(1 + (srcSize > 31 ? 1 : 0) + (srcSize > 4095 ? 1 : 0));
        assert(dstCapacity >= 4);
        assert(allBytesIdentical(src, srcSize) != 0);
        switch (flSize)
        {
            case 1:
                ostart[0] = (byte)((uint)SymbolEncodingType_e.set_rle + (srcSize << 3));
                break;
            case 2:
                MEM_writeLE16(
                    ostart,
                    (ushort)((uint)SymbolEncodingType_e.set_rle + (1 << 2) + (srcSize << 4))
                );
                break;
            case 3:
                MEM_writeLE32(
                    ostart,
                    (uint)((uint)SymbolEncodingType_e.set_rle + (3 << 2) + (srcSize << 4))
                );
                break;
            default:
                assert(0 != 0);
                break;
        }

        ostart[flSize] = *(byte*)src;
        return flSize + 1;
    }

    /* ZSTD_minLiteralsToCompress() :
     * returns minimal amount of literals
     * for literal compression to even be attempted.
     * Minimum is made tighter as compression strategy increases.
     */
    private static nuint ZSTD_minLiteralsToCompress(ZSTD_strategy strategy, HUF_repeat huf_repeat)
    {
        assert((int)strategy >= 0);
        assert((int)strategy <= 9);
        {
            int shift = 9 - (int)strategy < 3 ? 9 - (int)strategy : 3;
            nuint mintc = huf_repeat == HUF_repeat.HUF_repeat_valid ? 6 : (nuint)8 << shift;
            return mintc;
        }
    }

    /* ZSTD_compressLiterals():
     * @entropyWorkspace: must be aligned on 4-bytes boundaries
     * @entropyWorkspaceSize : must be >= HUF_WORKSPACE_SIZE
     * @suspectUncompressible: sampling checks, to potentially skip huffman coding
     */
    private static nuint ZSTD_compressLiterals(
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        void* entropyWorkspace,
        nuint entropyWorkspaceSize,
        ZSTD_hufCTables_t* prevHuf,
        ZSTD_hufCTables_t* nextHuf,
        ZSTD_strategy strategy,
        int disableLiteralCompression,
        int suspectUncompressible,
        int bmi2
    )
    {
        nuint lhSize = (nuint)(
            3 + (srcSize >= 1 * (1 << 10) ? 1 : 0) + (srcSize >= 16 * (1 << 10) ? 1 : 0)
        );
        byte* ostart = (byte*)dst;
        uint singleStream = srcSize < 256 ? 1U : 0U;
        SymbolEncodingType_e hType = SymbolEncodingType_e.set_compressed;
        nuint cLitSize;
        memcpy(nextHuf, prevHuf, (uint)sizeof(ZSTD_hufCTables_t));
        if (disableLiteralCompression != 0)
            return ZSTD_noCompressLiterals(dst, dstCapacity, src, srcSize);
        if (srcSize < ZSTD_minLiteralsToCompress(strategy, prevHuf->repeatMode))
            return ZSTD_noCompressLiterals(dst, dstCapacity, src, srcSize);
        if (dstCapacity < lhSize + 1)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        {
            HUF_repeat repeat = prevHuf->repeatMode;
            int flags =
                0
                | (bmi2 != 0 ? (int)HUF_flags_e.HUF_flags_bmi2 : 0)
                | (
                    strategy < ZSTD_strategy.ZSTD_lazy && srcSize <= 1024
                        ? (int)HUF_flags_e.HUF_flags_preferRepeat
                        : 0
                )
                | (
                    strategy >= ZSTD_strategy.ZSTD_btultra
                        ? (int)HUF_flags_e.HUF_flags_optimalDepth
                        : 0
                )
                | (
                    suspectUncompressible != 0
                        ? (int)HUF_flags_e.HUF_flags_suspectUncompressible
                        : 0
                );
            void* huf_compress;
            if (repeat == HUF_repeat.HUF_repeat_valid && lhSize == 3)
                singleStream = 1;
            huf_compress =
                singleStream != 0
                    ? (delegate* managed<
                        void*,
                        nuint,
                        void*,
                        nuint,
                        uint,
                        uint,
                        void*,
                        nuint,
                        nuint*,
                        HUF_repeat*,
                        int,
                        nuint>)(&HUF_compress1X_repeat)
                    : (delegate* managed<
                        void*,
                        nuint,
                        void*,
                        nuint,
                        uint,
                        uint,
                        void*,
                        nuint,
                        nuint*,
                        HUF_repeat*,
                        int,
                        nuint>)(&HUF_compress4X_repeat);
            cLitSize = (
                (delegate* managed<
                    void*,
                    nuint,
                    void*,
                    nuint,
                    uint,
                    uint,
                    void*,
                    nuint,
                    nuint*,
                    HUF_repeat*,
                    int,
                    nuint>)huf_compress
            )(
                ostart + lhSize,
                dstCapacity - lhSize,
                src,
                srcSize,
                255,
                11,
                entropyWorkspace,
                entropyWorkspaceSize,
                &nextHuf->CTable.e0,
                &repeat,
                flags
            );
            if (repeat != HUF_repeat.HUF_repeat_none)
            {
                hType = SymbolEncodingType_e.set_repeat;
            }
        }

        {
            nuint minGain = ZSTD_minGain(srcSize, strategy);
            if (cLitSize == 0 || cLitSize >= srcSize - minGain || ERR_isError(cLitSize))
            {
                memcpy(nextHuf, prevHuf, (uint)sizeof(ZSTD_hufCTables_t));
                return ZSTD_noCompressLiterals(dst, dstCapacity, src, srcSize);
            }
        }

        if (cLitSize == 1)
        {
            if (srcSize >= 8 || allBytesIdentical(src, srcSize) != 0)
            {
                memcpy(nextHuf, prevHuf, (uint)sizeof(ZSTD_hufCTables_t));
                return ZSTD_compressRleLiteralsBlock(dst, dstCapacity, src, srcSize);
            }
        }

        if (hType == SymbolEncodingType_e.set_compressed)
        {
            nextHuf->repeatMode = HUF_repeat.HUF_repeat_check;
        }

        switch (lhSize)
        {
            case 3:
#if DEBUG
                if (singleStream == 0)
                    assert(srcSize >= 6);

#endif
                {
                    uint lhc =
                        (uint)hType
                        + ((singleStream == 0 ? 1U : 0U) << 2)
                        + ((uint)srcSize << 4)
                        + ((uint)cLitSize << 14);
                    MEM_writeLE24(ostart, lhc);
                    break;
                }

            case 4:
                assert(srcSize >= 6);

                {
                    uint lhc =
                        (uint)(hType + (2 << 2)) + ((uint)srcSize << 4) + ((uint)cLitSize << 18);
                    MEM_writeLE32(ostart, lhc);
                    break;
                }

            case 5:
                assert(srcSize >= 6);

                {
                    uint lhc =
                        (uint)(hType + (3 << 2)) + ((uint)srcSize << 4) + ((uint)cLitSize << 22);
                    MEM_writeLE32(ostart, lhc);
                    ostart[4] = (byte)(cLitSize >> 10);
                    break;
                }

            default:
                assert(0 != 0);
                break;
        }

        return lhSize + cLitSize;
    }
}
