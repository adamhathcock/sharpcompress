using System;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /* FSE_buildCTable_wksp() :
     * Same as FSE_buildCTable(), but using an externally allocated scratch buffer (`workSpace`).
     * wkspSize should be sized to handle worst case situation, which is `1<<max_tableLog * sizeof(FSE_FUNCTION_TYPE)`
     * workSpace must also be properly aligned with FSE_FUNCTION_TYPE requirements
     */
    private static nuint FSE_buildCTable_wksp(
        uint* ct,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint tableLog,
        void* workSpace,
        nuint wkspSize
    )
    {
        uint tableSize = (uint)(1 << (int)tableLog);
        uint tableMask = tableSize - 1;
        void* ptr = ct;
        ushort* tableU16 = (ushort*)ptr + 2;
        /* header */
        void* FSCT = (uint*)ptr + 1 + (tableLog != 0 ? tableSize >> 1 : 1);
        FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)FSCT;
        uint step = (tableSize >> 1) + (tableSize >> 3) + 3;
        uint maxSV1 = maxSymbolValue + 1;
        /* size = maxSV1 */
        ushort* cumul = (ushort*)workSpace;
        /* size = tableSize */
        byte* tableSymbol = (byte*)(cumul + (maxSV1 + 1));
        uint highThreshold = tableSize - 1;
        assert(((nuint)workSpace & 1) == 0);
        if (
            sizeof(uint)
                * ((maxSymbolValue + 2 + (1UL << (int)tableLog)) / 2 + sizeof(ulong) / sizeof(uint))
            > wkspSize
        )
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        tableU16[-2] = (ushort)tableLog;
        tableU16[-1] = (ushort)maxSymbolValue;
        assert(tableLog < 16);
        {
            uint u;
            cumul[0] = 0;
            for (u = 1; u <= maxSV1; u++)
            {
                if (normalizedCounter[u - 1] == -1)
                {
                    cumul[u] = (ushort)(cumul[u - 1] + 1);
                    tableSymbol[highThreshold--] = (byte)(u - 1);
                }
                else
                {
                    assert(normalizedCounter[u - 1] >= 0);
                    cumul[u] = (ushort)(cumul[u - 1] + (ushort)normalizedCounter[u - 1]);
                    assert(cumul[u] >= cumul[u - 1]);
                }
            }

            cumul[maxSV1] = (ushort)(tableSize + 1);
        }

        if (highThreshold == tableSize - 1)
        {
            /* size = tableSize + 8 (may write beyond tableSize) */
            byte* spread = tableSymbol + tableSize;
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
                /* Experimentally determined optimal unroll */
                const nuint unroll = 2;
                assert(tableSize % unroll == 0);
                for (s = 0; s < tableSize; s += unroll)
                {
                    nuint u;
                    for (u = 0; u < unroll; ++u)
                    {
                        nuint uPosition = position + u * step & tableMask;
                        tableSymbol[uPosition] = spread[s + u];
                    }

                    position = position + unroll * step & tableMask;
                }

                assert(position == 0);
            }
        }
        else
        {
            uint position = 0;
            uint symbol;
            for (symbol = 0; symbol < maxSV1; symbol++)
            {
                int nbOccurrences;
                int freq = normalizedCounter[symbol];
                for (nbOccurrences = 0; nbOccurrences < freq; nbOccurrences++)
                {
                    tableSymbol[position] = (byte)symbol;
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
                /* note : static analyzer may not understand tableSymbol is properly initialized */
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
                        symbolTT[s].deltaNbBits = (tableLog + 1 << 16) - (uint)(1 << (int)tableLog);
                        break;
                    case -1:
                    case 1:
                        symbolTT[s].deltaNbBits = (tableLog << 16) - (uint)(1 << (int)tableLog);
                        assert(total <= 2147483647);
                        symbolTT[s].deltaFindState = (int)(total - 1);
                        total++;
                        break;
                    default:
                        assert(normalizedCounter[s] > 1);

                        {
                            uint maxBitsOut =
                                tableLog - ZSTD_highbit32((uint)normalizedCounter[s] - 1);
                            uint minStatePlus = (uint)normalizedCounter[s] << (int)maxBitsOut;
                            symbolTT[s].deltaNbBits = (maxBitsOut << 16) - minStatePlus;
                            symbolTT[s].deltaFindState = (int)(total - (uint)normalizedCounter[s]);
                            total += (uint)normalizedCounter[s];
                        }

                        break;
                }
            }
        }

        return 0;
    }

    /*-**************************************************************
     *  FSE NCount encoding
     ****************************************************************/
    private static nuint FSE_NCountWriteBound(uint maxSymbolValue, uint tableLog)
    {
        nuint maxHeaderSize = ((maxSymbolValue + 1) * tableLog + 4 + 2) / 8 + 1 + 2;
        return maxSymbolValue != 0 ? maxHeaderSize : 512;
    }

    private static nuint FSE_writeNCount_generic(
        void* header,
        nuint headerBufferSize,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint tableLog,
        uint writeIsSafe
    )
    {
        byte* ostart = (byte*)header;
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
        bitStream += tableLog - 5 << bitCount;
        bitCount += 4;
        remaining = tableSize + 1;
        threshold = tableSize;
        nbBits = (int)tableLog + 1;
        while (symbol < alphabetSize && remaining > 1)
        {
            if (previousIs0 != 0)
            {
                uint start = symbol;
                while (symbol < alphabetSize && normalizedCounter[symbol] == 0)
                    symbol++;
                if (symbol == alphabetSize)
                    break;
                while (symbol >= start + 24)
                {
                    start += 24;
                    bitStream += 0xFFFFU << bitCount;
                    if (writeIsSafe == 0 && @out > oend - 2)
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                    @out[0] = (byte)bitStream;
                    @out[1] = (byte)(bitStream >> 8);
                    @out += 2;
                    bitStream >>= 16;
                }

                while (symbol >= start + 3)
                {
                    start += 3;
                    bitStream += 3U << bitCount;
                    bitCount += 2;
                }

                bitStream += symbol - start << bitCount;
                bitCount += 2;
                if (bitCount > 16)
                {
                    if (writeIsSafe == 0 && @out > oend - 2)
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                    @out[0] = (byte)bitStream;
                    @out[1] = (byte)(bitStream >> 8);
                    @out += 2;
                    bitStream >>= 16;
                    bitCount -= 16;
                }
            }

            {
                int count = normalizedCounter[symbol++];
                int max = 2 * threshold - 1 - remaining;
                remaining -= count < 0 ? -count : count;
                count++;
                if (count >= threshold)
                    count += max;
                bitStream += (uint)count << bitCount;
                bitCount += nbBits;
                bitCount -= count < max ? 1 : 0;
                previousIs0 = count == 1 ? 1 : 0;
                if (remaining < 1)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
                while (remaining < threshold)
                {
                    nbBits--;
                    threshold >>= 1;
                }
            }

            if (bitCount > 16)
            {
                if (writeIsSafe == 0 && @out > oend - 2)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
                @out[0] = (byte)bitStream;
                @out[1] = (byte)(bitStream >> 8);
                @out += 2;
                bitStream >>= 16;
                bitCount -= 16;
            }
        }

        if (remaining != 1)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        assert(symbol <= alphabetSize);
        if (writeIsSafe == 0 && @out > oend - 2)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        @out[0] = (byte)bitStream;
        @out[1] = (byte)(bitStream >> 8);
        @out += (bitCount + 7) / 8;
        assert(@out >= ostart);
        return (nuint)(@out - ostart);
    }

    /*! FSE_writeNCount():
    Compactly save 'normalizedCounter' into 'buffer'.
    @return : size of the compressed table,
    or an errorCode, which can be tested using FSE_isError(). */
    private static nuint FSE_writeNCount(
        void* buffer,
        nuint bufferSize,
        short* normalizedCounter,
        uint maxSymbolValue,
        uint tableLog
    )
    {
        if (tableLog > 14 - 2)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        if (tableLog < 5)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        if (bufferSize < FSE_NCountWriteBound(maxSymbolValue, tableLog))
            return FSE_writeNCount_generic(
                buffer,
                bufferSize,
                normalizedCounter,
                maxSymbolValue,
                tableLog,
                0
            );
        return FSE_writeNCount_generic(
            buffer,
            bufferSize,
            normalizedCounter,
            maxSymbolValue,
            tableLog,
            1
        );
    }

    /* provides the minimum logSize to safely represent a distribution */
    private static uint FSE_minTableLog(nuint srcSize, uint maxSymbolValue)
    {
        uint minBitsSrc = ZSTD_highbit32((uint)srcSize) + 1;
        uint minBitsSymbols = ZSTD_highbit32(maxSymbolValue) + 2;
        uint minBits = minBitsSrc < minBitsSymbols ? minBitsSrc : minBitsSymbols;
        assert(srcSize > 1);
        return minBits;
    }

    /* *****************************************
     *  FSE advanced API
     ***************************************** */
    private static uint FSE_optimalTableLog_internal(
        uint maxTableLog,
        nuint srcSize,
        uint maxSymbolValue,
        uint minus
    )
    {
        uint maxBitsSrc = ZSTD_highbit32((uint)(srcSize - 1)) - minus;
        uint tableLog = maxTableLog;
        uint minBits = FSE_minTableLog(srcSize, maxSymbolValue);
        assert(srcSize > 1);
        if (tableLog == 0)
            tableLog = 13 - 2;
        if (maxBitsSrc < tableLog)
            tableLog = maxBitsSrc;
        if (minBits > tableLog)
            tableLog = minBits;
        if (tableLog < 5)
            tableLog = 5;
        if (tableLog > 14 - 2)
            tableLog = 14 - 2;
        return tableLog;
    }

    /*! FSE_optimalTableLog():
    dynamically downsize 'tableLog' when conditions are met.
    It saves CPU time, by using smaller tables, while preserving or even improving compression ratio.
    @return : recommended tableLog (necessarily <= 'maxTableLog') */
    private static uint FSE_optimalTableLog(uint maxTableLog, nuint srcSize, uint maxSymbolValue)
    {
        return FSE_optimalTableLog_internal(maxTableLog, srcSize, maxSymbolValue, 2);
    }

    /* Secondary normalization method.
    To be used when primary method fails. */
    private static nuint FSE_normalizeM2(
        short* norm,
        uint tableLog,
        uint* count,
        nuint total,
        uint maxSymbolValue,
        short lowProbCount
    )
    {
        const short NOT_YET_ASSIGNED = -2;
        uint s;
        uint distributed = 0;
        uint ToDistribute;
        /* Init */
        uint lowThreshold = (uint)(total >> (int)tableLog);
        uint lowOne = (uint)(total * 3 >> (int)(tableLog + 1));
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

        ToDistribute = (uint)(1 << (int)tableLog) - distributed;
        if (ToDistribute == 0)
            return 0;
        if (total / ToDistribute > lowOne)
        {
            lowOne = (uint)(total * 3 / (ToDistribute * 2));
            for (s = 0; s <= maxSymbolValue; s++)
            {
                if (norm[s] == NOT_YET_ASSIGNED && count[s] <= lowOne)
                {
                    norm[s] = 1;
                    distributed++;
                    total -= count[s];
                    continue;
                }
            }

            ToDistribute = (uint)(1 << (int)tableLog) - distributed;
        }

        if (distributed == maxSymbolValue + 1)
        {
            /* all values are pretty poor;
            probably incompressible data (should have already been detected);
            find max, then give all remaining points to max */
            uint maxV = 0,
                maxC = 0;
            for (s = 0; s <= maxSymbolValue; s++)
                if (count[s] > maxC)
                {
                    maxV = s;
                    maxC = count[s];
                }

            norm[maxV] += (short)ToDistribute;
            return 0;
        }

        if (total == 0)
        {
            for (s = 0; ToDistribute > 0; s = (s + 1) % (maxSymbolValue + 1))
                if (norm[s] > 0)
                {
                    ToDistribute--;
                    norm[s]++;
                }

            return 0;
        }

        {
            ulong vStepLog = 62 - tableLog;
            ulong mid = (1UL << (int)(vStepLog - 1)) - 1;
            /* scale on remaining */
            ulong rStep = (((ulong)1 << (int)vStepLog) * ToDistribute + mid) / (uint)total;
            ulong tmpTotal = mid;
            for (s = 0; s <= maxSymbolValue; s++)
            {
                if (norm[s] == NOT_YET_ASSIGNED)
                {
                    ulong end = tmpTotal + count[s] * rStep;
                    uint sStart = (uint)(tmpTotal >> (int)vStepLog);
                    uint sEnd = (uint)(end >> (int)vStepLog);
                    uint weight = sEnd - sStart;
                    if (weight < 1)
                        return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
                    norm[s] = (short)weight;
                    tmpTotal = end;
                }
            }
        }

        return 0;
    }

