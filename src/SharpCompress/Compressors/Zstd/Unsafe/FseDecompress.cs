using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /* Function templates */
        public static uint* FSE_createDTable(uint tableLog)
        {
            if (tableLog > 15)
            {
                tableLog = 15;
            }

            return (uint*)(malloc((uint)((1 + (1 << (int)(tableLog)))) * (nuint)(sizeof(uint))));
        }

        public static void FSE_freeDTable(uint* dt)
        {
            free((void*)(dt));
        }

        private static nuint FSE_buildDTable_internal(uint* dt, short* normalizedCounter, uint maxSymbolValue, uint tableLog, void* workSpace, nuint wkspSize)
        {
            void* tdPtr = (void*)(dt + 1);
            FSE_decode_t* tableDecode = (FSE_decode_t*)(tdPtr);
            ushort* symbolNext = (ushort*)(workSpace);
            byte* spread = (byte*)(symbolNext + maxSymbolValue + 1);
            uint maxSV1 = maxSymbolValue + 1;
            uint tableSize = (uint)(1 << (int)tableLog);
            uint highThreshold = tableSize - 1;

            if (((nuint)(sizeof(short)) * (maxSymbolValue + 1) + (1UL << (int)tableLog) + 8) > wkspSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge)));
            }

            if (maxSymbolValue > 255)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge)));
            }

            if (tableLog > (uint)((14 - 2)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }


            {
                FSE_DTableHeader DTableH;

                DTableH.tableLog = (ushort)(tableLog);
                DTableH.fastMode = 1;

                {
                    short largeLimit = (short)(1 << (int)(tableLog - 1));
                    uint s;

                    for (s = 0; s < maxSV1; s++)
                    {
                        if (normalizedCounter[s] == -1)
                        {
                            tableDecode[highThreshold--].symbol = (byte)(s);
                            symbolNext[s] = 1;
                        }
                        else
                        {
                            if (normalizedCounter[s] >= largeLimit)
                            {
                                DTableH.fastMode = 0;
                            }

                            symbolNext[s] = (ushort)(normalizedCounter[s]);
                        }
                    }
                }

                memcpy((void*)(dt), (void*)(&DTableH), ((nuint)(sizeof(FSE_DTableHeader))));
            }

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

                            tableDecode[uPosition].symbol = spread[s + u];
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

                    for (i = 0; i < normalizedCounter[s]; i++)
                    {
                        tableDecode[position].symbol = (byte)(s);
                        position = (position + step) & tableMask;
                        while (position > highThreshold)
                        {
                            position = (position + step) & tableMask;
                        }
                    }
                }

                if (position != 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                }
            }


            {
                uint u;

                for (u = 0; u < tableSize; u++)
                {
                    byte symbol = (byte)(tableDecode[u].symbol);
                    uint nextState = symbolNext[symbol]++;

                    tableDecode[u].nbBits = (byte)(tableLog - BIT_highbit32(nextState));
                    tableDecode[u].newState = (ushort)((nextState << (int)(tableDecode[u].nbBits)) - tableSize);
                }
            }

            return 0;
        }

        public static nuint FSE_buildDTable_wksp(uint* dt, short* normalizedCounter, uint maxSymbolValue, uint tableLog, void* workSpace, nuint wkspSize)
        {
            return FSE_buildDTable_internal(dt, normalizedCounter, maxSymbolValue, tableLog, workSpace, wkspSize);
        }

        /*-*******************************************************
        *  Decompression (Byte symbols)
        *********************************************************/
        public static nuint FSE_buildDTable_rle(uint* dt, byte symbolValue)
        {
            void* ptr = (void*)dt;
            FSE_DTableHeader* DTableH = (FSE_DTableHeader*)(ptr);
            void* dPtr = (void*)(dt + 1);
            FSE_decode_t* cell = (FSE_decode_t*)(dPtr);

            DTableH->tableLog = 0;
            DTableH->fastMode = 0;
            cell->newState = 0;
            cell->symbol = symbolValue;
            cell->nbBits = 0;
            return 0;
        }

        public static nuint FSE_buildDTable_raw(uint* dt, uint nbBits)
        {
            void* ptr = (void*)dt;
            FSE_DTableHeader* DTableH = (FSE_DTableHeader*)(ptr);
            void* dPtr = (void*)(dt + 1);
            FSE_decode_t* dinfo = (FSE_decode_t*)(dPtr);
            uint tableSize = (uint)(1 << (int)nbBits);
            uint tableMask = tableSize - 1;
            uint maxSV1 = tableMask + 1;
            uint s;

            if (nbBits < 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            DTableH->tableLog = (ushort)(nbBits);
            DTableH->fastMode = 1;
            for (s = 0; s < maxSV1; s++)
            {
                dinfo[s].newState = 0;
                dinfo[s].symbol = (byte)(s);
                dinfo[s].nbBits = (byte)(nbBits);
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint FSE_decompress_usingDTable_generic(void* dst, nuint maxDstSize, void* cSrc, nuint cSrcSize, uint* dt, uint fast)
        {
            byte* ostart = (byte*)(dst);
            byte* op = ostart;
            byte* omax = op + maxDstSize;
            byte* olimit = omax - 3;
            BIT_DStream_t bitD;
            FSE_DState_t state1;
            FSE_DState_t state2;


            {
                nuint _var_err__ = BIT_initDStream(&bitD, cSrc, cSrcSize);

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }

            FSE_initDState(&state1, &bitD, dt);
            FSE_initDState(&state2, &bitD, dt);
            for (; ((BIT_reloadDStream(&bitD) == BIT_DStream_status.BIT_DStream_unfinished) && (op < olimit)); op += 4)
            {
                op[0] = fast != 0 ? FSE_decodeSymbolFast(&state1, &bitD) : FSE_decodeSymbol(&state1, &bitD);
                if ((uint)((14 - 2) * 2 + 7) > (nuint)(sizeof(nuint)) * 8)
                {
                    BIT_reloadDStream(&bitD);
                }

                op[1] = fast != 0 ? FSE_decodeSymbolFast(&state2, &bitD) : FSE_decodeSymbol(&state2, &bitD);
                if ((uint)((14 - 2) * 4 + 7) > (nuint)(sizeof(nuint)) * 8)
                {
                    if (BIT_reloadDStream(&bitD) > BIT_DStream_status.BIT_DStream_unfinished)
                    {
                        op += 2;
                        break;
                    }
                }

                op[2] = fast != 0 ? FSE_decodeSymbolFast(&state1, &bitD) : FSE_decodeSymbol(&state1, &bitD);
                if ((uint)((14 - 2) * 2 + 7) > (nuint)(sizeof(nuint)) * 8)
                {
                    BIT_reloadDStream(&bitD);
                }

                op[3] = fast != 0 ? FSE_decodeSymbolFast(&state2, &bitD) : FSE_decodeSymbol(&state2, &bitD);
            }

            while (1 != 0)
            {
                if (op > (omax - 2))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                *op++ = fast != 0 ? FSE_decodeSymbolFast(&state1, &bitD) : FSE_decodeSymbol(&state1, &bitD);
                if (BIT_reloadDStream(&bitD) == BIT_DStream_status.BIT_DStream_overflow)
                {
                    *op++ = fast != 0 ? FSE_decodeSymbolFast(&state2, &bitD) : FSE_decodeSymbol(&state2, &bitD);
                    break;
                }

                if (op > (omax - 2))
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                *op++ = fast != 0 ? FSE_decodeSymbolFast(&state2, &bitD) : FSE_decodeSymbol(&state2, &bitD);
                if (BIT_reloadDStream(&bitD) == BIT_DStream_status.BIT_DStream_overflow)
                {
                    *op++ = fast != 0 ? FSE_decodeSymbolFast(&state1, &bitD) : FSE_decodeSymbol(&state1, &bitD);
                    break;
                }
            }

            return (nuint)(op - ostart);
        }

        /*! FSE_decompress_usingDTable():
            Decompress compressed source `cSrc` of size `cSrcSize` using `dt`
            into `dst` which must be already allocated.
            @return : size of regenerated data (necessarily <= `dstCapacity`),
                      or an errorCode, which can be tested using FSE_isError() */
        public static nuint FSE_decompress_usingDTable(void* dst, nuint originalSize, void* cSrc, nuint cSrcSize, uint* dt)
        {
            void* ptr = (void*)dt;
            FSE_DTableHeader* DTableH = (FSE_DTableHeader*)(ptr);
            uint fastMode = DTableH->fastMode;

            if (fastMode != 0)
            {
                return FSE_decompress_usingDTable_generic(dst, originalSize, cSrc, cSrcSize, dt, 1);
            }

            return FSE_decompress_usingDTable_generic(dst, originalSize, cSrc, cSrcSize, dt, 0);
        }

        public static nuint FSE_decompress_wksp(void* dst, nuint dstCapacity, void* cSrc, nuint cSrcSize, uint maxLog, void* workSpace, nuint wkspSize)
        {
            return FSE_decompress_wksp_bmi2(dst, dstCapacity, cSrc, cSrcSize, maxLog, workSpace, wkspSize, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint FSE_decompress_wksp_body(void* dst, nuint dstCapacity, void* cSrc, nuint cSrcSize, uint maxLog, void* workSpace, nuint wkspSize, int bmi2)
        {
            byte* istart = (byte*)(cSrc);
            byte* ip = istart;
            uint tableLog;
            uint maxSymbolValue = 255;
            FSE_DecompressWksp* wksp = (FSE_DecompressWksp*)(workSpace);

            if (wkspSize < (nuint)(sizeof(FSE_DecompressWksp)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }


            {
                nuint NCountLength = FSE_readNCount_bmi2((short*)wksp->ncount, &maxSymbolValue, &tableLog, (void*)istart, cSrcSize, bmi2);

                if ((ERR_isError(NCountLength)) != 0)
                {
                    return NCountLength;
                }

                if (tableLog > maxLog)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
                }

                assert(NCountLength <= cSrcSize);
                ip += NCountLength;
                cSrcSize -= NCountLength;
            }

            if ((((uint)((1 + (1 << (int)(tableLog)))) + ((((nuint)(sizeof(short)) * (maxSymbolValue + 1) + (1UL << (int)tableLog) + 8) + (nuint)(sizeof(uint)) - 1) / (nuint)(sizeof(uint))) + (uint)((255 + 1) / 2) + 1) * (nuint)(sizeof(uint))) > wkspSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            workSpace = wksp->dtable + (1 + (1 << (int)(tableLog)));
            wkspSize -= (nuint)(sizeof(FSE_DecompressWksp)) + ((uint)((1 + (1 << (int)(tableLog)))) * (nuint)(sizeof(uint)));

            {
                nuint _var_err__ = FSE_buildDTable_internal((uint*)wksp->dtable, (short*)wksp->ncount, maxSymbolValue, tableLog, workSpace, wkspSize);

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }


            {
                void* ptr = (void*)wksp->dtable;
                FSE_DTableHeader* DTableH = (FSE_DTableHeader*)(ptr);
                uint fastMode = DTableH->fastMode;

                if (fastMode != 0)
                {
                    return FSE_decompress_usingDTable_generic(dst, dstCapacity, (void*)ip, cSrcSize, (uint*)wksp->dtable, 1);
                }

                return FSE_decompress_usingDTable_generic(dst, dstCapacity, (void*)ip, cSrcSize, (uint*)wksp->dtable, 0);
            }
        }

        /* Avoids the FORCE_INLINE of the _body() function. */
        private static nuint FSE_decompress_wksp_body_default(void* dst, nuint dstCapacity, void* cSrc, nuint cSrcSize, uint maxLog, void* workSpace, nuint wkspSize)
        {
            return FSE_decompress_wksp_body(dst, dstCapacity, cSrc, cSrcSize, maxLog, workSpace, wkspSize, 0);
        }

        private static nuint FSE_decompress_wksp_body_bmi2(void* dst, nuint dstCapacity, void* cSrc, nuint cSrcSize, uint maxLog, void* workSpace, nuint wkspSize)
        {
            return FSE_decompress_wksp_body(dst, dstCapacity, cSrc, cSrcSize, maxLog, workSpace, wkspSize, 1);
        }

        public static nuint FSE_decompress_wksp_bmi2(void* dst, nuint dstCapacity, void* cSrc, nuint cSrcSize, uint maxLog, void* workSpace, nuint wkspSize, int bmi2)
        {
            if (bmi2 != 0)
            {
                return FSE_decompress_wksp_body_bmi2(dst, dstCapacity, cSrc, cSrcSize, maxLog, workSpace, wkspSize);
            }

            return FSE_decompress_wksp_body_default(dst, dstCapacity, cSrc, cSrcSize, maxLog, workSpace, wkspSize);
        }

        /*! FSE_buildDTable():
            Builds 'dt', which must be already allocated, using FSE_createDTable().
            return : 0, or an errorCode, which can be tested using FSE_isError() */
        public static nuint FSE_buildDTable(uint* dt, short* normalizedCounter, uint maxSymbolValue, uint tableLog)
        {
            uint* wksp = stackalloc uint[8322];

            return FSE_buildDTable_wksp(dt, normalizedCounter, maxSymbolValue, tableLog, (void*)wksp, (nuint)(sizeof(uint) * 8322));
        }

        /*! FSE_decompress():
            Decompress FSE data from buffer 'cSrc', of size 'cSrcSize',
            into already allocated destination buffer 'dst', of size 'dstCapacity'.
            @return : size of regenerated data (<= maxDstSize),
                      or an error code, which can be tested using FSE_isError() .

            ** Important ** : FSE_decompress() does not decompress non-compressible nor RLE data !!!
            Why ? : making this distinction requires a header.
            Header management is intentionally delegated to the user layer, which can better manage special cases.
        */
        public static nuint FSE_decompress(void* dst, nuint dstCapacity, void* cSrc, nuint cSrcSize)
        {
            uint* wksp = stackalloc uint[5380];

            return FSE_decompress_wksp(dst, dstCapacity, cSrc, cSrcSize, (uint)((14 - 2)), (void*)wksp, (nuint)(sizeof(uint) * 5380));
        }
    }
}
