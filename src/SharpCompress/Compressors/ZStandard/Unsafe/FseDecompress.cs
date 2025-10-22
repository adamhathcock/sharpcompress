using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static nuint FSE_buildDTable_internal(
        uint* dt,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint tableLog,
        void* workSpace,
        nuint wkspSize
    )
    {
        /* because *dt is unsigned, 32-bits aligned on 32-bits */
        void* tdPtr = dt + 1;
        FSE_decode_t* tableDecode = (FSE_decode_t*)tdPtr;
        ushort* symbolNext = (ushort*)workSpace;
        byte* spread = (byte*)(symbolNext + maxSymbolValue + 1);
        uint maxSV1 = maxSymbolValue + 1;
        uint tableSize = (uint)(1 << (int)tableLog);
        uint highThreshold = tableSize - 1;
        if (sizeof(short) * (maxSymbolValue + 1) + (1UL << (int)tableLog) + 8 > wkspSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge));
        if (maxSymbolValue > 255)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge));
        if (tableLog > 14 - 2)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        {
            FSE_DTableHeader DTableH;
            DTableH.tableLog = (ushort)tableLog;
            DTableH.fastMode = 1;
            {
                short largeLimit = (short)(1 << (int)(tableLog - 1));
                uint s;
                for (s = 0; s < maxSV1; s++)
                {
                    if (normalizedCounter[s] == -1)
                    {
                        tableDecode[highThreshold--].symbol = (byte)s;
                        symbolNext[s] = 1;
                    }
                    else
                    {
                        if (normalizedCounter[s] >= largeLimit)
                            DTableH.fastMode = 0;
                        symbolNext[s] = (ushort)normalizedCounter[s];
                    }
                }
            }

            memcpy(dt, &DTableH, (uint)sizeof(FSE_DTableHeader));
        }

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
                        tableDecode[uPosition].symbol = spread[s + u];
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
                for (i = 0; i < normalizedCounter[s]; i++)
                {
                    tableDecode[position].symbol = (byte)s;
                    position = position + step & tableMask;
                    while (position > highThreshold)
                        position = position + step & tableMask;
                }
            }

            if (position != 0)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }

        {
            uint u;
            for (u = 0; u < tableSize; u++)
            {
                byte symbol = tableDecode[u].symbol;
                uint nextState = symbolNext[symbol]++;
                tableDecode[u].nbBits = (byte)(tableLog - ZSTD_highbit32(nextState));
                tableDecode[u].newState = (ushort)(
                    (nextState << tableDecode[u].nbBits) - tableSize
                );
            }
        }

        return 0;
    }

    private static nuint FSE_buildDTable_wksp(
        uint* dt,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint tableLog,
        void* workSpace,
        nuint wkspSize
    )
    {
        return FSE_buildDTable_internal(
            dt,
            normalizedCounter,
            maxSymbolValue,
            tableLog,
            workSpace,
            wkspSize
        );
    }

    /*-*******************************************************
     *  Decompression (Byte symbols)
     *********************************************************/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint FSE_decompress_usingDTable_generic(
        void* dst,
        nuint maxDstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* dt,
        uint fast
    )
    {
        byte* ostart = (byte*)dst;
        byte* op = ostart;
        byte* omax = op + maxDstSize;
        byte* olimit = omax - 3;
        BIT_DStream_t bitD;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out bitD);
        FSE_DState_t state1;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out state1);
        FSE_DState_t state2;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out state2);
        {
            /* Init */
            nuint _var_err__ = BIT_initDStream(ref bitD, cSrc, cSrcSize);
            if (ERR_isError(_var_err__))
                return _var_err__;
        }

        FSE_initDState(ref state1, ref bitD, dt);
        FSE_initDState(ref state2, ref bitD, dt);
        nuint bitD_bitContainer = bitD.bitContainer;
        uint bitD_bitsConsumed = bitD.bitsConsumed;
        sbyte* bitD_ptr = bitD.ptr;
        sbyte* bitD_start = bitD.start;
        sbyte* bitD_limitPtr = bitD.limitPtr;
        if (
            BIT_reloadDStream(
                ref bitD_bitContainer,
                ref bitD_bitsConsumed,
                ref bitD_ptr,
                bitD_start,
                bitD_limitPtr
            ) == BIT_DStream_status.BIT_DStream_overflow
        )
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        }

        for (
            ;
            BIT_reloadDStream(
                ref bitD_bitContainer,
                ref bitD_bitsConsumed,
                ref bitD_ptr,
                bitD_start,
                bitD_limitPtr
            ) == BIT_DStream_status.BIT_DStream_unfinished
                && op < olimit;
            op += 4
        )
        {
            op[0] =
                fast != 0
                    ? FSE_decodeSymbolFast(ref state1, bitD_bitContainer, ref bitD_bitsConsumed)
                    : FSE_decodeSymbol(ref state1, bitD_bitContainer, ref bitD_bitsConsumed);
            if ((14 - 2) * 2 + 7 > sizeof(nuint) * 8)
                BIT_reloadDStream(
                    ref bitD_bitContainer,
                    ref bitD_bitsConsumed,
                    ref bitD_ptr,
                    bitD_start,
                    bitD_limitPtr
                );
            op[1] =
                fast != 0
                    ? FSE_decodeSymbolFast(ref state2, bitD_bitContainer, ref bitD_bitsConsumed)
                    : FSE_decodeSymbol(ref state2, bitD_bitContainer, ref bitD_bitsConsumed);
            if ((14 - 2) * 4 + 7 > sizeof(nuint) * 8)
            {
                if (
                    BIT_reloadDStream(
                        ref bitD_bitContainer,
                        ref bitD_bitsConsumed,
                        ref bitD_ptr,
                        bitD_start,
                        bitD_limitPtr
                    ) > BIT_DStream_status.BIT_DStream_unfinished
                )
                {
                    op += 2;
                    break;
                }
            }

            op[2] =
                fast != 0
                    ? FSE_decodeSymbolFast(ref state1, bitD_bitContainer, ref bitD_bitsConsumed)
                    : FSE_decodeSymbol(ref state1, bitD_bitContainer, ref bitD_bitsConsumed);
            if ((14 - 2) * 2 + 7 > sizeof(nuint) * 8)
                BIT_reloadDStream(
                    ref bitD_bitContainer,
                    ref bitD_bitsConsumed,
                    ref bitD_ptr,
                    bitD_start,
                    bitD_limitPtr
                );
            op[3] =
                fast != 0
                    ? FSE_decodeSymbolFast(ref state2, bitD_bitContainer, ref bitD_bitsConsumed)
                    : FSE_decodeSymbol(ref state2, bitD_bitContainer, ref bitD_bitsConsumed);
        }

        while (true)
        {
            if (op > omax - 2)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            *op++ =
                fast != 0
                    ? FSE_decodeSymbolFast(ref state1, bitD_bitContainer, ref bitD_bitsConsumed)
                    : FSE_decodeSymbol(ref state1, bitD_bitContainer, ref bitD_bitsConsumed);
            if (
                BIT_reloadDStream(
                    ref bitD_bitContainer,
                    ref bitD_bitsConsumed,
                    ref bitD_ptr,
                    bitD_start,
                    bitD_limitPtr
                ) == BIT_DStream_status.BIT_DStream_overflow
            )
            {
                *op++ =
                    fast != 0
                        ? FSE_decodeSymbolFast(ref state2, bitD_bitContainer, ref bitD_bitsConsumed)
                        : FSE_decodeSymbol(ref state2, bitD_bitContainer, ref bitD_bitsConsumed);
                break;
            }

            if (op > omax - 2)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
            *op++ =
                fast != 0
                    ? FSE_decodeSymbolFast(ref state2, bitD_bitContainer, ref bitD_bitsConsumed)
                    : FSE_decodeSymbol(ref state2, bitD_bitContainer, ref bitD_bitsConsumed);
            if (
                BIT_reloadDStream(
                    ref bitD_bitContainer,
                    ref bitD_bitsConsumed,
                    ref bitD_ptr,
                    bitD_start,
                    bitD_limitPtr
                ) == BIT_DStream_status.BIT_DStream_overflow
            )
            {
                *op++ =
                    fast != 0
                        ? FSE_decodeSymbolFast(ref state1, bitD_bitContainer, ref bitD_bitsConsumed)
                        : FSE_decodeSymbol(ref state1, bitD_bitContainer, ref bitD_bitsConsumed);
                break;
            }
        }

        assert(op >= ostart);
        return (nuint)(op - ostart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint FSE_decompress_wksp_body(
        void* dst,
        nuint dstCapacity,
        void* cSrc,
        nuint cSrcSize,
        uint maxLog,
        void* workSpace,
        nuint wkspSize,
        int bmi2
    )
    {
        byte* istart = (byte*)cSrc;
        byte* ip = istart;
        uint tableLog;
        uint maxSymbolValue = 255;
        FSE_DecompressWksp* wksp = (FSE_DecompressWksp*)workSpace;
        nuint dtablePos = (nuint)(sizeof(FSE_DecompressWksp) / sizeof(uint));
        uint* dtable = (uint*)workSpace + dtablePos;
        if (wkspSize < (nuint)sizeof(FSE_DecompressWksp))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        {
            nuint NCountLength = FSE_readNCount_bmi2(
                wksp->ncount,
                &maxSymbolValue,
                &tableLog,
                istart,
                cSrcSize,
                bmi2
            );
            if (ERR_isError(NCountLength))
                return NCountLength;
            if (tableLog > maxLog)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
            assert(NCountLength <= cSrcSize);
            ip += NCountLength;
            cSrcSize -= NCountLength;
        }

        if (
            (
                (ulong)(1 + (1 << (int)tableLog) + 1)
                + (
                    sizeof(short) * (maxSymbolValue + 1)
                    + (1UL << (int)tableLog)
                    + 8
                    + sizeof(uint)
                    - 1
                ) / sizeof(uint)
                + (255 + 1) / 2
                + 1
            ) * sizeof(uint)
            > wkspSize
        )
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        assert(
            (nuint)(sizeof(FSE_DecompressWksp) + (1 + (1 << (int)tableLog)) * sizeof(uint))
                <= wkspSize
        );
        workSpace =
            (byte*)workSpace
            + sizeof(FSE_DecompressWksp)
            + (1 + (1 << (int)tableLog)) * sizeof(uint);
        wkspSize -= (nuint)(sizeof(FSE_DecompressWksp) + (1 + (1 << (int)tableLog)) * sizeof(uint));
        {
            nuint _var_err__ = FSE_buildDTable_internal(
                dtable,
                wksp->ncount,
                maxSymbolValue,
                tableLog,
                workSpace,
                wkspSize
            );
            if (ERR_isError(_var_err__))
                return _var_err__;
        }

        {
            void* ptr = dtable;
            FSE_DTableHeader* DTableH = (FSE_DTableHeader*)ptr;
            uint fastMode = DTableH->fastMode;
            if (fastMode != 0)
                return FSE_decompress_usingDTable_generic(
                    dst,
                    dstCapacity,
                    ip,
                    cSrcSize,
                    dtable,
                    1
                );
            return FSE_decompress_usingDTable_generic(dst, dstCapacity, ip, cSrcSize, dtable, 0);
        }
    }

    /* Avoids the FORCE_INLINE of the _body() function. */
    private static nuint FSE_decompress_wksp_body_default(
        void* dst,
        nuint dstCapacity,
        void* cSrc,
        nuint cSrcSize,
        uint maxLog,
        void* workSpace,
        nuint wkspSize
    )
    {
        return FSE_decompress_wksp_body(
            dst,
            dstCapacity,
            cSrc,
            cSrcSize,
            maxLog,
            workSpace,
            wkspSize,
            0
        );
    }

    private static nuint FSE_decompress_wksp_bmi2(
        void* dst,
        nuint dstCapacity,
        void* cSrc,
        nuint cSrcSize,
        uint maxLog,
        void* workSpace,
        nuint wkspSize,
        int bmi2
    )
    {
        return FSE_decompress_wksp_body_default(
            dst,
            dstCapacity,
            cSrc,
            cSrcSize,
            maxLog,
            workSpace,
            wkspSize
        );
    }
}
