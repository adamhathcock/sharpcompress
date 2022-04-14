using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        private static DTableDesc HUF_getDTableDesc(uint* table)
        {
            DTableDesc dtd;

            memcpy((void*)(&dtd), (void*)(table), ((nuint)(sizeof(DTableDesc))));
            return dtd;
        }

        /**
         * Packs 4 HUF_DEltX1 structs into a U64. This is used to lay down 4 entries at
         * a time.
         */
        [InlineMethod.Inline]
        private static ulong HUF_DEltX1_set4(byte symbol, byte nbBits)
        {
            ulong D4;

            if (BitConverter.IsLittleEndian)
            {
                D4 = (ulong)(symbol + (nbBits << 8));
            }
            else
            {
                D4 = (ulong)((symbol << 8) + nbBits);
            }

            D4 *= 0x0001000100010001UL;
            return D4;
        }

        public static nuint HUF_readDTableX1_wksp(uint* DTable, void* src, nuint srcSize, void* workSpace, nuint wkspSize)
        {
            return HUF_readDTableX1_wksp_bmi2(DTable, src, srcSize, workSpace, wkspSize, 0);
        }

        public static nuint HUF_readDTableX1_wksp_bmi2(uint* DTable, void* src, nuint srcSize, void* workSpace, nuint wkspSize, int bmi2)
        {
            uint tableLog = 0;
            uint nbSymbols = 0;
            nuint iSize;
            void* dtPtr = (void*)(DTable + 1);
            HUF_DEltX1* dt = (HUF_DEltX1*)(dtPtr);
            HUF_ReadDTableX1_Workspace* wksp = (HUF_ReadDTableX1_Workspace*)(workSpace);

            if ((nuint)(sizeof(HUF_ReadDTableX1_Workspace)) > wkspSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            iSize = HUF_readStats_wksp((byte*)wksp->huffWeight, (nuint)(255 + 1), (uint*)wksp->rankVal, &nbSymbols, &tableLog, src, srcSize, (void*)wksp->statsWksp, (nuint)(sizeof(uint) * 218), bmi2);
            if ((ERR_isError(iSize)) != 0)
            {
                return iSize;
            }


            {
                DTableDesc dtd = HUF_getDTableDesc(DTable);

                if (tableLog > (uint)(dtd.maxTableLog + 1))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
                }

                dtd.tableType = 0;
                dtd.tableLog = (byte)(tableLog);
                memcpy((void*)(DTable), (void*)(&dtd), ((nuint)(sizeof(DTableDesc))));
            }


            {
                int n;
                int nextRankStart = 0;
                int unroll = 4;
                int nLimit = (int)(nbSymbols) - unroll + 1;

                for (n = 0; n < (int)(tableLog) + 1; n++)
                {
                    uint curr = (uint)nextRankStart;

                    nextRankStart += (int)((int)(wksp->rankVal[n]));
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

                for (; n < (int)(nbSymbols); ++n)
                {
                    nuint w = wksp->huffWeight[n];

                    wksp->symbols[wksp->rankStart[w]++] = (byte)(n);
                }
            }


            {
                uint w;
                int symbol = (int)(wksp->rankVal[0]);
                int rankStart = 0;

                for (w = 1; w < tableLog + 1; ++w)
                {
                    int symbolCount = (int)(wksp->rankVal[w]);
                    int length = (1 << (int)w) >> 1;
                    int uStart = rankStart;
                    byte nbBits = (byte)(tableLog + 1 - w);
                    int s;
                    int u;

                    switch (length)
                    {
                        case 1:
                        {
                            for (s = 0; s < symbolCount; ++s)
                            {
                                HUF_DEltX1 D;

                                D.@byte = wksp->symbols[symbol + s];
                                D.nbBits = nbBits;
                                dt[uStart] = D;
                                uStart += 1;
                            }
                        }

                        break;
                        case 2:
                        {
                            for (s = 0; s < symbolCount; ++s)
                            {
                                HUF_DEltX1 D;

                                D.@byte = wksp->symbols[symbol + s];
                                D.nbBits = nbBits;
                                dt[uStart + 0] = D;
                                dt[uStart + 1] = D;
                                uStart += 2;
                            }
                        }

                        break;
                        case 4:
                        {
                            for (s = 0; s < symbolCount; ++s)
                            {
                                ulong D4 = HUF_DEltX1_set4(wksp->symbols[symbol + s], nbBits);

                                MEM_write64((void*)(dt + uStart), D4);
                                uStart += 4;
                            }
                        }

                        break;
                        case 8:
                        {
                            for (s = 0; s < symbolCount; ++s)
                            {
                                ulong D4 = HUF_DEltX1_set4(wksp->symbols[symbol + s], nbBits);

                                MEM_write64((void*)(dt + uStart), D4);
                                MEM_write64((void*)(dt + uStart + 4), D4);
                                uStart += 8;
                            }
                        }

                        break;
                        default:
                        {
                            for (s = 0; s < symbolCount; ++s)
                            {
                                ulong D4 = HUF_DEltX1_set4(wksp->symbols[symbol + s], nbBits);

                                for (u = 0; u < length; u += 16)
                                {
                                    MEM_write64((void*)(dt + uStart + u + 0), D4);
                                    MEM_write64((void*)(dt + uStart + u + 4), D4);
                                    MEM_write64((void*)(dt + uStart + u + 8), D4);
                                    MEM_write64((void*)(dt + uStart + u + 12), D4);
                                }

                                assert(u == length);
                                uStart += length;
                            }
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
            nuint val = BIT_lookBitsFast(Dstream, dtLog);
            byte c = dt[val].@byte;

            BIT_skipBits(Dstream, dt[val].nbBits);
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_decodeStreamX1(byte* p, BIT_DStream_t* bitDPtr, byte* pEnd, HUF_DEltX1* dt, uint dtLog)
        {
            byte* pStart = p;

            while (((BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished) && (p < pEnd - 3)))
            {
                if (MEM_64bits)
                {
                    *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                }

                if (MEM_64bits || (12 <= 12))
                {
                    *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                }

                if (MEM_64bits)
                {
                    *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                }

                *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
            }

            if (MEM_32bits)
            {
                while (((BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished) && (p < pEnd)))
                {
                    *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
                }
            }

            while (p < pEnd)
            {
                *p++ = HUF_decodeSymbolX1(bitDPtr, dt, dtLog);
            }

            return (nuint)(pEnd - pStart);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_decompress1X1_usingDTable_internal_body(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            byte* op = (byte*)(dst);
            byte* oend = op + dstSize;
            void* dtPtr = (void*)(DTable + 1);
            HUF_DEltX1* dt = (HUF_DEltX1*)(dtPtr);
            BIT_DStream_t bitD;
            DTableDesc dtd = HUF_getDTableDesc(DTable);
            uint dtLog = dtd.tableLog;


            {
                nuint _var_err__ = BIT_initDStream(&bitD, cSrc, cSrcSize);

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }

            HUF_decodeStreamX1(op, &bitD, oend, dt, dtLog);
            if ((BIT_endOfDStream(&bitD)) == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            return dstSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_decompress4X1_usingDTable_internal_body(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            if (cSrcSize < 10)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }


            {
                byte* istart = (byte*)(cSrc);
                byte* ostart = (byte*)(dst);
                byte* oend = ostart + dstSize;
                byte* olimit = oend - 3;
                void* dtPtr = (void*)(DTable + 1);
                HUF_DEltX1* dt = (HUF_DEltX1*)(dtPtr);
                BIT_DStream_t bitD1;
                BIT_DStream_t bitD2;
                BIT_DStream_t bitD3;
                BIT_DStream_t bitD4;
                nuint length1 = MEM_readLE16((void*)istart);
                nuint length2 = MEM_readLE16((void*)(istart + 2));
                nuint length3 = MEM_readLE16((void*)(istart + 4));
                nuint length4 = cSrcSize - (length1 + length2 + length3 + 6);
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
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD1, (void*)istart1, length1);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD2, (void*)istart2, length2);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD3, (void*)istart3, length3);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD4, (void*)istart4, length4);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }

                for (; ((endSignal) & (uint)((((op4 < olimit)) ? 1 : 0))) != 0;)
                {
                    if (MEM_64bits)
                    {
                        *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    }

                    *op1++ = HUF_decodeSymbolX1(&bitD1, dt, dtLog);
                    *op2++ = HUF_decodeSymbolX1(&bitD2, dt, dtLog);
                    *op3++ = HUF_decodeSymbolX1(&bitD3, dt, dtLog);
                    *op4++ = HUF_decodeSymbolX1(&bitD4, dt, dtLog);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD1) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD2) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD3) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD4) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                }

                if (op1 > opStart2)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                if (op2 > opStart3)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                if (op3 > opStart4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                HUF_decodeStreamX1(op1, &bitD1, opStart2, dt, dtLog);
                HUF_decodeStreamX1(op2, &bitD2, opStart3, dt, dtLog);
                HUF_decodeStreamX1(op3, &bitD3, opStart4, dt, dtLog);
                HUF_decodeStreamX1(op4, &bitD4, oend, dt, dtLog);

                {
                    uint endCheck = BIT_endOfDStream(&bitD1) & BIT_endOfDStream(&bitD2) & BIT_endOfDStream(&bitD3) & BIT_endOfDStream(&bitD4);

                    if (endCheck == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }
                }

                return dstSize;
            }
        }

        private static nuint HUF_decompress1X1_usingDTable_internal_default(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress1X1_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress1X1_usingDTable_internal_bmi2(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress1X1_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress1X1_usingDTable_internal(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable, int bmi2)
        {
            if (bmi2 != 0)
            {
                return HUF_decompress1X1_usingDTable_internal_bmi2(dst, dstSize, cSrc, cSrcSize, DTable);
            }

            return HUF_decompress1X1_usingDTable_internal_default(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress4X1_usingDTable_internal_default(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress4X1_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress4X1_usingDTable_internal_bmi2(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress4X1_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress4X1_usingDTable_internal(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable, int bmi2)
        {
            if (bmi2 != 0)
            {
                return HUF_decompress4X1_usingDTable_internal_bmi2(dst, dstSize, cSrc, cSrcSize, DTable);
            }

            return HUF_decompress4X1_usingDTable_internal_default(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        public static nuint HUF_decompress1X1_usingDTable(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            if (dtd.tableType != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            return HUF_decompress1X1_usingDTable_internal(dst, dstSize, cSrc, cSrcSize, DTable, 0);
        }

        public static nuint HUF_decompress1X1_DCtx_wksp(uint* DCtx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize)
        {
            byte* ip = (byte*)(cSrc);
            nuint hSize = HUF_readDTableX1_wksp(DCtx, cSrc, cSrcSize, workSpace, wkspSize);

            if ((ERR_isError(hSize)) != 0)
            {
                return hSize;
            }

            if (hSize >= cSrcSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            ip += hSize;
            cSrcSize -= hSize;
            return HUF_decompress1X1_usingDTable_internal(dst, dstSize, (void*)ip, cSrcSize, DCtx, 0);
        }

        public static nuint HUF_decompress4X1_usingDTable(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            if (dtd.tableType != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            return HUF_decompress4X1_usingDTable_internal(dst, dstSize, cSrc, cSrcSize, DTable, 0);
        }

        private static nuint HUF_decompress4X1_DCtx_wksp_bmi2(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize, int bmi2)
        {
            byte* ip = (byte*)(cSrc);
            nuint hSize = HUF_readDTableX1_wksp_bmi2(dctx, cSrc, cSrcSize, workSpace, wkspSize, bmi2);

            if ((ERR_isError(hSize)) != 0)
            {
                return hSize;
            }

            if (hSize >= cSrcSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            ip += hSize;
            cSrcSize -= hSize;
            return HUF_decompress4X1_usingDTable_internal(dst, dstSize, (void*)ip, cSrcSize, dctx, bmi2);
        }

        public static nuint HUF_decompress4X1_DCtx_wksp(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize)
        {
            return HUF_decompress4X1_DCtx_wksp_bmi2(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize, 0);
        }

        /* HUF_fillDTableX2Level2() :
         * `rankValOrigin` must be a table of at least (HUF_TABLELOG_MAX + 1) U32 */
        [InlineMethod.Inline]
        private static void HUF_fillDTableX2Level2(HUF_DEltX2* DTable, uint sizeLog, uint consumed, uint* rankValOrigin, int minWeight, sortedSymbol_t* sortedSymbols, uint sortedListSize, uint nbBitsBaseline, ushort baseSeq, uint* wksp, nuint wkspSize)
        {
            HUF_DEltX2 DElt;
            uint* rankVal = wksp;

            assert(wkspSize >= (uint)(12 + 1));
            memcpy((void*)(rankVal), (void*)(rankValOrigin), ((nuint)(sizeof(uint)) * (uint)((12 + 1))));
            if (minWeight > 1)
            {
                uint i, skipSize = rankVal[minWeight];

                MEM_writeLE16((void*)&(DElt.sequence), baseSeq);
                DElt.nbBits = (byte)(consumed);
                DElt.length = 1;
                for (i = 0; i < skipSize; i++)
                {
                    DTable[i] = DElt;
                }
            }


            {
                uint s;

                for (s = 0; s < sortedListSize; s++)
                {
                    uint symbol = sortedSymbols[s].symbol;
                    uint weight = sortedSymbols[s].weight;
                    uint nbBits = nbBitsBaseline - weight;
                    uint length = (uint)(1 << (int)(sizeLog - nbBits));
                    uint start = rankVal[weight];
                    uint i = start;
                    uint end = start + length;

                    MEM_writeLE16((void*)&(DElt.sequence), (ushort)(baseSeq + (symbol << 8)));
                    DElt.nbBits = (byte)(nbBits + consumed);
                    DElt.length = 2;
                    do
                    {
                        DTable[i++] = DElt;
                    }
                    while (i < end);

                    rankVal[weight] += length;
                }
            }
        }

        private static void HUF_fillDTableX2(HUF_DEltX2* DTable, uint targetLog, sortedSymbol_t* sortedList, uint sortedListSize, uint* rankStart, rankValCol_t* rankValOrigin, uint maxWeight, uint nbBitsBaseline, uint* wksp, nuint wkspSize)
        {
            uint* rankVal = wksp;
            int scaleLog = (int)(nbBitsBaseline - targetLog);
            uint minBits = nbBitsBaseline - maxWeight;
            uint s;

            assert(wkspSize >= (uint)(12 + 1));
            wksp += 12 + 1;
            wkspSize -= (nuint)(12 + 1);
            memcpy((void*)(rankVal), (void*)(rankValOrigin), ((nuint)(sizeof(uint)) * (uint)((12 + 1))));
            for (s = 0; s < sortedListSize; s++)
            {
                ushort symbol = sortedList[s].symbol;
                uint weight = sortedList[s].weight;
                uint nbBits = nbBitsBaseline - weight;
                uint start = rankVal[weight];
                uint length = (uint)(1 << (int)(targetLog - nbBits));

                if (targetLog - nbBits >= minBits)
                {
                    uint sortedRank;
                    int minWeight = (int)(nbBits + (uint)scaleLog);

                    if (minWeight < 1)
                    {
                        minWeight = 1;
                    }

                    sortedRank = rankStart[minWeight];
                    HUF_fillDTableX2Level2(DTable + start, targetLog - nbBits, nbBits, (uint*)(rankValOrigin[nbBits]), minWeight, sortedList + sortedRank, sortedListSize - sortedRank, nbBitsBaseline, symbol, wksp, wkspSize);
                }
                else
                {
                    HUF_DEltX2 DElt;

                    MEM_writeLE16((void*)&(DElt.sequence), symbol);
                    DElt.nbBits = (byte)(nbBits);
                    DElt.length = 1;

                    {
                        uint end = start + length;
                        uint u;

                        for (u = start; u < end; u++)
                        {
                            DTable[u] = DElt;
                        }
                    }
                }

                rankVal[weight] += length;
            }
        }

        public static nuint HUF_readDTableX2_wksp(uint* DTable, void* src, nuint srcSize, void* workSpace, nuint wkspSize)
        {
            uint tableLog, maxW, sizeOfSort, nbSymbols;
            DTableDesc dtd = HUF_getDTableDesc(DTable);
            uint maxTableLog = dtd.maxTableLog;
            nuint iSize;
            void* dtPtr = (void*)(DTable + 1);
            HUF_DEltX2* dt = (HUF_DEltX2*)(dtPtr);
            uint* rankStart;
            HUF_ReadDTableX2_Workspace* wksp = (HUF_ReadDTableX2_Workspace*)(workSpace);

            if ((nuint)(sizeof(HUF_ReadDTableX2_Workspace)) > wkspSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            rankStart = wksp->rankStart0 + 1;
            memset((void*)(wksp->rankStats), (0), ((nuint)(sizeof(uint) * 13)));
            memset((void*)(wksp->rankStart0), (0), ((nuint)(sizeof(uint) * 14)));
            if (maxTableLog > 12)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            iSize = HUF_readStats_wksp((byte*)wksp->weightList, (nuint)(255 + 1), (uint*)wksp->rankStats, &nbSymbols, &tableLog, src, srcSize, (void*)wksp->calleeWksp, (nuint)(sizeof(uint) * 218), 0);
            if ((ERR_isError(iSize)) != 0)
            {
                return iSize;
            }

            if (tableLog > maxTableLog)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            for (maxW = tableLog; wksp->rankStats[maxW] == 0; maxW--)
            {
            }


            {
                uint w, nextRankStart = 0;

                for (w = 1; w < maxW + 1; w++)
                {
                    uint curr = nextRankStart;

                    nextRankStart += wksp->rankStats[w];
                    rankStart[w] = curr;
                }

                rankStart[0] = nextRankStart;
                sizeOfSort = nextRankStart;
            }


            {
                uint s;

                for (s = 0; s < nbSymbols; s++)
                {
                    uint w = wksp->weightList[s];
                    uint r = rankStart[w]++;

                    wksp->sortedSymbol[r].symbol = (byte)(s);
                    wksp->sortedSymbol[r].weight = (byte)(w);
                }

                rankStart[0] = 0;
            }


            {
                uint* rankVal0 = (uint*)(wksp->rankVal[0]);


                {
                    int rescale = (int)((maxTableLog - tableLog) - 1);
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
                        uint* rankValPtr = (uint*)(wksp->rankVal[consumed]);
                        uint w;

                        for (w = 1; w < maxW + 1; w++)
                        {
                            rankValPtr[w] = rankVal0[w] >> (int)consumed;
                        }
                    }
                }
            }

            HUF_fillDTableX2(dt, maxTableLog, (sortedSymbol_t*)wksp->sortedSymbol, sizeOfSort, (uint*)wksp->rankStart0, wksp->rankVal, maxW, tableLog + 1, (uint*)wksp->calleeWksp, (nuint)(sizeof(uint) * 218) / (nuint)(sizeof(uint)));
            dtd.tableLog = (byte)(maxTableLog);
            dtd.tableType = 1;
            memcpy((void*)(DTable), (void*)(&dtd), ((nuint)(sizeof(DTableDesc))));
            return iSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HUF_decodeSymbolX2(void* op, BIT_DStream_t* DStream, HUF_DEltX2* dt, uint dtLog)
        {
            nuint val = BIT_lookBitsFast(DStream, dtLog);

            memcpy((op), (void*)((dt + val)), (2));
            BIT_skipBits(DStream, dt[val].nbBits);
            return dt[val].length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HUF_decodeLastSymbolX2(void* op, BIT_DStream_t* DStream, HUF_DEltX2* dt, uint dtLog)
        {
            nuint val = BIT_lookBitsFast(DStream, dtLog);

            memcpy((op), (void*)((dt + val)), (1));
            if (dt[val].length == 1)
            {
                BIT_skipBits(DStream, dt[val].nbBits);
            }
            else
            {
                if (DStream->bitsConsumed < ((nuint)(sizeof(nuint)) * 8))
                {
                    BIT_skipBits(DStream, dt[val].nbBits);
                    if (DStream->bitsConsumed > ((nuint)(sizeof(nuint)) * 8))
                    {
                        DStream->bitsConsumed = (uint)(((nuint)(sizeof(nuint)) * 8));
                    }
                }
            }

            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_decodeStreamX2(byte* p, BIT_DStream_t* bitDPtr, byte* pEnd, HUF_DEltX2* dt, uint dtLog)
        {
            byte* pStart = p;

            while (((BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished) && (p < pEnd - ((nuint)(sizeof(nuint)) - 1))))
            {
                if (MEM_64bits)
                {
                    p += HUF_decodeSymbolX2((void*)p, bitDPtr, dt, dtLog);
                }

                if (MEM_64bits || (12 <= 12))
                {
                    p += HUF_decodeSymbolX2((void*)p, bitDPtr, dt, dtLog);
                }

                if (MEM_64bits)
                {
                    p += HUF_decodeSymbolX2((void*)p, bitDPtr, dt, dtLog);
                }

                p += HUF_decodeSymbolX2((void*)p, bitDPtr, dt, dtLog);
            }

            while (((BIT_reloadDStream(bitDPtr) == BIT_DStream_status.BIT_DStream_unfinished) && (p <= pEnd - 2)))
            {
                p += HUF_decodeSymbolX2((void*)p, bitDPtr, dt, dtLog);
            }

            while (p <= pEnd - 2)
            {
                p += HUF_decodeSymbolX2((void*)p, bitDPtr, dt, dtLog);
            }

            if (p < pEnd)
            {
                p += HUF_decodeLastSymbolX2((void*)p, bitDPtr, dt, dtLog);
            }

            return (nuint)(p - pStart);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_decompress1X2_usingDTable_internal_body(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            BIT_DStream_t bitD;


            {
                nuint _var_err__ = BIT_initDStream(&bitD, cSrc, cSrcSize);

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }


            {
                byte* ostart = (byte*)(dst);
                byte* oend = ostart + dstSize;
                void* dtPtr = (void*)(DTable + 1);
                HUF_DEltX2* dt = (HUF_DEltX2*)(dtPtr);
                DTableDesc dtd = HUF_getDTableDesc(DTable);

                HUF_decodeStreamX2(ostart, &bitD, oend, dt, dtd.tableLog);
            }

            if ((BIT_endOfDStream(&bitD)) == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            return dstSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_decompress4X2_usingDTable_internal_body(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            if (cSrcSize < 10)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }


            {
                byte* istart = (byte*)(cSrc);
                byte* ostart = (byte*)(dst);
                byte* oend = ostart + dstSize;
                byte* olimit = oend - ((nuint)(sizeof(nuint)) - 1);
                void* dtPtr = (void*)(DTable + 1);
                HUF_DEltX2* dt = (HUF_DEltX2*)(dtPtr);
                BIT_DStream_t bitD1;
                BIT_DStream_t bitD2;
                BIT_DStream_t bitD3;
                BIT_DStream_t bitD4;
                nuint length1 = MEM_readLE16((void*)istart);
                nuint length2 = MEM_readLE16((void*)(istart + 2));
                nuint length3 = MEM_readLE16((void*)(istart + 4));
                nuint length4 = cSrcSize - (length1 + length2 + length3 + 6);
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
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD1, (void*)istart1, length1);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD2, (void*)istart2, length2);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD3, (void*)istart3, length3);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }


                {
                    nuint _var_err__ = BIT_initDStream(&bitD4, (void*)istart4, length4);

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }

                for (; ((endSignal) & (uint)((((op4 < olimit)) ? 1 : 0))) != 0;)
                {
                    if (MEM_64bits)
                    {
                        op1 += HUF_decodeSymbolX2((void*)op1, &bitD1, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        op1 += HUF_decodeSymbolX2((void*)op1, &bitD1, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        op1 += HUF_decodeSymbolX2((void*)op1, &bitD1, dt, dtLog);
                    }

                    op1 += HUF_decodeSymbolX2((void*)op1, &bitD1, dt, dtLog);
                    if (MEM_64bits)
                    {
                        op2 += HUF_decodeSymbolX2((void*)op2, &bitD2, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        op2 += HUF_decodeSymbolX2((void*)op2, &bitD2, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        op2 += HUF_decodeSymbolX2((void*)op2, &bitD2, dt, dtLog);
                    }

                    op2 += HUF_decodeSymbolX2((void*)op2, &bitD2, dt, dtLog);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD1) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD2) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                    if (MEM_64bits)
                    {
                        op3 += HUF_decodeSymbolX2((void*)op3, &bitD3, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        op3 += HUF_decodeSymbolX2((void*)op3, &bitD3, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        op3 += HUF_decodeSymbolX2((void*)op3, &bitD3, dt, dtLog);
                    }

                    op3 += HUF_decodeSymbolX2((void*)op3, &bitD3, dt, dtLog);
                    if (MEM_64bits)
                    {
                        op4 += HUF_decodeSymbolX2((void*)op4, &bitD4, dt, dtLog);
                    }

                    if (MEM_64bits || (12 <= 12))
                    {
                        op4 += HUF_decodeSymbolX2((void*)op4, &bitD4, dt, dtLog);
                    }

                    if (MEM_64bits)
                    {
                        op4 += HUF_decodeSymbolX2((void*)op4, &bitD4, dt, dtLog);
                    }

                    op4 += HUF_decodeSymbolX2((void*)op4, &bitD4, dt, dtLog);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD3) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                    endSignal &= ((BIT_reloadDStreamFast(&bitD4) == BIT_DStream_status.BIT_DStream_unfinished) ? 1U : 0U);
                }

                if (op1 > opStart2)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                if (op2 > opStart3)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                if (op3 > opStart4)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                }

                HUF_decodeStreamX2(op1, &bitD1, opStart2, dt, dtLog);
                HUF_decodeStreamX2(op2, &bitD2, opStart3, dt, dtLog);
                HUF_decodeStreamX2(op3, &bitD3, opStart4, dt, dtLog);
                HUF_decodeStreamX2(op4, &bitD4, oend, dt, dtLog);

                {
                    uint endCheck = BIT_endOfDStream(&bitD1) & BIT_endOfDStream(&bitD2) & BIT_endOfDStream(&bitD3) & BIT_endOfDStream(&bitD4);

                    if (endCheck == 0)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
                    }
                }

                return dstSize;
            }
        }

        private static nuint HUF_decompress1X2_usingDTable_internal_default(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress1X2_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress1X2_usingDTable_internal_bmi2(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress1X2_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress1X2_usingDTable_internal(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable, int bmi2)
        {
            if (bmi2 != 0)
            {
                return HUF_decompress1X2_usingDTable_internal_bmi2(dst, dstSize, cSrc, cSrcSize, DTable);
            }

            return HUF_decompress1X2_usingDTable_internal_default(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress4X2_usingDTable_internal_default(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress4X2_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress4X2_usingDTable_internal_bmi2(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            return HUF_decompress4X2_usingDTable_internal_body(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        private static nuint HUF_decompress4X2_usingDTable_internal(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable, int bmi2)
        {
            if (bmi2 != 0)
            {
                return HUF_decompress4X2_usingDTable_internal_bmi2(dst, dstSize, cSrc, cSrcSize, DTable);
            }

            return HUF_decompress4X2_usingDTable_internal_default(dst, dstSize, cSrc, cSrcSize, DTable);
        }

        public static nuint HUF_decompress1X2_usingDTable(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            if (dtd.tableType != 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            return HUF_decompress1X2_usingDTable_internal(dst, dstSize, cSrc, cSrcSize, DTable, 0);
        }

        public static nuint HUF_decompress1X2_DCtx_wksp(uint* DCtx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize)
        {
            byte* ip = (byte*)(cSrc);
            nuint hSize = HUF_readDTableX2_wksp(DCtx, cSrc, cSrcSize, workSpace, wkspSize);

            if ((ERR_isError(hSize)) != 0)
            {
                return hSize;
            }

            if (hSize >= cSrcSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            ip += hSize;
            cSrcSize -= hSize;
            return HUF_decompress1X2_usingDTable_internal(dst, dstSize, (void*)ip, cSrcSize, DCtx, 0);
        }

        public static nuint HUF_decompress4X2_usingDTable(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            if (dtd.tableType != 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            return HUF_decompress4X2_usingDTable_internal(dst, dstSize, cSrc, cSrcSize, DTable, 0);
        }

        private static nuint HUF_decompress4X2_DCtx_wksp_bmi2(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize, int bmi2)
        {
            byte* ip = (byte*)(cSrc);
            nuint hSize = HUF_readDTableX2_wksp(dctx, cSrc, cSrcSize, workSpace, wkspSize);

            if ((ERR_isError(hSize)) != 0)
            {
                return hSize;
            }

            if (hSize >= cSrcSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            ip += hSize;
            cSrcSize -= hSize;
            return HUF_decompress4X2_usingDTable_internal(dst, dstSize, (void*)ip, cSrcSize, dctx, bmi2);
        }

        public static nuint HUF_decompress4X2_DCtx_wksp(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize)
        {
            return HUF_decompress4X2_DCtx_wksp_bmi2(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize, 0);
        }

        /* ***********************************/
        /* Universal decompression selectors */
        /* ***********************************/
        public static nuint HUF_decompress1X_usingDTable(void* dst, nuint maxDstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            return dtd.tableType != 0 ? HUF_decompress1X2_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, 0) : HUF_decompress1X1_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, 0);
        }

        public static nuint HUF_decompress4X_usingDTable(void* dst, nuint maxDstSize, void* cSrc, nuint cSrcSize, uint* DTable)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            return dtd.tableType != 0 ? HUF_decompress4X2_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, 0) : HUF_decompress4X1_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, 0);
        }

        public static algo_time_t[][] algoTime = new algo_time_t[16][]
        {
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 0,
                    decode256Time = 0,
                },
                new algo_time_t
                {
                    tableTime = 1,
                    decode256Time = 1,
                },
                new algo_time_t
                {
                    tableTime = 2,
                    decode256Time = 2,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 0,
                    decode256Time = 0,
                },
                new algo_time_t
                {
                    tableTime = 1,
                    decode256Time = 1,
                },
                new algo_time_t
                {
                    tableTime = 2,
                    decode256Time = 2,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 38,
                    decode256Time = 130,
                },
                new algo_time_t
                {
                    tableTime = 1313,
                    decode256Time = 74,
                },
                new algo_time_t
                {
                    tableTime = 2151,
                    decode256Time = 38,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 448,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1353,
                    decode256Time = 74,
                },
                new algo_time_t
                {
                    tableTime = 2238,
                    decode256Time = 41,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 556,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1353,
                    decode256Time = 74,
                },
                new algo_time_t
                {
                    tableTime = 2238,
                    decode256Time = 47,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 714,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1418,
                    decode256Time = 74,
                },
                new algo_time_t
                {
                    tableTime = 2436,
                    decode256Time = 53,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 883,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1437,
                    decode256Time = 74,
                },
                new algo_time_t
                {
                    tableTime = 2464,
                    decode256Time = 61,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 897,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1515,
                    decode256Time = 75,
                },
                new algo_time_t
                {
                    tableTime = 2622,
                    decode256Time = 68,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 926,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1613,
                    decode256Time = 75,
                },
                new algo_time_t
                {
                    tableTime = 2730,
                    decode256Time = 75,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 947,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1729,
                    decode256Time = 77,
                },
                new algo_time_t
                {
                    tableTime = 3359,
                    decode256Time = 77,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 1107,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 2083,
                    decode256Time = 81,
                },
                new algo_time_t
                {
                    tableTime = 4006,
                    decode256Time = 84,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 1177,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 2379,
                    decode256Time = 87,
                },
                new algo_time_t
                {
                    tableTime = 4785,
                    decode256Time = 88,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 1242,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 2415,
                    decode256Time = 93,
                },
                new algo_time_t
                {
                    tableTime = 5155,
                    decode256Time = 84,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 1349,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 2644,
                    decode256Time = 106,
                },
                new algo_time_t
                {
                    tableTime = 5260,
                    decode256Time = 106,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 1455,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 2422,
                    decode256Time = 124,
                },
                new algo_time_t
                {
                    tableTime = 4174,
                    decode256Time = 124,
                },
            },
            new algo_time_t[3]
            {
                new algo_time_t
                {
                    tableTime = 722,
                    decode256Time = 128,
                },
                new algo_time_t
                {
                    tableTime = 1891,
                    decode256Time = 145,
                },
                new algo_time_t
                {
                    tableTime = 1936,
                    decode256Time = 146,
                },
            },
        };

        /** HUF_selectDecoder() :
         *  Tells which decoder is likely to decode faster,
         *  based on a set of pre-computed metrics.
         * @return : 0==HUF_decompress4X1, 1==HUF_decompress4X2 .
         *  Assumption : 0 < dstSize <= 128 KB */
        public static uint HUF_selectDecoder(nuint dstSize, nuint cSrcSize)
        {
            assert(dstSize > 0);
            assert(dstSize <= (uint)(128 * 1024));

            {
                uint Q = (uint)((cSrcSize >= dstSize) ? 15 : (uint)(cSrcSize * 16 / dstSize));
                uint D256 = (uint)(dstSize >> 8);
                uint DTime0 = algoTime[Q][0].tableTime + (algoTime[Q][0].decode256Time * D256);
                uint DTime1 = algoTime[Q][1].tableTime + (algoTime[Q][1].decode256Time * D256);

                DTime1 += DTime1 >> 3;
                return ((DTime1 < DTime0) ? 1U : 0U);
            }
        }

        public static nuint HUF_decompress4X_hufOnly_wksp(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize)
        {
            if (dstSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (cSrcSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }


            {
                uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);

                return algoNb != 0 ? HUF_decompress4X2_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize) : HUF_decompress4X1_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize);
            }
        }

        public static nuint HUF_decompress1X_DCtx_wksp(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize)
        {
            if (dstSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (cSrcSize > dstSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            if (cSrcSize == dstSize)
            {
                memcpy((dst), (cSrc), (dstSize));
                return dstSize;
            }

            if (cSrcSize == 1)
            {
                memset((dst), (int)(*(byte*)(cSrc)), (dstSize));
                return dstSize;
            }


            {
                uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);

                return algoNb != 0 ? HUF_decompress1X2_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize) : HUF_decompress1X1_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize);
            }
        }

        /* BMI2 variants.
         * If the CPU has BMI2 support, pass bmi2=1, otherwise pass bmi2=0.
         */
        public static nuint HUF_decompress1X_usingDTable_bmi2(void* dst, nuint maxDstSize, void* cSrc, nuint cSrcSize, uint* DTable, int bmi2)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            return dtd.tableType != 0 ? HUF_decompress1X2_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, bmi2) : HUF_decompress1X1_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, bmi2);
        }

        public static nuint HUF_decompress1X1_DCtx_wksp_bmi2(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize, int bmi2)
        {
            byte* ip = (byte*)(cSrc);
            nuint hSize = HUF_readDTableX1_wksp_bmi2(dctx, cSrc, cSrcSize, workSpace, wkspSize, bmi2);

            if ((ERR_isError(hSize)) != 0)
            {
                return hSize;
            }

            if (hSize >= cSrcSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            ip += hSize;
            cSrcSize -= hSize;
            return HUF_decompress1X1_usingDTable_internal(dst, dstSize, (void*)ip, cSrcSize, dctx, bmi2);
        }

        public static nuint HUF_decompress4X_usingDTable_bmi2(void* dst, nuint maxDstSize, void* cSrc, nuint cSrcSize, uint* DTable, int bmi2)
        {
            DTableDesc dtd = HUF_getDTableDesc(DTable);

            return dtd.tableType != 0 ? HUF_decompress4X2_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, bmi2) : HUF_decompress4X1_usingDTable_internal(dst, maxDstSize, cSrc, cSrcSize, DTable, bmi2);
        }

        public static nuint HUF_decompress4X_hufOnly_wksp_bmi2(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize, void* workSpace, nuint wkspSize, int bmi2)
        {
            if (dstSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (cSrcSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }


            {
                uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);

                return algoNb != 0 ? HUF_decompress4X2_DCtx_wksp_bmi2(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize, bmi2) : HUF_decompress4X1_DCtx_wksp_bmi2(dctx, dst, dstSize, cSrc, cSrcSize, workSpace, wkspSize, bmi2);
            }
        }

        public static nuint HUF_readDTableX1(uint* DTable, void* src, nuint srcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_readDTableX1_wksp(DTable, src, srcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        public static nuint HUF_decompress1X1_DCtx(uint* DCtx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_decompress1X1_DCtx_wksp(DCtx, dst, dstSize, cSrc, cSrcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        public static nuint HUF_decompress1X1(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* DTable = stackalloc uint[2049];
            DTable[0] = ((uint)((12) - 1) * 0x01000001);
            memset(DTable + 1, 0, sizeof(uint) * 2048);

            return HUF_decompress1X1_DCtx((uint*)DTable, dst, dstSize, cSrc, cSrcSize);
        }

        public static nuint HUF_readDTableX2(uint* DTable, void* src, nuint srcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_readDTableX2_wksp(DTable, src, srcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        public static nuint HUF_decompress1X2_DCtx(uint* DCtx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_decompress1X2_DCtx_wksp(DCtx, dst, dstSize, cSrc, cSrcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        public static nuint HUF_decompress1X2(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* DTable = stackalloc uint[4097];
            DTable[0] = ((uint)(12) * 0x01000001);
            memset(DTable + 1, 0, sizeof(uint) * 4096);

            return HUF_decompress1X2_DCtx((uint*)DTable, dst, dstSize, cSrc, cSrcSize);
        }

        public static nuint HUF_decompress4X1_DCtx(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_decompress4X1_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        /* ****************************************
        *  Advanced decompression functions
        ******************************************/
        public static nuint HUF_decompress4X1(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* DTable = stackalloc uint[2049];
            DTable[0] = ((uint)((12) - 1) * 0x01000001);
            memset(DTable + 1, 0, sizeof(uint) * 2048);

            return HUF_decompress4X1_DCtx((uint*)DTable, dst, dstSize, cSrc, cSrcSize);
        }

        public static nuint HUF_decompress4X2_DCtx(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_decompress4X2_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        public static nuint HUF_decompress4X2(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* DTable = stackalloc uint[4097];
            DTable[0] = ((uint)(12) * 0x01000001);
            memset(DTable + 1, 0, sizeof(uint) * 4096);

            return HUF_decompress4X2_DCtx((uint*)DTable, dst, dstSize, cSrc, cSrcSize);
        }

        /** HUF_decompress() :
         *  Decompress HUF data from buffer 'cSrc', of size 'cSrcSize',
         *  into already allocated buffer 'dst', of minimum size 'dstSize'.
         * `originalSize` : **must** be the ***exact*** size of original (uncompressed) data.
         *  Note : in contrast with FSE, HUF_decompress can regenerate
         *         RLE (cSrcSize==1) and uncompressed (cSrcSize==dstSize) data,
         *         because it knows size to regenerate (originalSize).
         * @return : size of regenerated data (== originalSize),
         *           or an error code, which can be tested using HUF_isError()
         */
        public static nuint HUF_decompress(void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {


            if (dstSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (cSrcSize > dstSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            if (cSrcSize == dstSize)
            {
                memcpy((dst), (cSrc), (dstSize));
                return dstSize;
            }

            if (cSrcSize == 1)
            {
                memset((dst), (int)(*(byte*)(cSrc)), (dstSize));
                return dstSize;
            }


            {
                uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);

                return decompress[algoNb](dst, dstSize, cSrc, cSrcSize);
            }
        }

        public static nuint HUF_decompress4X_DCtx(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            if (dstSize == 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (cSrcSize > dstSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected)));
            }

            if (cSrcSize == dstSize)
            {
                memcpy((dst), (cSrc), (dstSize));
                return dstSize;
            }

            if (cSrcSize == 1)
            {
                memset((dst), (int)(*(byte*)(cSrc)), (dstSize));
                return dstSize;
            }


            {
                uint algoNb = HUF_selectDecoder(dstSize, cSrcSize);

                return algoNb != 0 ? HUF_decompress4X2_DCtx(dctx, dst, dstSize, cSrc, cSrcSize) : HUF_decompress4X1_DCtx(dctx, dst, dstSize, cSrc, cSrcSize);
            }
        }

        public static nuint HUF_decompress4X_hufOnly(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_decompress4X_hufOnly_wksp(dctx, dst, dstSize, cSrc, cSrcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }

        public static nuint HUF_decompress1X_DCtx(uint* dctx, void* dst, nuint dstSize, void* cSrc, nuint cSrcSize)
        {
            uint* workSpace = stackalloc uint[640];

            return HUF_decompress1X_DCtx_wksp(dctx, dst, dstSize, cSrc, cSrcSize, (void*)workSpace, (nuint)(sizeof(uint) * 640));
        }
    }
}
