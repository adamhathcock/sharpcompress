using System;
using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static DTableDesc HUF_getDTableDesc(uint* table)
    {
        DTableDesc dtd;
        memcpy(&dtd, table, (uint)sizeof(DTableDesc));
        return dtd;
    }

    private static nuint HUF_initFastDStream(byte* ip)
    {
        byte lastByte = ip[7];
        nuint bitsConsumed = lastByte != 0 ? 8 - ZSTD_highbit32(lastByte) : 0;
        nuint value = MEM_readLEST(ip) | 1;
        assert(bitsConsumed <= 8);
        assert(sizeof(nuint) == 8);
        return value << (int)bitsConsumed;
    }

    /**
     * Initializes args for the fast decoding loop.
     * @returns 1 on success
     *          0 if the fallback implementation should be used.
     *          Or an error code on failure.
     */
    private static nuint HUF_DecompressFastArgs_init(
        HUF_DecompressFastArgs* args,
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        uint* DTable
    )
    {
        void* dt = DTable + 1;
        uint dtLog = HUF_getDTableDesc(DTable).tableLog;
        byte* istart = (byte*)src;
        byte* oend = ZSTD_maybeNullPtrAdd((byte*)dst, (nint)dstSize);
        if (!BitConverter.IsLittleEndian || MEM_32bits)
            return 0;
        if (dstSize == 0)
            return 0;
        assert(dst != null);
        if (srcSize < 10)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        if (dtLog != 11)
            return 0;
        {
            nuint length1 = MEM_readLE16(istart);
            nuint length2 = MEM_readLE16(istart + 2);
            nuint length3 = MEM_readLE16(istart + 4);
            nuint length4 = srcSize - (length1 + length2 + length3 + 6);
            args->iend.e0 = istart + 6;
            args->iend.e1 = args->iend.e0 + length1;
            args->iend.e2 = args->iend.e1 + length2;
            args->iend.e3 = args->iend.e2 + length3;
            if (length1 < 8 || length2 < 8 || length3 < 8 || length4 < 8)
                return 0;
            if (length4 > srcSize)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        }

        args->ip.e0 = args->iend.e1 - sizeof(ulong);
        args->ip.e1 = args->iend.e2 - sizeof(ulong);
        args->ip.e2 = args->iend.e3 - sizeof(ulong);
        args->ip.e3 = (byte*)src + srcSize - sizeof(ulong);
        args->op.e0 = (byte*)dst;
        args->op.e1 = args->op.e0 + (dstSize + 3) / 4;
        args->op.e2 = args->op.e1 + (dstSize + 3) / 4;
        args->op.e3 = args->op.e2 + (dstSize + 3) / 4;
        if (args->op.e3 >= oend)
            return 0;
        args->bits[0] = HUF_initFastDStream(args->ip.e0);
        args->bits[1] = HUF_initFastDStream(args->ip.e1);
        args->bits[2] = HUF_initFastDStream(args->ip.e2);
        args->bits[3] = HUF_initFastDStream(args->ip.e3);
        args->ilowest = istart;
        args->oend = oend;
        args->dt = dt;
        return 1;
    }

    private static nuint HUF_initRemainingDStream(
        BIT_DStream_t* bit,
        HUF_DecompressFastArgs* args,
        int stream,
        byte* segmentEnd
    )
    {
        if ((&args->op.e0)[stream] > segmentEnd)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        if ((&args->ip.e0)[stream] < (&args->iend.e0)[stream] - 8)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        assert(sizeof(nuint) == 8);
        bit->bitContainer = MEM_readLEST((&args->ip.e0)[stream]);
        bit->bitsConsumed = ZSTD_countTrailingZeros64(args->bits[stream]);
        bit->start = (sbyte*)args->ilowest;
        bit->limitPtr = bit->start + sizeof(nuint);
        bit->ptr = (sbyte*)(&args->ip.e0)[stream];
        return 0;
    }

    /**
     * Packs 4 HUF_DEltX1 structs into a U64. This is used to lay down 4 entries at
     * a time.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HUF_DEltX1_set4(byte symbol, byte nbBits)
    {
        ulong D4;
        if (BitConverter.IsLittleEndian)
        {
            D4 = (ulong)((symbol << 8) + nbBits);
        }
        else
        {
            D4 = (ulong)(symbol + (nbBits << 8));
        }

        assert(D4 < 1U << 16);
        D4 *= 0x0001000100010001UL;
        return D4;
    }

    /**
     * Increase the tableLog to targetTableLog and rescales the stats.
     * If tableLog > targetTableLog this is a no-op.
     * @returns New tableLog
     */
    private static uint HUF_rescaleStats(
        byte* huffWeight,
        uint* rankVal,
        uint nbSymbols,
        uint tableLog,
        uint targetTableLog
    )
    {
        if (tableLog > targetTableLog)
            return tableLog;
        if (tableLog < targetTableLog)
        {
            uint scale = targetTableLog - tableLog;
            uint s;
            for (s = 0; s < nbSymbols; ++s)
            {
                huffWeight[s] += (byte)(huffWeight[s] == 0 ? 0 : scale);
            }

            for (s = targetTableLog; s > scale; --s)
            {
                rankVal[s] = rankVal[s - scale];
            }

            for (s = scale; s > 0; --s)
            {
                rankVal[s] = 0;
            }
        }

        return targetTableLog;
    }

    private static nuint HUF_readDTableX1_wksp(
        uint* DTable,
        void* src,
        nuint srcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        uint tableLog = 0;
        uint nbSymbols = 0;
        nuint iSize;
        void* dtPtr = DTable + 1;
        HUF_DEltX1* dt = (HUF_DEltX1*)dtPtr;
        HUF_ReadDTableX1_Workspace* wksp = (HUF_ReadDTableX1_Workspace*)workSpace;
        if ((nuint)sizeof(HUF_ReadDTableX1_Workspace) > wkspSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        iSize = HUF_readStats_wksp(
            wksp->huffWeight,
            255 + 1,
            wksp->rankVal,
            &nbSymbols,
            &tableLog,
            src,
            srcSize,
            wksp->statsWksp,
            sizeof(uint) * 219,
            flags
        );
        if (ERR_isError(iSize))
            return iSize;
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);
            uint maxTableLog = (uint)(dtd.maxTableLog + 1);
            uint targetTableLog = maxTableLog < 11 ? maxTableLog : 11;
            tableLog = HUF_rescaleStats(
                wksp->huffWeight,
                wksp->rankVal,
                nbSymbols,
                tableLog,
                targetTableLog
            );
            if (tableLog > (uint)(dtd.maxTableLog + 1))
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
            dtd.tableType = 0;
            dtd.tableLog = (byte)tableLog;
            memcpy(DTable, &dtd, (uint)sizeof(DTableDesc));
        }

        {
            int n;
            uint nextRankStart = 0;
            const int unroll = 4;
            int nLimit = (int)nbSymbols - unroll + 1;
            for (n = 0; n < (int)tableLog + 1; n++)
            {
                uint curr = nextRankStart;
                nextRankStart += wksp->rankVal[n];
                wksp->rankStart[n] = curr;
            }

            for (n = 0; n < nLimit; n += unroll)
            {
                int u;
                for (u = 0; u < unroll; ++u)
                {
                    nuint w = wksp->huffWeight[n + u];
                    wksp->symbols[wksp->rankStart[w]++] = (byte)(n + u);
                }
            }

            for (; n < (int)nbSymbols; ++n)
            {
                nuint w = wksp->huffWeight[n];
                wksp->symbols[wksp->rankStart[w]++] = (byte)n;
            }
        }

        {
            uint w;
            int symbol = (int)wksp->rankVal[0];
            int rankStart = 0;
            for (w = 1; w < tableLog + 1; ++w)
            {
                int symbolCount = (int)wksp->rankVal[w];
                int length = 1 << (int)w >> 1;
                int uStart = rankStart;
                byte nbBits = (byte)(tableLog + 1 - w);
                int s;
                int u;
                switch (length)
                {
                    case 1:
                        for (s = 0; s < symbolCount; ++s)
                        {
                            HUF_DEltX1 D;
                            D.@byte = wksp->symbols[symbol + s];
                            D.nbBits = nbBits;
                            dt[uStart] = D;
                            uStart += 1;
                        }

                        break;
                    case 2:
                        for (s = 0; s < symbolCount; ++s)
                        {
                            HUF_DEltX1 D;
                            D.@byte = wksp->symbols[symbol + s];
                            D.nbBits = nbBits;
                            dt[uStart + 0] = D;
                            dt[uStart + 1] = D;
                            uStart += 2;
                        }

                        break;
                    case 4:
                        for (s = 0; s < symbolCount; ++s)
                        {
                            ulong D4 = HUF_DEltX1_set4(wksp->symbols[symbol + s], nbBits);
                            MEM_write64(dt + uStart, D4);
                            uStart += 4;
                        }

                        break;
                    case 8:
                        for (s = 0; s < symbolCount; ++s)
                        {
                            ulong D4 = HUF_DEltX1_set4(wksp->symbols[symbol + s], nbBits);
                            MEM_write64(dt + uStart, D4);
                            MEM_write64(dt + uStart + 4, D4);
                            uStart += 8;
                        }

                        break;
                    default:
                        for (s = 0; s < symbolCount; ++s)
                        {
                            ulong D4 = HUF_DEltX1_set4(wksp->symbols[symbol + s], nbBits);
                            for (u = 0; u < length; u += 16)
                            {
                                MEM_write64(dt + uStart + u + 0, D4);
                                MEM_write64(dt + uStart + u + 4, D4);
                                MEM_write64(dt + uStart + u + 8, D4);
                                MEM_write64(dt + uStart + u + 12, D4);
                            }

                            assert(u == length);
                            uStart += length;
                        }

                        break;
                }

                symbol += symbolCount;
                rankStart += symbolCount * length;
            }
        }

        return iSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HUF_decodeSymbolX1(BIT_DStream_t* Dstream, HUF_DEltX1* dt, uint dtLog)
    {
        /* note : dtLog >= 1 */
        nuint val = BIT_lookBitsFast(Dstream, dtLog);
        byte c = dt[val].@byte;
        BIT_skipBits(Dstream, dt[val].nbBits);
        return c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_decodeStreamX1(
        byte* p,
        BIT_DStream_t* bitDPtr,
        byte* pEnd,
        HUF_DEltX1* dt,
        uint dtLog
    )
    {
        byte* pStart = p;
        if (pEnd - p > 3)
        {
            while (
                BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished
                && p < pEnd - 3
            )
            {
                if (MEM_64bits)
                    *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                if (MEM_64bits)
                    *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
            }
        }
        else
        {
            BIT_reloadDStream(bitDPtr);
        }

        if (MEM_32bits)
            while (
                BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished && p < pEnd
            )
                *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
        while (p < pEnd)
            *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
        return (nuint)(pEnd - pStart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_decompress1X1_usingDTable_internal_body(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable
    )
    {
        byte* op = (byte*)dst;
        byte* oend = ZSTD_maybeNullPtrAdd(op, (nint)dstSize);
        void* dtPtr = DTable + 1;
        HUF_DEltX1* dt = (HUF_DEltX1*)dtPtr;
        BIT_DStream_t bitD;
        DTableDesc dtd = HUF_getDTableDesc(DTable);
        uint dtLog = dtd.tableLog;
        {
            nuint _var_err__ = BIT_initDStream(&bitD, cSrc, cSrcSize);
            if (ERR_isError(_var_err__))
                return _var_err__;
        }

        HUF_decodeStreamX1(op, &bitD, oend, dt, dtLog);
        if (BIT_endOfDStream(&bitD) == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        return dstSize;
    }

    /* HUF_decompress4X1_usingDTable_internal_body():
     * Conditions :
     * @dstSize >= 6
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_decompress4X1_usingDTable_internal_body(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable
    )
    {
        if (cSrcSize < 10)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        if (dstSize < 6)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        {
            byte* istart = (byte*)cSrc;
            byte* ostart = (byte*)dst;
            byte* oend = ostart + dstSize;
            byte* olimit = oend - 3;
            void* dtPtr = DTable + 1;
            HUF_DEltX1* dt = (HUF_DEltX1*)dtPtr;
            /* Init */
            BIT_DStream_t bitD1;
            BIT_DStream_t bitD2;
            BIT_DStream_t bitD3;
            BIT_DStream_t bitD4;
            nuint length1 = MEM_readLE16(istart);
            nuint length2 = MEM_readLE16(istart + 2);
            nuint length3 = MEM_readLE16(istart + 4);
            nuint length4 = cSrcSize - (length1 + length2 + length3 + 6);
            /* jumpTable */
            byte* istart1 = istart + 6;
            byte* istart2 = istart1 + length1;
            byte* istart3 = istart2 + length2;
            byte* istart4 = istart3 + length3;
            nuint segmentSize = (dstSize + 3) / 4;
            byte* opStart2 = ostart + segmentSize;
            byte* opStart3 = opStart2 + segmentSize;
            byte* opStart4 = opStart3 + segmentSize;
            byte* op1 = ostart;
            byte* op2 = opStart2;
            byte* op3 = opStart3;
            byte* op4 = opStart4;
            DTableDesc dtd = HUF_getDTableDesc(DTable);
            uint dtLog = dtd.tableLog;
            uint endSignal = 1;
            if (length4 > cSrcSize)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            if (opStart4 > oend)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            assert(dstSize >= 6);
            {
                nuint _var_err__ = BIT_initDStream(&bitD1, istart1, length1);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            {
                nuint _var_err__ = BIT_initDStream(&bitD2, istart2, length2);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            {
                nuint _var_err__ = BIT_initDStream(&bitD3, istart3, length3);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            {
                nuint _var_err__ = BIT_initDStream(&bitD4, istart4, length4);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            if ((nuint)(oend - op4) >= (nuint)sizeof(nuint))
            {
                for (; (endSignal & (uint)(op4 < olimit ? 1 : 0)) != 0; )
                {
                    if (MEM_64bits)
                        *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    if (MEM_64bits)
                        *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    if (MEM_64bits)
                        *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    if (MEM_64bits)
                        *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    if (MEM_64bits)
                        *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    if (MEM_64bits)
                        *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    if (MEM_64bits)
                        *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    if (MEM_64bits)
                        *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD1) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD2) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD3) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD4) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                }
            }

            if (op1 > opStart2)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            if (op2 > opStart3)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            if (op3 > opStart4)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            HUF_decodeStreamX1(op1, &bitD1, opStart2, dt, dtLog);
            HUF_decodeStreamX1(op2, &bitD2, opStart3, dt, dtLog);
            HUF_decodeStreamX1(op3, &bitD3, opStart4, dt, dtLog);
            HUF_decodeStreamX1(op4, &bitD4, oend, dt, dtLog);
            {
                uint endCheck =
                    BIT_endOfDStream(&bitD1)
                    & BIT_endOfDStream(&bitD2)
                    & BIT_endOfDStream(&bitD3)
                    & BIT_endOfDStream(&bitD4);
                if (endCheck == 0)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            return dstSize;
        }
    }

    private static nuint HUF_decompress4X1_usingDTable_internal_default(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable
    )
    {
        return HUF_decompress4X1_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
    }

    private static void HUF_decompress4X1_usingDTable_internal_fast_c_loop(
        HUF_DecompressFastArgs* args
    )
    {
        ulong bits0,
            bits1,
            bits2,
            bits3;
        byte* ip0,
            ip1,
            ip2,
            ip3;
        byte* op0,
            op1,
            op2,
            op3;
        ushort* dtable = (ushort*)args->dt;
        byte* oend = args->oend;
        byte* ilowest = args->ilowest;
        bits0 = args->bits[0];
        bits1 = args->bits[1];
        bits2 = args->bits[2];
        bits3 = args->bits[3];
        ip0 = args->ip.e0;
        ip1 = args->ip.e1;
        ip2 = args->ip.e2;
        ip3 = args->ip.e3;
        op0 = args->op.e0;
        op1 = args->op.e1;
        op2 = args->op.e2;
        op3 = args->op.e3;
        assert(BitConverter.IsLittleEndian);
        assert(!MEM_32bits);
        for (; ; )
        {
            byte* olimit;
            {
                assert(op0 <= op1);
                assert(ip0 >= ilowest);
            }

            {
                assert(op1 <= op2);
                assert(ip1 >= ilowest);
            }

            {
                assert(op2 <= op3);
                assert(ip2 >= ilowest);
            }

            {
                assert(op3 <= oend);
                assert(ip3 >= ilowest);
            }

            {
                /* Each iteration produces 5 output symbols per stream */
                nuint oiters = (nuint)(oend - op3) / 5;
                /* Each iteration consumes up to 11 bits * 5 = 55 bits < 7 bytes
                 * per stream.
                 */
                nuint iiters = (nuint)(ip0 - ilowest) / 7;
                /* We can safely run iters iterations before running bounds checks */
                nuint iters = oiters < iiters ? oiters : iiters;
                nuint symbols = iters * 5;
                olimit = op3 + symbols;
                if (op3 == olimit)
                    break;
                {
                    if (ip1 < ip0)
                        goto _out;
                }

                {
                    if (ip2 < ip1)
                        goto _out;
                }

                {
                    if (ip3 < ip2)
                        goto _out;
                }
            }

            {
                assert(ip1 >= ip0);
            }

            {
                assert(ip2 >= ip1);
            }

            {
                assert(ip3 >= ip2);
            }

            do
            {
                {
                    {
                        /* Decode 5 symbols in each of the 4 streams */
                        int index = (int)(bits0 >> 53);
                        int entry = dtable[index];
                        bits0 <<= entry & 0x3F;
                        op0[0] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        int entry = dtable[index];
                        bits1 <<= entry & 0x3F;
                        op1[0] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        int entry = dtable[index];
                        bits2 <<= entry & 0x3F;
                        op2[0] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits3 >> 53);
                        int entry = dtable[index];
                        bits3 <<= entry & 0x3F;
                        op3[0] = (byte)(entry >> 8 & 0xFF);
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        int entry = dtable[index];
                        bits0 <<= entry & 0x3F;
                        op0[1] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        int entry = dtable[index];
                        bits1 <<= entry & 0x3F;
                        op1[1] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        int entry = dtable[index];
                        bits2 <<= entry & 0x3F;
                        op2[1] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits3 >> 53);
                        int entry = dtable[index];
                        bits3 <<= entry & 0x3F;
                        op3[1] = (byte)(entry >> 8 & 0xFF);
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        int entry = dtable[index];
                        bits0 <<= entry & 0x3F;
                        op0[2] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        int entry = dtable[index];
                        bits1 <<= entry & 0x3F;
                        op1[2] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        int entry = dtable[index];
                        bits2 <<= entry & 0x3F;
                        op2[2] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits3 >> 53);
                        int entry = dtable[index];
                        bits3 <<= entry & 0x3F;
                        op3[2] = (byte)(entry >> 8 & 0xFF);
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        int entry = dtable[index];
                        bits0 <<= entry & 0x3F;
                        op0[3] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        int entry = dtable[index];
                        bits1 <<= entry & 0x3F;
                        op1[3] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        int entry = dtable[index];
                        bits2 <<= entry & 0x3F;
                        op2[3] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits3 >> 53);
                        int entry = dtable[index];
                        bits3 <<= entry & 0x3F;
                        op3[3] = (byte)(entry >> 8 & 0xFF);
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        int entry = dtable[index];
                        bits0 <<= entry & 0x3F;
                        op0[4] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        int entry = dtable[index];
                        bits1 <<= entry & 0x3F;
                        op1[4] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        int entry = dtable[index];
                        bits2 <<= entry & 0x3F;
                        op2[4] = (byte)(entry >> 8 & 0xFF);
                    }

                    {
                        int index = (int)(bits3 >> 53);
                        int entry = dtable[index];
                        bits3 <<= entry & 0x3F;
                        op3[4] = (byte)(entry >> 8 & 0xFF);
                    }
                }

                {
                    {
                        /* Reload each of the 4 the bitstreams */
                        int ctz = (int)ZSTD_countTrailingZeros64(bits0);
                        int nbBits = ctz & 7;
                        int nbBytes = ctz >> 3;
                        op0 += 5;
                        ip0 -= nbBytes;
                        bits0 = MEM_read64(ip0) | 1;
                        bits0 <<= nbBits;
                    }

                    {
                        int ctz = (int)ZSTD_countTrailingZeros64(bits1);
                        int nbBits = ctz & 7;
                        int nbBytes = ctz >> 3;
                        op1 += 5;
                        ip1 -= nbBytes;
                        bits1 = MEM_read64(ip1) | 1;
                        bits1 <<= nbBits;
                    }

                    {
                        int ctz = (int)ZSTD_countTrailingZeros64(bits2);
                        int nbBits = ctz & 7;
                        int nbBytes = ctz >> 3;
                        op2 += 5;
                        ip2 -= nbBytes;
                        bits2 = MEM_read64(ip2) | 1;
                        bits2 <<= nbBits;
                    }

                    {
                        int ctz = (int)ZSTD_countTrailingZeros64(bits3);
                        int nbBits = ctz & 7;
                        int nbBytes = ctz >> 3;
                        op3 += 5;
                        ip3 -= nbBytes;
                        bits3 = MEM_read64(ip3) | 1;
                        bits3 <<= nbBits;
                    }
                }
            } while (op3 < olimit);
        }

        _out:
        args->bits[0] = bits0;
        args->bits[1] = bits1;
        args->bits[2] = bits2;
        args->bits[3] = bits3;
        args->ip.e0 = ip0;
        args->ip.e1 = ip1;
        args->ip.e2 = ip2;
        args->ip.e3 = ip3;
        args->op.e0 = op0;
        args->op.e1 = op1;
        args->op.e2 = op2;
        args->op.e3 = op3;
    }

    /**
     * @returns @p dstSize on success (>= 6)
     *          0 if the fallback implementation should be used
     *          An error if an error occurred
     */
    private static nuint HUF_decompress4X1_usingDTable_internal_fast(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        void* loopFn
    )
    {
        void* dt = DTable + 1;
        byte* ilowest = (byte*)cSrc;
        byte* oend = ZSTD_maybeNullPtrAdd((byte*)dst, (nint)dstSize);
        HUF_DecompressFastArgs args;
        {
            nuint ret = HUF_DecompressFastArgs_init(&args, dst, dstSize, cSrc, cSrcSize, DTable);
            {
                nuint err_code = ret;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (ret == 0)
                return 0;
        }

        assert(args.ip.e0 >= args.ilowest);
        ((delegate* managed<HUF_DecompressFastArgs*, void>)loopFn)(&args);
        assert(args.ip.e0 >= ilowest);
        assert(args.ip.e0 >= ilowest);
        assert(args.ip.e1 >= ilowest);
        assert(args.ip.e2 >= ilowest);
        assert(args.ip.e3 >= ilowest);
        assert(args.op.e3 <= oend);
        assert(ilowest == args.ilowest);
        assert(ilowest + 6 == args.iend.e0);
        {
            nuint segmentSize = (dstSize + 3) / 4;
            byte* segmentEnd = (byte*)dst;
            int i;
            for (i = 0; i < 4; ++i)
            {
                BIT_DStream_t bit;
                if (segmentSize <= (nuint)(oend - segmentEnd))
                    segmentEnd += segmentSize;
                else
                    segmentEnd = oend;
                {
                    nuint err_code = HUF_initRemainingDStream(&bit, &args, i, segmentEnd);
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                (&args.op.e0)[i] += HUF_decodeStreamX1(
                    (&args.op.e0)[i],
                    &bit,
                    segmentEnd,
                    (HUF_DEltX1*)dt,
                    11
                );
                if ((&args.op.e0)[i] != segmentEnd)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }
        }

        assert(dstSize != 0);
        return dstSize;
    }

    private static nuint HUF_decompress1X1_usingDTable_internal(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        int flags
    )
    {
        return HUF_decompress1X1_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
    }

    private static nuint HUF_decompress4X1_usingDTable_internal(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        int flags
    )
    {
        void* fallbackFn = (delegate* managed<void*, nuint, void*, nuint, uint*, nuint>)(
            &HUF_decompress4X1_usingDTable_internal_default
        );
        void* loopFn = (delegate* managed<HUF_DecompressFastArgs*, void>)(
            &HUF_decompress4X1_usingDTable_internal_fast_c_loop
        );
        if ((flags & (int)HUF_flags_e.HUF_flags_disableFast) == 0)
        {
            nuint ret = HUF_decompress4X1_usingDTable_internal_fast(
                dst,
                dstSize,
                cSrc,
                cSrcSize,
                DTable,
                loopFn
            );
            if (ret != 0)
                return ret;
        }

        return ((delegate* managed<void*, nuint, void*, nuint, uint*, nuint>)fallbackFn)(
            dst,
            dstSize,
            cSrc,
            cSrcSize,
            DTable
        );
    }

    private static nuint HUF_decompress4X1_DCtx_wksp(
        uint* dctx,
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        byte* ip = (byte*)cSrc;
        nuint hSize = HUF_readDTableX1_wksp(dctx, cSrc, cSrcSize, workSpace, wkspSize, flags);
        if (ERR_isError(hSize))
            return hSize;
        if (hSize >= cSrcSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        ip += hSize;
        cSrcSize -= hSize;
        return HUF_decompress4X1_usingDTable_internal(dst, dstSize, ip, cSrcSize, dctx, flags);
    }

    /**
     * Constructs a HUF_DEltX2 in a U32.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HUF_buildDEltX2U32(uint symbol, uint nbBits, uint baseSeq, int level)
    {
        uint seq;
        if (BitConverter.IsLittleEndian)
        {
            seq = level == 1 ? symbol : baseSeq + (symbol << 8);
            return seq + (nbBits << 16) + ((uint)level << 24);
        }
        else
        {
            seq = level == 1 ? symbol << 8 : (baseSeq << 8) + symbol;
            return (seq << 16) + (nbBits << 8) + (uint)level;
        }
    }

    /**
     * Constructs a HUF_DEltX2.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static HUF_DEltX2 HUF_buildDEltX2(uint symbol, uint nbBits, uint baseSeq, int level)
    {
        HUF_DEltX2 DElt;
        uint val = HUF_buildDEltX2U32(symbol, nbBits, baseSeq, level);
        memcpy(&DElt, &val, sizeof(uint));
        return DElt;
    }

    /**
     * Constructs 2 HUF_DEltX2s and packs them into a U64.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HUF_buildDEltX2U64(uint symbol, uint nbBits, ushort baseSeq, int level)
    {
        uint DElt = HUF_buildDEltX2U32(symbol, nbBits, baseSeq, level);
        return DElt + ((ulong)DElt << 32);
    }

    /**
     * Fills the DTable rank with all the symbols from [begin, end) that are each
     * nbBits long.
     *
     * @param DTableRank The start of the rank in the DTable.
     * @param begin The first symbol to fill (inclusive).
     * @param end The last symbol to fill (exclusive).
     * @param nbBits Each symbol is nbBits long.
     * @param tableLog The table log.
     * @param baseSeq If level == 1 { 0 } else { the first level symbol }
     * @param level The level in the table. Must be 1 or 2.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_fillDTableX2ForWeight(
        HUF_DEltX2* DTableRank,
        sortedSymbol_t* begin,
        sortedSymbol_t* end,
        uint nbBits,
        uint tableLog,
        ushort baseSeq,
        int level
    )
    {
        /* quiet static-analyzer */
        uint length = 1U << (int)(tableLog - nbBits & 0x1F);
        sortedSymbol_t* ptr;
        assert(level >= 1 && level <= 2);
        switch (length)
        {
            case 1:
                for (ptr = begin; ptr != end; ++ptr)
                {
                    HUF_DEltX2 DElt = HUF_buildDEltX2(ptr->symbol, nbBits, baseSeq, level);
                    *DTableRank++ = DElt;
                }

                break;
            case 2:
                for (ptr = begin; ptr != end; ++ptr)
                {
                    HUF_DEltX2 DElt = HUF_buildDEltX2(ptr->symbol, nbBits, baseSeq, level);
                    DTableRank[0] = DElt;
                    DTableRank[1] = DElt;
                    DTableRank += 2;
                }

                break;
            case 4:
                for (ptr = begin; ptr != end; ++ptr)
                {
                    ulong DEltX2 = HUF_buildDEltX2U64(ptr->symbol, nbBits, baseSeq, level);
                    memcpy(DTableRank + 0, &DEltX2, sizeof(ulong));
                    memcpy(DTableRank + 2, &DEltX2, sizeof(ulong));
                    DTableRank += 4;
                }

                break;
            case 8:
                for (ptr = begin; ptr != end; ++ptr)
                {
                    ulong DEltX2 = HUF_buildDEltX2U64(ptr->symbol, nbBits, baseSeq, level);
                    memcpy(DTableRank + 0, &DEltX2, sizeof(ulong));
                    memcpy(DTableRank + 2, &DEltX2, sizeof(ulong));
                    memcpy(DTableRank + 4, &DEltX2, sizeof(ulong));
                    memcpy(DTableRank + 6, &DEltX2, sizeof(ulong));
                    DTableRank += 8;
                }

                break;
            default:
                for (ptr = begin; ptr != end; ++ptr)
                {
                    ulong DEltX2 = HUF_buildDEltX2U64(ptr->symbol, nbBits, baseSeq, level);
                    HUF_DEltX2* DTableRankEnd = DTableRank + length;
                    for (; DTableRank != DTableRankEnd; DTableRank += 8)
                    {
                        memcpy(DTableRank + 0, &DEltX2, sizeof(ulong));
                        memcpy(DTableRank + 2, &DEltX2, sizeof(ulong));
                        memcpy(DTableRank + 4, &DEltX2, sizeof(ulong));
                        memcpy(DTableRank + 6, &DEltX2, sizeof(ulong));
                    }
                }

                break;
        }
    }

    /* HUF_fillDTableX2Level2() :
     * `rankValOrigin` must be a table of at least (HUF_TABLELOG_MAX + 1) U32 */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_fillDTableX2Level2(
        HUF_DEltX2* DTable,
        uint targetLog,
        uint consumedBits,
        uint* rankVal,
        int minWeight,
        int maxWeight1,
        sortedSymbol_t* sortedSymbols,
        uint* rankStart,
        uint nbBitsBaseline,
        ushort baseSeq
    )
    {
        if (minWeight > 1)
        {
            /* quiet static-analyzer */
            uint length = 1U << (int)(targetLog - consumedBits & 0x1F);
            /* baseSeq */
            ulong DEltX2 = HUF_buildDEltX2U64(baseSeq, consumedBits, 0, 1);
            int skipSize = (int)rankVal[minWeight];
            assert(length > 1);
            assert((uint)skipSize < length);
            switch (length)
            {
                case 2:
                    assert(skipSize == 1);
                    memcpy(DTable, &DEltX2, sizeof(ulong));
                    break;
                case 4:
                    assert(skipSize <= 4);
                    memcpy(DTable + 0, &DEltX2, sizeof(ulong));
                    memcpy(DTable + 2, &DEltX2, sizeof(ulong));
                    break;
                default:
                    {
                        int i;
                        for (i = 0; i < skipSize; i += 8)
                        {
                            memcpy(DTable + i + 0, &DEltX2, sizeof(ulong));
                            memcpy(DTable + i + 2, &DEltX2, sizeof(ulong));
                            memcpy(DTable + i + 4, &DEltX2, sizeof(ulong));
                            memcpy(DTable + i + 6, &DEltX2, sizeof(ulong));
                        }
                    }

                    break;
            }
        }

        {
            int w;
            for (w = minWeight; w < maxWeight1; ++w)
            {
                int begin = (int)rankStart[w];
                int end = (int)rankStart[w + 1];
                uint nbBits = nbBitsBaseline - (uint)w;
                uint totalBits = nbBits + consumedBits;
                HUF_fillDTableX2ForWeight(
                    DTable + rankVal[w],
                    sortedSymbols + begin,
                    sortedSymbols + end,
                    totalBits,
                    targetLog,
                    baseSeq,
                    2
                );
            }
        }
    }

    private static void HUF_fillDTableX2(
        HUF_DEltX2* DTable,
        uint targetLog,
        sortedSymbol_t* sortedList,
        uint* rankStart,
        rankValCol_t* rankValOrigin,
        uint maxWeight,
        uint nbBitsBaseline
    )
    {
        uint* rankVal = (uint*)&rankValOrigin[0];
        /* note : targetLog >= srcLog, hence scaleLog <= 1 */
        int scaleLog = (int)(nbBitsBaseline - targetLog);
        uint minBits = nbBitsBaseline - maxWeight;
        int w;
        int wEnd = (int)maxWeight + 1;
        for (w = 1; w < wEnd; ++w)
        {
            int begin = (int)rankStart[w];
            int end = (int)rankStart[w + 1];
            uint nbBits = nbBitsBaseline - (uint)w;
            if (targetLog - nbBits >= minBits)
            {
                /* Enough room for a second symbol. */
                int start = (int)rankVal[w];
                /* quiet static-analyzer */
                uint length = 1U << (int)(targetLog - nbBits & 0x1F);
                int minWeight = (int)(nbBits + (uint)scaleLog);
                int s;
                if (minWeight < 1)
                    minWeight = 1;
                for (s = begin; s != end; ++s)
                {
                    HUF_fillDTableX2Level2(
                        DTable + start,
                        targetLog,
                        nbBits,
                        (uint*)&rankValOrigin[nbBits],
                        minWeight,
                        wEnd,
                        sortedList,
                        rankStart,
                        nbBitsBaseline,
                        sortedList[s].symbol
                    );
                    start += (int)length;
                }
            }
            else
            {
                HUF_fillDTableX2ForWeight(
                    DTable + rankVal[w],
                    sortedList + begin,
                    sortedList + end,
                    nbBits,
                    targetLog,
                    0,
                    1
                );
            }
        }
    }

    private static nuint HUF_readDTableX2_wksp(
        uint* DTable,
        void* src,
        nuint srcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        uint tableLog,
            maxW,
            nbSymbols;
        DTableDesc dtd = HUF_getDTableDesc(DTable);
        uint maxTableLog = dtd.maxTableLog;
        nuint iSize;
        /* force compiler to avoid strict-aliasing */
        void* dtPtr = DTable + 1;
        HUF_DEltX2* dt = (HUF_DEltX2*)dtPtr;
        uint* rankStart;
        HUF_ReadDTableX2_Workspace* wksp = (HUF_ReadDTableX2_Workspace*)workSpace;
        if ((nuint)sizeof(HUF_ReadDTableX2_Workspace) > wkspSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        rankStart = wksp->rankStart0 + 1;
        memset(wksp->rankStats, 0, sizeof(uint) * 13);
        memset(wksp->rankStart0, 0, sizeof(uint) * 15);
        if (maxTableLog > 12)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        iSize = HUF_readStats_wksp(
            wksp->weightList,
            255 + 1,
            wksp->rankStats,
            &nbSymbols,
            &tableLog,
            src,
            srcSize,
            wksp->calleeWksp,
            sizeof(uint) * 219,
            flags
        );
        if (ERR_isError(iSize))
            return iSize;
        if (tableLog > maxTableLog)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        if (tableLog <= 11 && maxTableLog > 11)
            maxTableLog = 11;
        for (maxW = tableLog; wksp->rankStats[maxW] == 0; maxW--) { }

        {
            uint w,
                nextRankStart = 0;
            for (w = 1; w < maxW + 1; w++)
            {
                uint curr = nextRankStart;
                nextRankStart += wksp->rankStats[w];
                rankStart[w] = curr;
            }

            rankStart[0] = nextRankStart;
            rankStart[maxW + 1] = nextRankStart;
        }

        {
            uint s;
            for (s = 0; s < nbSymbols; s++)
            {
                uint w = wksp->weightList[s];
                uint r = rankStart[w]++;
                (&wksp->sortedSymbol.e0)[r].symbol = (byte)s;
            }

            rankStart[0] = 0;
        }

        {
            uint* rankVal0 = (uint*)&wksp->rankVal.e0;
            {
                /* tableLog <= maxTableLog */
                int rescale = (int)(maxTableLog - tableLog - 1);
                uint nextRankVal = 0;
                uint w;
                for (w = 1; w < maxW + 1; w++)
                {
                    uint curr = nextRankVal;
                    nextRankVal += wksp->rankStats[w] << (int)(w + (uint)rescale);
                    rankVal0[w] = curr;
                }
            }

            {
                uint minBits = tableLog + 1 - maxW;
                uint consumed;
                for (consumed = minBits; consumed < maxTableLog - minBits + 1; consumed++)
                {
                    uint* rankValPtr = (uint*)&(&wksp->rankVal.e0)[consumed];
                    uint w;
                    for (w = 1; w < maxW + 1; w++)
                    {
                        rankValPtr[w] = rankVal0[w] >> (int)consumed;
                    }
                }
            }
        }

        HUF_fillDTableX2(
            dt,
            maxTableLog,
            &wksp->sortedSymbol.e0,
            wksp->rankStart0,
            &wksp->rankVal.e0,
            maxW,
            tableLog + 1
        );
        dtd.tableLog = (byte)maxTableLog;
        dtd.tableType = 1;
        memcpy(DTable, &dtd, (uint)sizeof(DTableDesc));
        return iSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HUF_decodeSymbolX2(
        void* op,
        BIT_DStream_t* DStream,
        HUF_DEltX2* dt,
        uint dtLog
    )
    {
        /* note : dtLog >= 1 */
        nuint val = BIT_lookBitsFast(DStream, dtLog);
        memcpy(op, &dt[val].sequence, 2);
        BIT_skipBits(DStream, dt[val].nbBits);
        return dt[val].length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HUF_decodeLastSymbolX2(
        void* op,
        BIT_DStream_t* DStream,
        HUF_DEltX2* dt,
        uint dtLog
    )
    {
        /* note : dtLog >= 1 */
        nuint val = BIT_lookBitsFast(DStream, dtLog);
        memcpy(op, &dt[val].sequence, 1);
        if (dt[val].length == 1)
        {
            BIT_skipBits(DStream, dt[val].nbBits);
        }
        else
        {
            if (DStream->bitsConsumed < (uint)(sizeof(nuint) * 8))
            {
                BIT_skipBits(DStream, dt[val].nbBits);
                if (DStream->bitsConsumed > (uint)(sizeof(nuint) * 8))
                    DStream->bitsConsumed = (uint)(sizeof(nuint) * 8);
            }
        }

        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_decodeStreamX2(
        byte* p,
        BIT_DStream_t* bitDPtr,
        byte* pEnd,
        HUF_DEltX2* dt,
        uint dtLog
    )
    {
        byte* pStart = p;
        if ((nuint)(pEnd - p) >= (nuint)sizeof(nuint))
        {
            if (dtLog <= 11 && MEM_64bits)
            {
                while (
                    BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished
                    && p < pEnd - 9
                )
                {
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                }
            }
            else
            {
                while (
                    BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished
                    && p < pEnd - (sizeof(nuint) - 1)
                )
                {
                    if (MEM_64bits)
                        p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    if (MEM_64bits)
                        p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                    p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
                }
            }
        }
        else
        {
            BIT_reloadDStream(bitDPtr);
        }

        if ((nuint)(pEnd - p) >= 2)
        {
            while (
                BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished
                && p <= pEnd - 2
            )
                p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
            while (p <= pEnd - 2)
                p += HUF_decodeSymbolX2(p, bitDPtr, dt, dtLog);
        }

        if (p < pEnd)
            p += HUF_decodeLastSymbolX2(p, bitDPtr, dt, dtLog);
        return (nuint)(p - pStart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_decompress1X2_usingDTable_internal_body(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable
    )
    {
        BIT_DStream_t bitD;
        {
            /* Init */
            nuint _var_err__ = BIT_initDStream(&bitD, cSrc, cSrcSize);
            if (ERR_isError(_var_err__))
                return _var_err__;
        }

        {
            byte* ostart = (byte*)dst;
            byte* oend = ZSTD_maybeNullPtrAdd(ostart, (nint)dstSize);
            /* force compiler to not use strict-aliasing */
            void* dtPtr = DTable + 1;
            HUF_DEltX2* dt = (HUF_DEltX2*)dtPtr;
            DTableDesc dtd = HUF_getDTableDesc(DTable);
            HUF_decodeStreamX2(ostart, &bitD, oend, dt, dtd.tableLog);
        }

        if (BIT_endOfDStream(&bitD) == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        return dstSize;
    }

    /* HUF_decompress4X2_usingDTable_internal_body():
     * Conditions:
     * @dstSize >= 6
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_decompress4X2_usingDTable_internal_body(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable
    )
    {
        if (cSrcSize < 10)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        if (dstSize < 6)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        {
            byte* istart = (byte*)cSrc;
            byte* ostart = (byte*)dst;
            byte* oend = ostart + dstSize;
            byte* olimit = oend - (sizeof(nuint) - 1);
            void* dtPtr = DTable + 1;
            HUF_DEltX2* dt = (HUF_DEltX2*)dtPtr;
            /* Init */
            BIT_DStream_t bitD1;
            BIT_DStream_t bitD2;
            BIT_DStream_t bitD3;
            BIT_DStream_t bitD4;
            nuint length1 = MEM_readLE16(istart);
            nuint length2 = MEM_readLE16(istart + 2);
            nuint length3 = MEM_readLE16(istart + 4);
            nuint length4 = cSrcSize - (length1 + length2 + length3 + 6);
            /* jumpTable */
            byte* istart1 = istart + 6;
            byte* istart2 = istart1 + length1;
            byte* istart3 = istart2 + length2;
            byte* istart4 = istart3 + length3;
            nuint segmentSize = (dstSize + 3) / 4;
            byte* opStart2 = ostart + segmentSize;
            byte* opStart3 = opStart2 + segmentSize;
            byte* opStart4 = opStart3 + segmentSize;
            byte* op1 = ostart;
            byte* op2 = opStart2;
            byte* op3 = opStart3;
            byte* op4 = opStart4;
            uint endSignal = 1;
            DTableDesc dtd = HUF_getDTableDesc(DTable);
            uint dtLog = dtd.tableLog;
            if (length4 > cSrcSize)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            if (opStart4 > oend)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            assert(dstSize >= 6);
            {
                nuint _var_err__ = BIT_initDStream(&bitD1, istart1, length1);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            {
                nuint _var_err__ = BIT_initDStream(&bitD2, istart2, length2);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            {
                nuint _var_err__ = BIT_initDStream(&bitD3, istart3, length3);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            {
                nuint _var_err__ = BIT_initDStream(&bitD4, istart4, length4);
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            if ((nuint)(oend - op4) >= (nuint)sizeof(nuint))
            {
                for (; (endSignal & (uint)(op4 < olimit ? 1 : 0)) != 0; )
                {
                    if (MEM_64bits)
                        op1 += HUF_decodeSymbolX2(op1, &bitD1, dt, dtLog);
                    op1 += HUF_decodeSymbolX2(op1, &bitD1, dt, dtLog);
                    if (MEM_64bits)
                        op1 += HUF_decodeSymbolX2(op1, &bitD1, dt, dtLog);
                    op1 += HUF_decodeSymbolX2(op1, &bitD1, dt, dtLog);
                    if (MEM_64bits)
                        op2 += HUF_decodeSymbolX2(op2, &bitD2, dt, dtLog);
                    op2 += HUF_decodeSymbolX2(op2, &bitD2, dt, dtLog);
                    if (MEM_64bits)
                        op2 += HUF_decodeSymbolX2(op2, &bitD2, dt, dtLog);
                    op2 += HUF_decodeSymbolX2(op2, &bitD2, dt, dtLog);
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD1) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD2) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                    if (MEM_64bits)
                        op3 += HUF_decodeSymbolX2(op3, &bitD3, dt, dtLog);
                    op3 += HUF_decodeSymbolX2(op3, &bitD3, dt, dtLog);
                    if (MEM_64bits)
                        op3 += HUF_decodeSymbolX2(op3, &bitD3, dt, dtLog);
                    op3 += HUF_decodeSymbolX2(op3, &bitD3, dt, dtLog);
                    if (MEM_64bits)
                        op4 += HUF_decodeSymbolX2(op4, &bitD4, dt, dtLog);
                    op4 += HUF_decodeSymbolX2(op4, &bitD4, dt, dtLog);
                    if (MEM_64bits)
                        op4 += HUF_decodeSymbolX2(op4, &bitD4, dt, dtLog);
                    op4 += HUF_decodeSymbolX2(op4, &bitD4, dt, dtLog);
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD3) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                    endSignal &=
                        BIT_reloadDStreamFast(&bitD4) == BIT_DStream_status.BIT_DStream_unfinished
                            ? 1U
                            : 0U;
                }
            }

            if (op1 > opStart2)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            if (op2 > opStart3)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            if (op3 > opStart4)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            HUF_decodeStreamX2(op1, &bitD1, opStart2, dt, dtLog);
            HUF_decodeStreamX2(op2, &bitD2, opStart3, dt, dtLog);
            HUF_decodeStreamX2(op3, &bitD3, opStart4, dt, dtLog);
            HUF_decodeStreamX2(op4, &bitD4, oend, dt, dtLog);
            {
                uint endCheck =
                    BIT_endOfDStream(&bitD1)
                    & BIT_endOfDStream(&bitD2)
                    & BIT_endOfDStream(&bitD3)
                    & BIT_endOfDStream(&bitD4);
                if (endCheck == 0)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }

            return dstSize;
        }
    }

    private static nuint HUF_decompress4X2_usingDTable_internal_default(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable
    )
    {
        return HUF_decompress4X2_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
    }

    private static void HUF_decompress4X2_usingDTable_internal_fast_c_loop(
        HUF_DecompressFastArgs* args
    )
    {
        ulong bits0,
            bits1,
            bits2,
            bits3;
        byte* ip0,
            ip1,
            ip2,
            ip3;
        byte* op0,
            op1,
            op2,
            op3;
        byte* oend0,
            oend1,
            oend2,
            oend3;
        HUF_DEltX2* dtable = (HUF_DEltX2*)args->dt;
        byte* ilowest = args->ilowest;
        bits0 = args->bits[0];
        bits1 = args->bits[1];
        bits2 = args->bits[2];
        bits3 = args->bits[3];
        ip0 = args->ip.e0;
        ip1 = args->ip.e1;
        ip2 = args->ip.e2;
        ip3 = args->ip.e3;
        op0 = args->op.e0;
        op1 = args->op.e1;
        op2 = args->op.e2;
        op3 = args->op.e3;
        oend0 = op1;
        oend1 = op2;
        oend2 = op3;
        oend3 = args->oend;
        assert(BitConverter.IsLittleEndian);
        assert(!MEM_32bits);
        for (; ; )
        {
            byte* olimit;
            {
                assert(op0 <= oend0);
                assert(ip0 >= ilowest);
            }

            {
                assert(op1 <= oend1);
                assert(ip1 >= ilowest);
            }

            {
                assert(op2 <= oend2);
                assert(ip2 >= ilowest);
            }

            {
                assert(op3 <= oend3);
                assert(ip3 >= ilowest);
            }

            {
                /* Each loop does 5 table lookups for each of the 4 streams.
                 * Each table lookup consumes up to 11 bits of input, and produces
                 * up to 2 bytes of output.
                 */
                /* We can consume up to 7 bytes of input per iteration per stream.
                 * We also know that each input pointer is >= ip[0]. So we can run
                 * iters loops before running out of input.
                 */
                nuint iters = (nuint)(ip0 - ilowest) / 7;
                {
                    nuint oiters = (nuint)(oend0 - op0) / 10;
                    iters = iters < oiters ? iters : oiters;
                }

                {
                    nuint oiters = (nuint)(oend1 - op1) / 10;
                    iters = iters < oiters ? iters : oiters;
                }

                {
                    nuint oiters = (nuint)(oend2 - op2) / 10;
                    iters = iters < oiters ? iters : oiters;
                }

                {
                    nuint oiters = (nuint)(oend3 - op3) / 10;
                    iters = iters < oiters ? iters : oiters;
                }

                olimit = op3 + iters * 5;
                if (op3 == olimit)
                    break;
                {
                    if (ip1 < ip0)
                        goto _out;
                }

                {
                    if (ip2 < ip1)
                        goto _out;
                }

                {
                    if (ip3 < ip2)
                        goto _out;
                }
            }

            {
                assert(ip1 >= ip0);
            }

            {
                assert(ip2 >= ip1);
            }

            {
                assert(ip3 >= ip2);
            }

            do
            {
                {
                    {
                        /* Decode 5 symbols from each of the first 3 streams.
                         * The final stream will be decoded during the reload phase
                         * to reduce register pressure.
                         */
                        int index = (int)(bits0 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op0, entry.sequence);
                        bits0 <<= entry.nbBits & 0x3F;
                        op0 += entry.length;
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op1, entry.sequence);
                        bits1 <<= entry.nbBits & 0x3F;
                        op1 += entry.length;
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op2, entry.sequence);
                        bits2 <<= entry.nbBits & 0x3F;
                        op2 += entry.length;
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op0, entry.sequence);
                        bits0 <<= entry.nbBits & 0x3F;
                        op0 += entry.length;
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op1, entry.sequence);
                        bits1 <<= entry.nbBits & 0x3F;
                        op1 += entry.length;
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op2, entry.sequence);
                        bits2 <<= entry.nbBits & 0x3F;
                        op2 += entry.length;
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op0, entry.sequence);
                        bits0 <<= entry.nbBits & 0x3F;
                        op0 += entry.length;
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op1, entry.sequence);
                        bits1 <<= entry.nbBits & 0x3F;
                        op1 += entry.length;
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op2, entry.sequence);
                        bits2 <<= entry.nbBits & 0x3F;
                        op2 += entry.length;
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op0, entry.sequence);
                        bits0 <<= entry.nbBits & 0x3F;
                        op0 += entry.length;
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op1, entry.sequence);
                        bits1 <<= entry.nbBits & 0x3F;
                        op1 += entry.length;
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op2, entry.sequence);
                        bits2 <<= entry.nbBits & 0x3F;
                        op2 += entry.length;
                    }
                }

                {
                    {
                        int index = (int)(bits0 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op0, entry.sequence);
                        bits0 <<= entry.nbBits & 0x3F;
                        op0 += entry.length;
                    }

                    {
                        int index = (int)(bits1 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op1, entry.sequence);
                        bits1 <<= entry.nbBits & 0x3F;
                        op1 += entry.length;
                    }

                    {
                        int index = (int)(bits2 >> 53);
                        HUF_DEltX2 entry = dtable[index];
                        MEM_write16(op2, entry.sequence);
                        bits2 <<= entry.nbBits & 0x3F;
                        op2 += entry.length;
                    }
                }

                {
                    /* Decode one symbol from the final stream */
                    int index = (int)(bits3 >> 53);
                    HUF_DEltX2 entry = dtable[index];
                    MEM_write16(op3, entry.sequence);
                    bits3 <<= entry.nbBits & 0x3F;
                    op3 += entry.length;
                }

                {
                    {
                        {
                            /* Decode 4 symbols from the final stream & reload bitstreams.
                             * The final stream is reloaded last, meaning that all 5 symbols
                             * are decoded from the final stream before it is reloaded.
                             */
                            int index = (int)(bits3 >> 53);
                            HUF_DEltX2 entry = dtable[index];
                            MEM_write16(op3, entry.sequence);
                            bits3 <<= entry.nbBits & 0x3F;
                            op3 += entry.length;
                        }

                        {
                            int ctz = (int)ZSTD_countTrailingZeros64(bits0);
                            int nbBits = ctz & 7;
                            int nbBytes = ctz >> 3;
                            ip0 -= nbBytes;
                            bits0 = MEM_read64(ip0) | 1;
                            bits0 <<= nbBits;
                        }
                    }

                    {
                        {
                            int index = (int)(bits3 >> 53);
                            HUF_DEltX2 entry = dtable[index];
                            MEM_write16(op3, entry.sequence);
                            bits3 <<= entry.nbBits & 0x3F;
                            op3 += entry.length;
                        }

                        {
                            int ctz = (int)ZSTD_countTrailingZeros64(bits1);
                            int nbBits = ctz & 7;
                            int nbBytes = ctz >> 3;
                            ip1 -= nbBytes;
                            bits1 = MEM_read64(ip1) | 1;
                            bits1 <<= nbBits;
                        }
                    }

                    {
                        {
                            int index = (int)(bits3 >> 53);
                            HUF_DEltX2 entry = dtable[index];
                            MEM_write16(op3, entry.sequence);
                            bits3 <<= entry.nbBits & 0x3F;
                            op3 += entry.length;
                        }

                        {
                            int ctz = (int)ZSTD_countTrailingZeros64(bits2);
                            int nbBits = ctz & 7;
                            int nbBytes = ctz >> 3;
                            ip2 -= nbBytes;
                            bits2 = MEM_read64(ip2) | 1;
                            bits2 <<= nbBits;
                        }
                    }

                    {
                        {
                            int index = (int)(bits3 >> 53);
                            HUF_DEltX2 entry = dtable[index];
                            MEM_write16(op3, entry.sequence);
                            bits3 <<= entry.nbBits & 0x3F;
                            op3 += entry.length;
                        }

                        {
                            int ctz = (int)ZSTD_countTrailingZeros64(bits3);
                            int nbBits = ctz & 7;
                            int nbBytes = ctz >> 3;
                            ip3 -= nbBytes;
                            bits3 = MEM_read64(ip3) | 1;
                            bits3 <<= nbBits;
                        }
                    }
                }
            } while (op3 < olimit);
        }

        _out:
        args->bits[0] = bits0;
        args->bits[1] = bits1;
        args->bits[2] = bits2;
        args->bits[3] = bits3;
        args->ip.e0 = ip0;
        args->ip.e1 = ip1;
        args->ip.e2 = ip2;
        args->ip.e3 = ip3;
        args->op.e0 = op0;
        args->op.e1 = op1;
        args->op.e2 = op2;
        args->op.e3 = op3;
    }

    private static nuint HUF_decompress4X2_usingDTable_internal_fast(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        void* loopFn
    )
    {
        void* dt = DTable + 1;
        byte* ilowest = (byte*)cSrc;
        byte* oend = ZSTD_maybeNullPtrAdd((byte*)dst, (nint)dstSize);
        HUF_DecompressFastArgs args;
        {
            nuint ret = HUF_DecompressFastArgs_init(&args, dst, dstSize, cSrc, cSrcSize, DTable);
            {
                nuint err_code = ret;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (ret == 0)
                return 0;
        }

        assert(args.ip.e0 >= args.ilowest);
        ((delegate* managed<HUF_DecompressFastArgs*, void>)loopFn)(&args);
        assert(args.ip.e0 >= ilowest);
        assert(args.ip.e1 >= ilowest);
        assert(args.ip.e2 >= ilowest);
        assert(args.ip.e3 >= ilowest);
        assert(args.op.e3 <= oend);
        assert(ilowest == args.ilowest);
        assert(ilowest + 6 == args.iend.e0);
        {
            nuint segmentSize = (dstSize + 3) / 4;
            byte* segmentEnd = (byte*)dst;
            int i;
            for (i = 0; i < 4; ++i)
            {
                BIT_DStream_t bit;
                if (segmentSize <= (nuint)(oend - segmentEnd))
                    segmentEnd += segmentSize;
                else
                    segmentEnd = oend;
                {
                    nuint err_code = HUF_initRemainingDStream(&bit, &args, i, segmentEnd);
                    if (ERR_isError(err_code))
                    {
                        return err_code;
                    }
                }

                (&args.op.e0)[i] += HUF_decodeStreamX2(
                    (&args.op.e0)[i],
                    &bit,
                    segmentEnd,
                    (HUF_DEltX2*)dt,
                    11
                );
                if ((&args.op.e0)[i] != segmentEnd)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            }
        }

        return dstSize;
    }

    private static nuint HUF_decompress4X2_usingDTable_internal(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        int flags
    )
    {
        void* fallbackFn = (delegate* managed<void*, nuint, void*, nuint, uint*, nuint>)(
            &HUF_decompress4X2_usingDTable_internal_default
        );
        void* loopFn = (delegate* managed<HUF_DecompressFastArgs*, void>)(
            &HUF_decompress4X2_usingDTable_internal_fast_c_loop
        );
        if ((flags & (int)HUF_flags_e.HUF_flags_disableFast) == 0)
        {
            nuint ret = HUF_decompress4X2_usingDTable_internal_fast(
                dst,
                dstSize,
                cSrc,
                cSrcSize,
                DTable,
                loopFn
            );
            if (ret != 0)
                return ret;
        }

        return ((delegate* managed<void*, nuint, void*, nuint, uint*, nuint>)fallbackFn)(
            dst,
            dstSize,
            cSrc,
            cSrcSize,
            DTable
        );
    }

    private static nuint HUF_decompress1X2_usingDTable_internal(
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        int flags
    )
    {
        return HUF_decompress1X2_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
    }

    private static nuint HUF_decompress1X2_DCtx_wksp(
        uint* DCtx,
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        byte* ip = (byte*)cSrc;
        nuint hSize = HUF_readDTableX2_wksp(DCtx, cSrc, cSrcSize, workSpace, wkspSize, flags);
        if (ERR_isError(hSize))
            return hSize;
        if (hSize >= cSrcSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        ip += hSize;
        cSrcSize -= hSize;
        return HUF_decompress1X2_usingDTable_internal(dst, dstSize, ip, cSrcSize, DCtx, flags);
    }

    private static nuint HUF_decompress4X2_DCtx_wksp(
        uint* dctx,
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        byte* ip = (byte*)cSrc;
        nuint hSize = HUF_readDTableX2_wksp(dctx, cSrc, cSrcSize, workSpace, wkspSize, flags);
        if (ERR_isError(hSize))
            return hSize;
        if (hSize >= cSrcSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        ip += hSize;
        cSrcSize -= hSize;
        return HUF_decompress4X2_usingDTable_internal(dst, dstSize, ip, cSrcSize, dctx, flags);
    }

    private static readonly algo_time_t[][] algoTime = new algo_time_t[16][]
    {
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 0, decode256Time: 0),
            new algo_time_t(tableTime: 1, decode256Time: 1),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 0, decode256Time: 0),
            new algo_time_t(tableTime: 1, decode256Time: 1),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 150, decode256Time: 216),
            new algo_time_t(tableTime: 381, decode256Time: 119),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 170, decode256Time: 205),
            new algo_time_t(tableTime: 514, decode256Time: 112),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 177, decode256Time: 199),
            new algo_time_t(tableTime: 539, decode256Time: 110),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 197, decode256Time: 194),
            new algo_time_t(tableTime: 644, decode256Time: 107),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 221, decode256Time: 192),
            new algo_time_t(tableTime: 735, decode256Time: 107),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 256, decode256Time: 189),
            new algo_time_t(tableTime: 881, decode256Time: 106),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 359, decode256Time: 188),
            new algo_time_t(tableTime: 1167, decode256Time: 109),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 582, decode256Time: 187),
            new algo_time_t(tableTime: 1570, decode256Time: 114),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 688, decode256Time: 187),
            new algo_time_t(tableTime: 1712, decode256Time: 122),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 825, decode256Time: 186),
            new algo_time_t(tableTime: 1965, decode256Time: 136),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 976, decode256Time: 185),
            new algo_time_t(tableTime: 2131, decode256Time: 150),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 1180, decode256Time: 186),
            new algo_time_t(tableTime: 2070, decode256Time: 175),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 1377, decode256Time: 185),
            new algo_time_t(tableTime: 1731, decode256Time: 202),
        },
        new algo_time_t[2]
        {
            new algo_time_t(tableTime: 1412, decode256Time: 185),
            new algo_time_t(tableTime: 1695, decode256Time: 202),
        },
    };

    /** HUF_selectDecoder() :
     *  Tells which decoder is likely to decode faster,
     *  based on a set of pre-computed metrics.
     * @return : 0==HUF_decompress4X1, 1==HUF_decompress4X2 .
     *  Assumption : 0 < dstSize <= 128 KB */
    private static uint HUF_selectDecoder(nuint dstSize, nuint cSrcSize)
    {
        assert(dstSize > 0);
        assert(dstSize <= 128 * 1024);
        {
            /* Q < 16 */
            uint Q = cSrcSize >= dstSize ? 15 : (uint)(cSrcSize * 16 / dstSize);
            uint D256 = (uint)(dstSize >> 8);
            uint DTime0 = algoTime[Q][0].tableTime + algoTime[Q][0].decode256Time * D256;
            uint DTime1 = algoTime[Q][1].tableTime + algoTime[Q][1].decode256Time * D256;
            DTime1 += DTime1 >> 5;
            return DTime1 < DTime0 ? 1U : 0U;
        }
    }

    private static nuint HUF_decompress1X_DCtx_wksp(
        uint* dctx,
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        if (dstSize == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        if (cSrcSize > dstSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        if (cSrcSize == dstSize)
        {
            memcpy(dst, cSrc, (uint)dstSize);
            return dstSize;
        }

        if (cSrcSize == 1)
        {
            memset(dst, *(byte*)cSrc, (uint)dstSize);
            return dstSize;
        }

        {
            uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);
            return algoNb != 0
                ? HUF_decompress1X2_DCtx_wksp(
                    dctx,
                    dst,
                    dstSize,
                    cSrc,
                    cSrcSize,
                    workSpace,
                    wkspSize,
                    flags
                )
                : HUF_decompress1X1_DCtx_wksp(
                    dctx,
                    dst,
                    dstSize,
                    cSrc,
                    cSrcSize,
                    workSpace,
                    wkspSize,
                    flags
                );
        }
    }

    /* BMI2 variants.
     * If the CPU has BMI2 support, pass bmi2=1, otherwise pass bmi2=0.
     */
    private static nuint HUF_decompress1X_usingDTable(
        void* dst,
        nuint maxDstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        int flags
    )
    {
        DTableDesc dtd = HUF_getDTableDesc(DTable);
        return dtd.tableType != 0
            ? HUF_decompress1X2_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, flags)
            : HUF_decompress1X1_usingDTable_internal(
                dst,
                maxDstSize,
                cSrc,
                cSrcSize,
                DTable,
                flags
            );
    }

    private static nuint HUF_decompress1X1_DCtx_wksp(
        uint* dctx,
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        byte* ip = (byte*)cSrc;
        nuint hSize = HUF_readDTableX1_wksp(dctx, cSrc, cSrcSize, workSpace, wkspSize, flags);
        if (ERR_isError(hSize))
            return hSize;
        if (hSize >= cSrcSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        ip += hSize;
        cSrcSize -= hSize;
        return HUF_decompress1X1_usingDTable_internal(dst, dstSize, ip, cSrcSize, dctx, flags);
    }

    private static nuint HUF_decompress4X_usingDTable(
        void* dst,
        nuint maxDstSize,
        void* cSrc,
        nuint cSrcSize,
        uint* DTable,
        int flags
    )
    {
        DTableDesc dtd = HUF_getDTableDesc(DTable);
        return dtd.tableType != 0
            ? HUF_decompress4X2_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, flags)
            : HUF_decompress4X1_usingDTable_internal(
                dst,
                maxDstSize,
                cSrc,
                cSrcSize,
                DTable,
                flags
            );
    }

    private static nuint HUF_decompress4X_hufOnly_wksp(
        uint* dctx,
        void* dst,
        nuint dstSize,
        void* cSrc,
        nuint cSrcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        if (dstSize == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        if (cSrcSize == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        {
            uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);
            return algoNb != 0
                ? HUF_decompress4X2_DCtx_wksp(
                    dctx,
                    dst,
                    dstSize,
                    cSrc,
                    cSrcSize,
                    workSpace,
                    wkspSize,
                    flags
                )
                : HUF_decompress4X1_DCtx_wksp(
                    dctx,
                    dst,
                    dstSize,
                    cSrc,
                    cSrcSize,
                    workSpace,
                    wkspSize,
                    flags
                );
        }
    }
}