#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_rtbTable =>
        new uint[8] { 0, 473195, 504333, 520860, 550000, 700000, 750000, 830000 };
    private static uint* rtbTable =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_rtbTable)
            );
#else

    private static readonly uint* rtbTable = GetArrayPointer(
        new uint[8] { 0, 473195, 504333, 520860, 550000, 700000, 750000, 830000 }
    );
#endif
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
    private static nuint FSE_normalizeCount(
        short* normalizedCounter,
        uint tableLog,
        uint* count,
        nuint total,
        uint maxSymbolValue,
        uint useLowProbCount
    )
    {
        if (tableLog == 0)
            tableLog = 13 - 2;
        if (tableLog < 5)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        if (tableLog > 14 - 2)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        if (tableLog < FSE_minTableLog(total, maxSymbolValue))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        {
            short lowProbCount = (short)(useLowProbCount != 0 ? -1 : 1);
            ulong scale = 62 - tableLog;
            /* <== here, one division ! */
            ulong step = ((ulong)1 << 62) / (uint)total;
            ulong vStep = 1UL << (int)(scale - 20);
            int stillToDistribute = 1 << (int)tableLog;
            uint s;
            uint largest = 0;
            short largestP = 0;
            uint lowThreshold = (uint)(total >> (int)tableLog);
            for (s = 0; s <= maxSymbolValue; s++)
            {
                if (count[s] == total)
                    return 0;
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
                    short proba = (short)(count[s] * step >> (int)scale);
                    if (proba < 8)
                    {
                        ulong restToBeat = vStep * rtbTable[proba];
                        proba += (short)(
                            count[s] * step - ((ulong)proba << (int)scale) > restToBeat ? 1 : 0
                        );
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

            if (-stillToDistribute >= normalizedCounter[largest] >> 1)
            {
                /* corner case, need another normalization method */
                nuint errorCode = FSE_normalizeM2(
                    normalizedCounter,
                    tableLog,
                    count,
                    total,
                    maxSymbolValue,
                    lowProbCount
                );
                if (ERR_isError(errorCode))
                    return errorCode;
            }
            else
                normalizedCounter[largest] += (short)stillToDistribute;
        }

        return tableLog;
    }

    /* fake FSE_CTable, for rle input (always same symbol) */
    private static nuint FSE_buildCTable_rle(uint* ct, byte symbolValue)
    {
        void* ptr = ct;
        ushort* tableU16 = (ushort*)ptr + 2;
        void* FSCTptr = (uint*)ptr + 2;
        FSE_symbolCompressionTransform* symbolTT = (FSE_symbolCompressionTransform*)FSCTptr;
        tableU16[-2] = 0;
        tableU16[-1] = symbolValue;
        tableU16[0] = 0;
        tableU16[1] = 0;
        symbolTT[symbolValue].deltaNbBits = 0;
        symbolTT[symbolValue].deltaFindState = 0;
        return 0;
    }

    private static nuint FSE_compress_usingCTable_generic(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        uint* ct,
        uint fast
    )
    {
        byte* istart = (byte*)src;
        byte* iend = istart + srcSize;
        byte* ip = iend;
        BIT_CStream_t bitC;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out bitC);
        FSE_CState_t CState1,
            CState2;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out CState1);
        System.Runtime.CompilerServices.Unsafe.SkipInit(out CState2);
        if (srcSize <= 2)
            return 0;
        {
            nuint initError = BIT_initCStream(ref bitC, dst, dstSize);
            if (ERR_isError(initError))
                return 0;
        }

        nuint bitC_bitContainer = bitC.bitContainer;
        uint bitC_bitPos = bitC.bitPos;
        sbyte* bitC_ptr = bitC.ptr;
        sbyte* bitC_endPtr = bitC.endPtr;
        if ((srcSize & 1) != 0)
        {
            FSE_initCState2(ref CState1, ct, *--ip);
            FSE_initCState2(ref CState2, ct, *--ip);
            FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState1, *--ip);
            if (fast != 0)
                BIT_flushBitsFast(
                    ref bitC_bitContainer,
                    ref bitC_bitPos,
                    ref bitC_ptr,
                    bitC_endPtr
                );
            else
                BIT_flushBits(ref bitC_bitContainer, ref bitC_bitPos, ref bitC_ptr, bitC_endPtr);
        }
        else
        {
            FSE_initCState2(ref CState2, ct, *--ip);
            FSE_initCState2(ref CState1, ct, *--ip);
        }

        srcSize -= 2;
        if (sizeof(nuint) * 8 > (14 - 2) * 4 + 7 && (srcSize & 2) != 0)
        {
            FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState2, *--ip);
            FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState1, *--ip);
            if (fast != 0)
                BIT_flushBitsFast(
                    ref bitC_bitContainer,
                    ref bitC_bitPos,
                    ref bitC_ptr,
                    bitC_endPtr
                );
            else
                BIT_flushBits(ref bitC_bitContainer, ref bitC_bitPos, ref bitC_ptr, bitC_endPtr);
        }

        while (ip > istart)
        {
            FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState2, *--ip);
            if (sizeof(nuint) * 8 < (14 - 2) * 2 + 7)
                if (fast != 0)
                    BIT_flushBitsFast(
                        ref bitC_bitContainer,
                        ref bitC_bitPos,
                        ref bitC_ptr,
                        bitC_endPtr
                    );
                else
                    BIT_flushBits(
                        ref bitC_bitContainer,
                        ref bitC_bitPos,
                        ref bitC_ptr,
                        bitC_endPtr
                    );
            FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState1, *--ip);
            if (sizeof(nuint) * 8 > (14 - 2) * 4 + 7)
            {
                FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState2, *--ip);
                FSE_encodeSymbol(ref bitC_bitContainer, ref bitC_bitPos, ref CState1, *--ip);
            }

            if (fast != 0)
                BIT_flushBitsFast(
                    ref bitC_bitContainer,
                    ref bitC_bitPos,
                    ref bitC_ptr,
                    bitC_endPtr
                );
            else
                BIT_flushBits(ref bitC_bitContainer, ref bitC_bitPos, ref bitC_ptr, bitC_endPtr);
        }

        FSE_flushCState(
            ref bitC_bitContainer,
            ref bitC_bitPos,
            ref bitC_ptr,
            bitC_endPtr,
            ref CState2
        );
        FSE_flushCState(
            ref bitC_bitContainer,
            ref bitC_bitPos,
            ref bitC_ptr,
            bitC_endPtr,
            ref CState1
        );
        return BIT_closeCStream(
            ref bitC_bitContainer,
            ref bitC_bitPos,
            bitC_ptr,
            bitC_endPtr,
            bitC.startPtr
        );
    }

    /*! FSE_compress_usingCTable():
    Compress `src` using `ct` into `dst` which must be already allocated.
    @return : size of compressed data (<= `dstCapacity`),
    or 0 if compressed data could not fit into `dst`,
    or an errorCode, which can be tested using FSE_isError() */
    private static nuint FSE_compress_usingCTable(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        uint* ct
    )
    {
        uint fast = dstSize >= srcSize + (srcSize >> 7) + 4 + (nuint)sizeof(nuint) ? 1U : 0U;
        if (fast != 0)
            return FSE_compress_usingCTable_generic(dst, dstSize, src, srcSize, ct, 1);
        else
            return FSE_compress_usingCTable_generic(dst, dstSize, src, srcSize, ct, 0);
    }

    /*-*****************************************
     *  Tool functions
     ******************************************/
    private static nuint FSE_compressBound(nuint size)
    {
        return 512 + (size + (size >> 7) + 4 + (nuint)sizeof(nuint));
    }
}
