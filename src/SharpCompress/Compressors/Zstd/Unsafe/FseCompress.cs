using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /* FSE_buildCTable_wksp() :
         * Same as FSE_buildCTable(), but using an externally allocated scratch buffer (`workSpace`).
         * wkspSize should be sized to handle worst case situation, which is `1<<max_tableLog * sizeof(FSE_FUNCTION_TYPE)`
         * workSpace must also be properly aligned with FSE_FUNCTION_TYPE requirements
         */
        public static nuint FSE_buildCTable_wksp(uint* ct, short* normalizedCounter, uint maxSymbolValue, uint tableLog, void* workSpace, nuint wkspSize)
        {
            uint tableSize = (uint)(1 << (int)tableLog);
            uint tableMask = tableSize - 1;
            void* ptr = (void*)ct;
            ushort* tableU16 = ((ushort*)(ptr)) + 2;
            void* FSCT = (void*)(((uint*)(ptr)) + 1 + (tableLog != 0 ? tableSize >> 1 : 1));
            FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)(FSCT);
            uint step = (((tableSize) >> 1) + ((tableSize) >> 3) + 3);
            uint* cumul = (uint*)(workSpace);
            byte* tableSymbol = (byte*)(cumul + (maxSymbolValue + 2));
            uint highThreshold = tableSize - 1;

            if (((nuint)(workSpace) & 3) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (((nuint)(sizeof(uint)) * (maxSymbolValue + 2 + (1UL << (int)(tableLog - 2)))) > wkspSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            tableU16[-2] = (ushort)(tableLog);
            tableU16[-1] = (ushort)(maxSymbolValue);
            assert(tableLog < 16);

            {
                uint u;

                cumul[0] = 0;
                for (u = 1; u <= maxSymbolValue + 1; u++)
                {
                    if (normalizedCounter[u - 1] == -1)
                    {
                        cumul[u] = cumul[u - 1] + 1;
                        tableSymbol[highThreshold--] = (byte)(u - 1);
                    }
                    else
                    {
                        cumul[u] = cumul[u - 1] + (ushort)(normalizedCounter[u - 1]);
                    }
                }

                cumul[maxSymbolValue + 1] = tableSize + 1;
            }


            {
                uint position = 0;
                uint symbol;

                for (symbol = 0; symbol <= maxSymbolValue; symbol++)
                {
                    int nbOccurrences;
                    int freq = normalizedCounter[symbol];

                    for (nbOccurrences = 0; nbOccurrences < freq; nbOccurrences++)
                    {
                        tableSymbol[position] = (byte)(symbol);
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
                    byte s = tableSymbol[u];

                    tableU16[cumul[s]++] = (ushort)(tableSize + u);
                }
            }


            {
                uint total = 0;
                uint s;

                for (s = 0; s <= maxSymbolValue; s++)
                {
                    switch (normalizedCounter[s])
                    {
                        case 0:
                        {
                            symbolTT[s].deltaNbBits = ((tableLog + 1) << 16) - (uint)((1 << (int)tableLog));
                        }

                        break;
                        case -1:
                        case 1:
                        {
                            symbolTT[s].deltaNbBits = (tableLog << 16) - (uint)((1 << (int)tableLog));
                        }

                        symbolTT[s].deltaFindState = (int)(total - 1);
                        total++;
                        break;
                        default:
                        {
                            uint maxBitsOut = tableLog - BIT_highbit32((uint)(normalizedCounter[s] - 1));
                            uint minStatePlus = (uint)(normalizedCounter[s] << (int)maxBitsOut);

                            symbolTT[s].deltaNbBits = (maxBitsOut << 16) - minStatePlus;
                            symbolTT[s].deltaFindState = (int)(total - (ushort)(normalizedCounter[s]));
                            total += (uint)(normalizedCounter[s]);
                        }
                        break;
                    }
                }
            }

            return 0;
        }

        /*! FSE_buildCTable():
            Builds `ct`, which must be already allocated, using FSE_createCTable().
            @return : 0, or an errorCode, which can be tested using FSE_isError() */
        public static nuint FSE_buildCTable(uint* ct, short* normalizedCounter, uint maxSymbolValue, uint tableLog)
        {
            byte* tableSymbol = stackalloc byte[4096];

            return FSE_buildCTable_wksp(ct, normalizedCounter, maxSymbolValue, tableLog, (void*)tableSymbol, (nuint)(sizeof(byte) * 4096));
        }

        /*-**************************************************************
        *  FSE NCount encoding
        ****************************************************************/
        public static nuint FSE_NCountWriteBound(uint maxSymbolValue, uint tableLog)
        {
            nuint maxHeaderSize = (((maxSymbolValue + 1) * tableLog) >> 3) + 3;

            return maxSymbolValue != 0 ? maxHeaderSize : 512;
        }

        private static nuint FSE_writeNCount_generic(void* header, nuint headerBufferSize, short* normalizedCounter, uint maxSymbolValue, uint tableLog, uint writeIsSafe)
        {
            byte* ostart = (byte*)(header);
            byte* @out = ostart;
            byte* oend = ostart + headerBufferSize;
            int nbBits;
            int tableSize = 1 << (int)tableLog;
            int remaining;
            int threshold;
            uint bitStream = 0;
            int bitCount = 0;
            uint symbol = 0;
            uint alphabetSize = maxSymbolValue + 1;
            int previousIs0 = 0;

            bitStream += (tableLog - 5) << bitCount;
            bitCount += 4;
            remaining = tableSize + 1;
            threshold = tableSize;
            nbBits = (int)(tableLog + 1);
            while ((symbol < alphabetSize) && (remaining > 1))
            {
                if (previousIs0 != 0)
                {
                    uint start = symbol;

                    while ((symbol < alphabetSize) && (normalizedCounter[symbol]) == 0)
                    {
                        symbol++;
                    }

                    if (symbol == alphabetSize)
                    {
                        break;
                    }

                    while (symbol >= start + 24)
                    {
                        start += 24;
                        bitStream += 0xFFFFU << bitCount;
                        if (writeIsSafe == 0 && (@out > oend - 2))
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                        }

                        @out[0] = (byte)(bitStream);
                        @out[1] = (byte)(bitStream >> 8);
                        @out += 2;
                        bitStream >>= 16;
                    }

                    while (symbol >= start + 3)
                    {
                        start += 3;
                        bitStream += (uint)(3 << bitCount);
                        bitCount += 2;
                    }

                    bitStream += (symbol - start) << bitCount;
                    bitCount += 2;
                    if (bitCount > 16)
                    {
                        if (writeIsSafe == 0 && (@out > oend - 2))
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                        }

                        @out[0] = (byte)(bitStream);
                        @out[1] = (byte)(bitStream >> 8);
                        @out += 2;
                        bitStream >>= 16;
                        bitCount -= 16;
                    }
                }


                {
                    int count = normalizedCounter[symbol++];
                    int max = (2 * threshold - 1) - remaining;

                    remaining -= count < 0 ? -count : count;
                    count++;
                    if (count >= threshold)
                    {
                        count += max;
                    }

                    bitStream += (uint)(count << bitCount);
                    bitCount += nbBits;
                    bitCount -= ((count < max) ? 1 : 0);
                    previousIs0 = ((count == 1) ? 1 : 0);
                    if (remaining < 1)
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                    }

                    while (remaining < threshold)
                    {
                        nbBits--;
                        threshold >>= 1;
                    }
                }

                if (bitCount > 16)
                {
                    if (writeIsSafe == 0 && (@out > oend - 2))
                    {
                        return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                    }

                    @out[0] = (byte)(bitStream);
                    @out[1] = (byte)(bitStream >> 8);
                    @out += 2;
                    bitStream >>= 16;
                    bitCount -= 16;
                }
            }

            if (remaining != 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            assert(symbol <= alphabetSize);
            if (writeIsSafe == 0 && (@out > oend - 2))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            @out[0] = (byte)(bitStream);
            @out[1] = (byte)(bitStream >> 8);
            @out += (bitCount + 7) / 8;
            return (nuint)((@out - ostart));
        }

        /*! FSE_writeNCount():
            Compactly save 'normalizedCounter' into 'buffer'.
            @return : size of the compressed table,
                      or an errorCode, which can be tested using FSE_isError(). */
        public static nuint FSE_writeNCount(void* buffer, nuint bufferSize, short* normalizedCounter, uint maxSymbolValue, uint tableLog)
        {
            if (tableLog > (uint)((14 - 2)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            if (tableLog < 5)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (bufferSize < FSE_NCountWriteBound(maxSymbolValue, tableLog))
            {
                return FSE_writeNCount_generic(buffer, bufferSize, normalizedCounter, maxSymbolValue, tableLog, 0);
            }

            return FSE_writeNCount_generic(buffer, bufferSize, normalizedCounter, maxSymbolValue, tableLog, 1);
        }

        /*-**************************************************************
        *  FSE Compression Code
        ****************************************************************/
        public static uint* FSE_createCTable(uint maxSymbolValue, uint tableLog)
        {
            nuint size;

            if (tableLog > 15)
            {
                tableLog = 15;
            }

            size = ((uint)(1 + (1 << (int)((tableLog) - 1))) + (((maxSymbolValue) + 1) * 2)) * (nuint)(sizeof(uint));
            return (uint*)(malloc(size));
        }

        public static void FSE_freeCTable(uint* ct)
        {
            free((void*)(ct));
        }

        /* provides the minimum logSize to safely represent a distribution */
        [InlineMethod.Inline]
        private static uint FSE_minTableLog(nuint srcSize, uint maxSymbolValue)
        {
            uint minBitsSrc = BIT_highbit32((uint)(srcSize)) + 1;
            uint minBitsSymbols = BIT_highbit32(maxSymbolValue) + 2;
            uint minBits = minBitsSrc < minBitsSymbols ? minBitsSrc : minBitsSymbols;

            assert(srcSize > 1);
            return minBits;
        }

        /* *****************************************
         *  FSE advanced API
         ***************************************** */
        public static uint FSE_optimalTableLog_internal(uint maxTableLog, nuint srcSize, uint maxSymbolValue, uint minus)
        {
            uint maxBitsSrc = BIT_highbit32((uint)(srcSize - 1)) - minus;
            uint tableLog = maxTableLog;
            uint minBits = FSE_minTableLog(srcSize, maxSymbolValue);

            assert(srcSize > 1);
            if (tableLog == 0)
            {
                tableLog = (uint)((13 - 2));
            }

            if (maxBitsSrc < tableLog)
            {
                tableLog = maxBitsSrc;
            }

            if (minBits > tableLog)
            {
                tableLog = minBits;
            }

            if (tableLog < 5)
            {
                tableLog = 5;
            }

            if (tableLog > (uint)((14 - 2)))
            {
                tableLog = (uint)((14 - 2));
            }

            return tableLog;
        }

        /*! FSE_optimalTableLog():
            dynamically downsize 'tableLog' when conditions are met.
            It saves CPU time, by using smaller tables, while preserving or even improving compression ratio.
            @return : recommended tableLog (necessarily <= 'maxTableLog') */
        public static uint FSE_optimalTableLog(uint maxTableLog, nuint srcSize, uint maxSymbolValue)
        {
            return FSE_optimalTableLog_internal(maxTableLog, srcSize, maxSymbolValue, 2);
        }

        /* Secondary normalization method.
           To be used when primary method fails. */
        private static nuint FSE_normalizeM2(short* norm, uint tableLog, uint* count, nuint total, uint maxSymbolValue, short lowProbCount)
        {
            short NOT_YET_ASSIGNED = (short)-2;
            uint s;
            uint distributed = 0;
            uint ToDistribute;
            uint lowThreshold = (uint)(total >> (int)tableLog);
            uint lowOne = (uint)((total * 3) >> (int)(tableLog + 1));

            for (s = 0; s <= maxSymbolValue; s++)
            {
                if (count[s] == 0)
                {
                    norm[s] = 0;
                    continue;
                }

                if (count[s] <= lowThreshold)
                {
                    norm[s] = lowProbCount;
                    distributed++;
                    total -= count[s];
                    continue;
                }

                if (count[s] <= lowOne)
                {
                    norm[s] = 1;
                    distributed++;
                    total -= count[s];
                    continue;
                }

                norm[s] = NOT_YET_ASSIGNED;
            }

            ToDistribute = (uint)((1 << (int)tableLog)) - distributed;
            if (ToDistribute == 0)
            {
                return 0;
            }

            if ((total / ToDistribute) > lowOne)
            {
                lowOne = (uint)((total * 3) / (ToDistribute * 2));
                for (s = 0; s <= maxSymbolValue; s++)
                {
                    if ((norm[s] == NOT_YET_ASSIGNED) && (count[s] <= lowOne))
                    {
                        norm[s] = 1;
                        distributed++;
                        total -= count[s];
                        continue;
                    }
                }

                ToDistribute = (uint)((1 << (int)tableLog)) - distributed;
            }

            if (distributed == maxSymbolValue + 1)
            {
                uint maxV = 0, maxC = 0;

                for (s = 0; s <= maxSymbolValue; s++)
                {
                    if (count[s] > maxC)
                    {
                        maxV = s;
                        maxC = count[s];
                    }
                }

                norm[maxV] += (short)(short)(ToDistribute);
                return 0;
            }

            if (total == 0)
            {
                for (s = 0; ToDistribute > 0; s = (s + 1) % (maxSymbolValue + 1))
                {
                    if (norm[s] > 0)
                    {
                        ToDistribute--;
                        norm[s]++;
                    }
                }

                return 0;
            }


            {
                ulong vStepLog = 62 - tableLog;
                ulong mid = (1UL << (int)(vStepLog - 1)) - 1;
                ulong rStep = (((((ulong)(1) << (int)vStepLog) * ToDistribute) + mid) / ((uint)(total)));
                ulong tmpTotal = mid;

                for (s = 0; s <= maxSymbolValue; s++)
                {
                    if (norm[s] == NOT_YET_ASSIGNED)
                    {
                        ulong end = tmpTotal + (count[s] * rStep);
                        uint sStart = (uint)(tmpTotal >> (int)vStepLog);
                        uint sEnd = (uint)(end >> (int)vStepLog);
                        uint weight = sEnd - sStart;

                        if (weight < 1)
                        {
                            return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                        }

                        norm[s] = (short)(weight);
                        tmpTotal = end;
                    }
                }
            }

            return 0;
        }

        /*! FSE_normalizeCount():
            normalize counts so that sum(count[]) == Power_of_2 (2^tableLog)
            'normalizedCounter' is a table of short, of minimum size (maxSymbolValue+1).
            useLowProbCount is a boolean parameter which trades off compressed size for
            faster header decoding. When it is set to 1, the compressed data will be slightly
            smaller. And when it is set to 0, FSE_readNCount() and FSE_buildDTable() will be
            faster. If you are compressing a small amount of data (< 2 KB) then useLowProbCount=0
            is a good default, since header deserialization makes a big speed difference.
            Otherwise, useLowProbCount=1 is a good default, since the speed difference is small.
            @return : tableLog,
                      or an errorCode, which can be tested using FSE_isError() */
        public static nuint FSE_normalizeCount(short* normalizedCounter, uint tableLog, uint* count, nuint total, uint maxSymbolValue, uint useLowProbCount)
        {
            if (tableLog == 0)
            {
                tableLog = (uint)((13 - 2));
            }

            if (tableLog < 5)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (tableLog > (uint)((14 - 2)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            if (tableLog < FSE_minTableLog(total, maxSymbolValue))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }


            {

                short lowProbCount = (short)(useLowProbCount != 0 ? -1 : 1);
                ulong scale = 62 - tableLog;
                ulong step = (((ulong)(1) << 62) / ((uint)(total)));
                ulong vStep = 1UL << (int)(scale - 20);
                int stillToDistribute = 1 << (int)tableLog;
                uint s;
                uint largest = 0;
                short largestP = 0;
                uint lowThreshold = (uint)(total >> (int)tableLog);

                for (s = 0; s <= maxSymbolValue; s++)
                {
                    if (count[s] == total)
                    {
                        return 0;
                    }

                    if (count[s] == 0)
                    {
                        normalizedCounter[s] = 0;
                        continue;
                    }

                    if (count[s] <= lowThreshold)
                    {
                        normalizedCounter[s] = lowProbCount;
                        stillToDistribute--;
                    }
                    else
                    {
                        short proba = (short)((count[s] * step) >> (int)scale);

                        if (proba < 8)
                        {
                            ulong restToBeat = vStep * rtbTable[proba];

                            proba += (short)((((count[s] * step) - ((ulong)(proba) << (int)scale) > restToBeat) ? 1 : 0));
                        }

                        if (proba > largestP)
                        {
                            largestP = proba;
                            largest = s;
                        }

                        normalizedCounter[s] = proba;
                        stillToDistribute -= proba;
                    }
                }

                if (-stillToDistribute >= (normalizedCounter[largest] >> 1))
                {
                    nuint errorCode = FSE_normalizeM2(normalizedCounter, tableLog, count, total, maxSymbolValue, lowProbCount);

                    if ((ERR_isError(errorCode)) != 0)
                    {
                        return errorCode;
                    }
                }
                else
                {
                    normalizedCounter[largest] += (short)(short)(stillToDistribute);
                }
            }

            return tableLog;
        }

        /* fake FSE_CTable, for raw (uncompressed) input */
        public static nuint FSE_buildCTable_raw(uint* ct, uint nbBits)
        {
            uint tableSize = (uint)(1 << (int)nbBits);
            uint tableMask = tableSize - 1;
            uint maxSymbolValue = tableMask;
            void* ptr = (void*)ct;
            ushort* tableU16 = ((ushort*)(ptr)) + 2;
            void* FSCT = (void*)(((uint*)(ptr)) + 1 + (tableSize >> 1));
            FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)(FSCT);
            uint s;

            if (nbBits < 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            tableU16[-2] = (ushort)(nbBits);
            tableU16[-1] = (ushort)(maxSymbolValue);
            for (s = 0; s < tableSize; s++)
            {
                tableU16[s] = (ushort)(tableSize + s);
            }


            {
                uint deltaNbBits = (nbBits << 16) - (uint)((1 << (int)nbBits));

                for (s = 0; s <= maxSymbolValue; s++)
                {
                    symbolTT[s].deltaNbBits = deltaNbBits;
                    symbolTT[s].deltaFindState = (int)(s - 1);
                }
            }

            return 0;
        }

        /* fake FSE_CTable, for rle input (always same symbol) */
        public static nuint FSE_buildCTable_rle(uint* ct, byte symbolValue)
        {
            void* ptr = (void*)ct;
            ushort* tableU16 = ((ushort*)(ptr)) + 2;
            void* FSCTptr = (void*)((uint*)(ptr) + 2);
            FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)(FSCTptr);

            tableU16[-2] = (ushort)(0);
            tableU16[-1] = (ushort)(symbolValue);
            tableU16[0] = 0;
            tableU16[1] = 0;
            symbolTT[symbolValue].deltaNbBits = 0;
            symbolTT[symbolValue].deltaFindState = 0;
            return 0;
        }

        private static nuint FSE_compress_usingCTable_generic(void* dst, nuint dstSize, void* src, nuint srcSize, uint* ct, uint fast)
        {
            byte* istart = (byte*)(src);
            byte* iend = istart + srcSize;
            byte* ip = iend;
            BIT_CStream_t bitC;
            FSE_CState_t CState1, CState2;

            if (srcSize <= 2)
            {
                return 0;
            }


            {
                nuint initError = BIT_initCStream(&bitC, dst, dstSize);

                if ((ERR_isError(initError)) != 0)
                {
                    return 0;
                }
            }

            if ((srcSize & 1) != 0)
            {
                FSE_initCState2(&CState1, ct, *--ip);
                FSE_initCState2(&CState2, ct, *--ip);
                FSE_encodeSymbol(&bitC, &CState1, *--ip);
                if (fast != 0)
                {
                    BIT_flushBitsFast(&bitC);
                }
                else
                {
                    BIT_flushBits(&bitC);
                }

            }
            else
            {
                FSE_initCState2(&CState2, ct, *--ip);
                FSE_initCState2(&CState1, ct, *--ip);
            }

            srcSize -= 2;
            if (((nuint)(sizeof(nuint)) * 8 > (uint)((14 - 2) * 4 + 7)) && (srcSize & 2) != 0)
            {
                FSE_encodeSymbol(&bitC, &CState2, *--ip);
                FSE_encodeSymbol(&bitC, &CState1, *--ip);
                if (fast != 0)
                {
                    BIT_flushBitsFast(&bitC);
                }
                else
                {
                    BIT_flushBits(&bitC);
                }

            }

            while (ip > istart)
            {
                FSE_encodeSymbol(&bitC, &CState2, *--ip);
                if ((nuint)(sizeof(nuint)) * 8 < (uint)((14 - 2) * 2 + 7))
                {
                    if (fast != 0)
                    {
                        BIT_flushBitsFast(&bitC);
                    }
                    else
                    {
                        BIT_flushBits(&bitC);
                    }
                }

                FSE_encodeSymbol(&bitC, &CState1, *--ip);
                if ((nuint)(sizeof(nuint)) * 8 > (uint)((14 - 2) * 4 + 7))
                {
                    FSE_encodeSymbol(&bitC, &CState2, *--ip);
                    FSE_encodeSymbol(&bitC, &CState1, *--ip);
                }

                if (fast != 0)
                {
                    BIT_flushBitsFast(&bitC);
                }
                else
                {
                    BIT_flushBits(&bitC);
                }

            }

            FSE_flushCState(&bitC, &CState2);
            FSE_flushCState(&bitC, &CState1);
            return BIT_closeCStream(&bitC);
        }

        /*! FSE_compress_usingCTable():
            Compress `src` using `ct` into `dst` which must be already allocated.
            @return : size of compressed data (<= `dstCapacity`),
                      or 0 if compressed data could not fit into `dst`,
                      or an errorCode, which can be tested using FSE_isError() */
        public static nuint FSE_compress_usingCTable(void* dst, nuint dstSize, void* src, nuint srcSize, uint* ct)
        {
            uint fast = (((dstSize >= ((srcSize) + ((srcSize) >> 7) + 4 + (nuint)(sizeof(nuint))))) ? 1U : 0U);

            if (fast != 0)
            {
                return FSE_compress_usingCTable_generic(dst, dstSize, src, srcSize, ct, 1);
            }
            else
            {
                return FSE_compress_usingCTable_generic(dst, dstSize, src, srcSize, ct, 0);
            }
        }

        /*-*****************************************
        *  Tool functions
        ******************************************/
        public static nuint FSE_compressBound(nuint size)
        {
            return (512 + ((size) + ((size) >> 7) + 4 + (nuint)(sizeof(nuint))));
        }

        /* FSE_compress_wksp() :
         * Same as FSE_compress2(), but using an externally allocated scratch buffer (`workSpace`).
         * `wkspSize` size must be `(1<<tableLog)`.
         */
        public static nuint FSE_compress_wksp(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint tableLog, void* workSpace, nuint wkspSize)
        {
            byte* ostart = (byte*)(dst);
            byte* op = ostart;
            byte* oend = ostart + dstSize;
            uint* count = stackalloc uint[256];
            short* norm = stackalloc short[256];
            uint* CTable = (uint*)(workSpace);
            nuint CTableSize = ((uint)(1 + (1 << (int)((tableLog) - 1))) + (((maxSymbolValue) + 1) * 2));
            void* scratchBuffer = (void*)(CTable + CTableSize);
            nuint scratchBufferSize = wkspSize - (CTableSize * (nuint)(4));

            if (wkspSize < (((uint)(1 + (1 << (int)((tableLog) - 1))) + (((maxSymbolValue) + 1) * 2)) + (uint)(((tableLog > 12) ? (1 << (int)(tableLog - 2)) : 1024))))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            if (srcSize <= 1)
            {
                return 0;
            }

            if (maxSymbolValue == 0)
            {
                maxSymbolValue = 255;
            }

            if (tableLog == 0)
            {
                tableLog = (uint)((13 - 2));
            }


            {
                nuint maxCount = HIST_count_wksp((uint*)count, &maxSymbolValue, src, srcSize, scratchBuffer, scratchBufferSize);

                if ((ERR_isError(maxCount)) != 0)
                {
                    return maxCount;
                }

                if (maxCount == srcSize)
                {
                    return 1;
                }

                if (maxCount == 1)
                {
                    return 0;
                }

                if (maxCount < (srcSize >> 7))
                {
                    return 0;
                }
            }

            tableLog = FSE_optimalTableLog(tableLog, srcSize, maxSymbolValue);

            {
                nuint _var_err__ = FSE_normalizeCount((short*)norm, tableLog, (uint*)count, srcSize, maxSymbolValue, ((srcSize >= 2048) ? 1U : 0U));

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }


            {
                nuint nc_err = FSE_writeNCount((void*)op, (nuint)(oend - op), (short*)norm, maxSymbolValue, tableLog);

                if ((ERR_isError(nc_err)) != 0)
                {
                    return nc_err;
                }

                op += nc_err;
            }


            {
                nuint _var_err__ = FSE_buildCTable_wksp(CTable, (short*)norm, maxSymbolValue, tableLog, scratchBuffer, scratchBufferSize);

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }


            {
                nuint cSize = FSE_compress_usingCTable((void*)op, (nuint)(oend - op), src, srcSize, CTable);

                if ((ERR_isError(cSize)) != 0)
                {
                    return cSize;
                }

                if (cSize == 0)
                {
                    return 0;
                }

                op += cSize;
            }

            if ((nuint)(op - ostart) >= srcSize - 1)
            {
                return 0;
            }

            return (nuint)(op - ostart);
        }

        /*-*****************************************
        *  FSE advanced functions
        ******************************************/
        /*! FSE_compress2() :
            Same as FSE_compress(), but allows the selection of 'maxSymbolValue' and 'tableLog'
            Both parameters can be defined as '0' to mean : use default value
            @return : size of compressed data
            Special values : if return == 0, srcData is not compressible => Nothing is stored within cSrc !!!
                             if return == 1, srcData is a single byte symbol * srcSize times. Use RLE compression.
                             if FSE_isError(return), it's an error code.
        */
        public static nuint FSE_compress2(void* dst, nuint dstCapacity, void* src, nuint srcSize, uint maxSymbolValue, uint tableLog)
        {
            fseWkspMax_t scratchBuffer;

            if (tableLog > (uint)((14 - 2)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            return FSE_compress_wksp(dst, dstCapacity, src, srcSize, maxSymbolValue, tableLog, (void*)&scratchBuffer, (nuint)(sizeof(fseWkspMax_t)));
        }

        /*-****************************************
        *  FSE simple functions
        ******************************************/
        /*! FSE_compress() :
            Compress content of buffer 'src', of size 'srcSize', into destination buffer 'dst'.
            'dst' buffer must be already allocated. Compression runs faster is dstCapacity >= FSE_compressBound(srcSize).
            @return : size of compressed data (<= dstCapacity).
            Special values : if return == 0, srcData is not compressible => Nothing is stored within dst !!!
                             if return == 1, srcData is a single byte symbol * srcSize times. Use RLE compression instead.
                             if FSE_isError(return), compression failed (more details using FSE_getErrorName())
        */
        public static nuint FSE_compress(void* dst, nuint dstCapacity, void* src, nuint srcSize)
        {
            return FSE_compress2(dst, dstCapacity, src, srcSize, 255, (uint)((13 - 2)));
        }
    }
}
