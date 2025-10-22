using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*===   Version   ===*/
    private static uint FSE_versionNumber()
    {
        return 0 * 100 * 100 + 9 * 100 + 0;
    }

    /*===   Error Management   ===*/
    private static bool FSE_isError(nuint code)
    {
        return ERR_isError(code);
    }

    private static string FSE_getErrorName(nuint code)
    {
        return ERR_getErrorName(code);
    }

    /* Error Management */
    private static bool HUF_isError(nuint code)
    {
        return ERR_isError(code);
    }

    private static string HUF_getErrorName(nuint code)
    {
        return ERR_getErrorName(code);
    }

    /*-**************************************************************
     *  FSE NCount encoding-decoding
     ****************************************************************/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint FSE_readNCount_body(
        short* normalizedCounter,
        uint* maxSVPtr,
        uint* tableLogPtr,
        void* headerBuffer,
        nuint hbSize
    )
    {
        byte* istart = (byte*)headerBuffer;
        byte* iend = istart + hbSize;
        byte* ip = istart;
        int nbBits;
        int remaining;
        int threshold;
        uint bitStream;
        int bitCount;
        uint charnum = 0;
        uint maxSV1 = *maxSVPtr + 1;
        int previous0 = 0;
        if (hbSize < 8)
        {
            sbyte* buffer = stackalloc sbyte[8];
            /* This function only works when hbSize >= 8 */
            memset(buffer, 0, sizeof(sbyte) * 8);
            memcpy(buffer, headerBuffer, (uint)hbSize);
            {
                nuint countSize = FSE_readNCount(
                    normalizedCounter,
                    maxSVPtr,
                    tableLogPtr,
                    buffer,
                    sizeof(sbyte) * 8
                );
                if (FSE_isError(countSize))
                    return countSize;
                if (countSize > hbSize)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                return countSize;
            }
        }

        assert(hbSize >= 8);
        memset(normalizedCounter, 0, (*maxSVPtr + 1) * sizeof(short));
        bitStream = MEM_readLE32(ip);
        nbBits = (int)((bitStream & 0xF) + 5);
        if (nbBits > 15)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        bitStream >>= 4;
        bitCount = 4;
        *tableLogPtr = (uint)nbBits;
        remaining = (1 << nbBits) + 1;
        threshold = 1 << nbBits;
        nbBits++;
        for (; ; )
        {
            if (previous0 != 0)
            {
                /* Count the number of repeats. Each time the
                 * 2-bit repeat code is 0b11 there is another
                 * repeat.
                 * Avoid UB by setting the high bit to 1.
                 */
                int repeats = (int)(ZSTD_countTrailingZeros32(~bitStream | 0x80000000) >> 1);
                while (repeats >= 12)
                {
                    charnum += 3 * 12;
                    if (ip <= iend - 7)
                    {
                        ip += 3;
                    }
                    else
                    {
                        bitCount -= (int)(8 * (iend - 7 - ip));
                        bitCount &= 31;
                        ip = iend - 4;
                    }

                    bitStream = MEM_readLE32(ip) >> bitCount;
                    repeats = (int)(ZSTD_countTrailingZeros32(~bitStream | 0x80000000) >> 1);
                }

                charnum += (uint)(3 * repeats);
                bitStream >>= 2 * repeats;
                bitCount += 2 * repeats;
                assert((bitStream & 3) < 3);
                charnum += bitStream & 3;
                bitCount += 2;
                if (charnum >= maxSV1)
                    break;
                if (ip <= iend - 7 || ip + (bitCount >> 3) <= iend - 4)
                {
                    assert(bitCount >> 3 <= 3);
                    ip += bitCount >> 3;
                    bitCount &= 7;
                }
                else
                {
                    bitCount -= (int)(8 * (iend - 4 - ip));
                    bitCount &= 31;
                    ip = iend - 4;
                }

                bitStream = MEM_readLE32(ip) >> bitCount;
            }

            {
                int max = 2 * threshold - 1 - remaining;
                int count;
                if ((bitStream & (uint)(threshold - 1)) < (uint)max)
                {
                    count = (int)(bitStream & (uint)(threshold - 1));
                    bitCount += nbBits - 1;
                }
                else
                {
                    count = (int)(bitStream & (uint)(2 * threshold - 1));
                    if (count >= threshold)
                        count -= max;
                    bitCount += nbBits;
                }

                count--;
                if (count >= 0)
                {
                    remaining -= count;
                }
                else
                {
                    assert(count == -1);
                    remaining += count;
                }

                normalizedCounter[charnum++] = (short)count;
                previous0 = count == 0 ? 1 : 0;
                assert(threshold > 1);
                if (remaining < threshold)
                {
                    if (remaining <= 1)
                        break;
                    nbBits = (int)(ZSTD_highbit32((uint)remaining) + 1);
                    threshold = 1 << nbBits - 1;
                }

                if (charnum >= maxSV1)
                    break;
                if (ip <= iend - 7 || ip + (bitCount >> 3) <= iend - 4)
                {
                    ip += bitCount >> 3;
                    bitCount &= 7;
                }
                else
                {
                    bitCount -= (int)(8 * (iend - 4 - ip));
                    bitCount &= 31;
                    ip = iend - 4;
                }

                bitStream = MEM_readLE32(ip) >> bitCount;
            }
        }

        if (remaining != 1)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        if (charnum > maxSV1)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooSmall));
        if (bitCount > 32)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        *maxSVPtr = charnum - 1;
        ip += bitCount + 7 >> 3;
        return (nuint)(ip - istart);
    }

    /* Avoids the FORCE_INLINE of the _body() function. */
    private static nuint FSE_readNCount_body_default(
        short* normalizedCounter,
        uint* maxSVPtr,
        uint* tableLogPtr,
        void* headerBuffer,
        nuint hbSize
    )
    {
        return FSE_readNCount_body(normalizedCounter, maxSVPtr, tableLogPtr, headerBuffer, hbSize);
    }

    /*! FSE_readNCount_bmi2():
     * Same as FSE_readNCount() but pass bmi2=1 when your CPU supports BMI2 and 0 otherwise.
     */
    private static nuint FSE_readNCount_bmi2(
        short* normalizedCounter,
        uint* maxSVPtr,
        uint* tableLogPtr,
        void* headerBuffer,
        nuint hbSize,
        int bmi2
    )
    {
        return FSE_readNCount_body_default(
            normalizedCounter,
            maxSVPtr,
            tableLogPtr,
            headerBuffer,
            hbSize
        );
    }

    /*! FSE_readNCount():
    Read compactly saved 'normalizedCounter' from 'rBuffer'.
    @return : size read from 'rBuffer',
    or an errorCode, which can be tested using FSE_isError().
    maxSymbolValuePtr[0] and tableLogPtr[0] will also be updated with their respective values */
    private static nuint FSE_readNCount(
        short* normalizedCounter,
        uint* maxSVPtr,
        uint* tableLogPtr,
        void* headerBuffer,
        nuint hbSize
    )
    {
        return FSE_readNCount_bmi2(
            normalizedCounter,
            maxSVPtr,
            tableLogPtr,
            headerBuffer,
            hbSize,
            0
        );
    }

    /*! HUF_readStats() :
    Read compact Huffman tree, saved by HUF_writeCTable().
    `huffWeight` is destination buffer.
    `rankStats` is assumed to be a table of at least HUF_TABLELOG_MAX U32.
    @return : size read from `src` , or an error Code .
    Note : Needed by HUF_readCTable() and HUF_readDTableX?() .
     */
    private static nuint HUF_readStats(
        byte* huffWeight,
        nuint hwSize,
        uint* rankStats,
        uint* nbSymbolsPtr,
        uint* tableLogPtr,
        void* src,
        nuint srcSize
    )
    {
        uint* wksp = stackalloc uint[219];
        return HUF_readStats_wksp(
            huffWeight,
            hwSize,
            rankStats,
            nbSymbolsPtr,
            tableLogPtr,
            src,
            srcSize,
            wksp,
            sizeof(uint) * 219,
            0
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_readStats_body(
        byte* huffWeight,
        nuint hwSize,
        uint* rankStats,
        uint* nbSymbolsPtr,
        uint* tableLogPtr,
        void* src,
        nuint srcSize,
        void* workSpace,
        nuint wkspSize,
        int bmi2
    )
    {
        uint weightTotal;
        byte* ip = (byte*)src;
        nuint iSize;
        nuint oSize;
        if (srcSize == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        iSize = ip[0];
        if (iSize >= 128)
        {
            oSize = iSize - 127;
            iSize = (oSize + 1) / 2;
            if (iSize + 1 > srcSize)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
            if (oSize >= hwSize)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            ip += 1;
            {
                uint n;
                for (n = 0; n < oSize; n += 2)
                {
                    huffWeight[n] = (byte)(ip[n / 2] >> 4);
                    huffWeight[n + 1] = (byte)(ip[n / 2] & 15);
                }
            }
        }
        else
        {
            if (iSize + 1 > srcSize)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
            oSize = FSE_decompress_wksp_bmi2(
                huffWeight,
                hwSize - 1,
                ip + 1,
                iSize,
                6,
                workSpace,
                wkspSize,
                bmi2
            );
            if (FSE_isError(oSize))
                return oSize;
        }

        memset(rankStats, 0, (12 + 1) * sizeof(uint));
        weightTotal = 0;
        {
            uint n;
            for (n = 0; n < oSize; n++)
            {
                if (huffWeight[n] > 12)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                rankStats[huffWeight[n]]++;
                weightTotal += (uint)(1 << huffWeight[n] >> 1);
            }
        }

        if (weightTotal == 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        {
            uint tableLog = ZSTD_highbit32(weightTotal) + 1;
            if (tableLog > 12)
                return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
            *tableLogPtr = tableLog;
            {
                uint total = (uint)(1 << (int)tableLog);
                uint rest = total - weightTotal;
                uint verif = (uint)(1 << (int)ZSTD_highbit32(rest));
                uint lastWeight = ZSTD_highbit32(rest) + 1;
                if (verif != rest)
                    return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
                huffWeight[oSize] = (byte)lastWeight;
                rankStats[lastWeight]++;
            }
        }

        if (rankStats[1] < 2 || (rankStats[1] & 1) != 0)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_corruption_detected));
        *nbSymbolsPtr = (uint)(oSize + 1);
        return iSize + 1;
    }

    /* Avoids the FORCE_INLINE of the _body() function. */
    private static nuint HUF_readStats_body_default(
        byte* huffWeight,
        nuint hwSize,
        uint* rankStats,
        uint* nbSymbolsPtr,
        uint* tableLogPtr,
        void* src,
        nuint srcSize,
        void* workSpace,
        nuint wkspSize
    )
    {
        return HUF_readStats_body(
            huffWeight,
            hwSize,
            rankStats,
            nbSymbolsPtr,
            tableLogPtr,
            src,
            srcSize,
            workSpace,
            wkspSize,
            0
        );
    }

    private static nuint HUF_readStats_wksp(
        byte* huffWeight,
        nuint hwSize,
        uint* rankStats,
        uint* nbSymbolsPtr,
        uint* tableLogPtr,
        void* src,
        nuint srcSize,
        void* workSpace,
        nuint wkspSize,
        int flags
    )
    {
        return HUF_readStats_body_default(
            huffWeight,
            hwSize,
            rankStats,
            nbSymbolsPtr,
            tableLogPtr,
            src,
            srcSize,
            workSpace,
            wkspSize
        );
    }
}
