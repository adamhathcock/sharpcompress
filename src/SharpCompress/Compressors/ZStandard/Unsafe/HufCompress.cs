using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static void* HUF_alignUpWorkspace(void* workspace, nuint* workspaceSizePtr, nuint align)
    {
        nuint mask = align - 1;
        nuint rem = (nuint)workspace & mask;
        nuint add = align - rem & mask;
        byte* aligned = (byte*)workspace + add;
        assert((align & align - 1) == 0);
        assert(align <= 8);
        if (*workspaceSizePtr >= add)
        {
            assert(add < align);
            assert(((nuint)aligned & mask) == 0);
            *workspaceSizePtr -= add;
            return aligned;
        }
        else
        {
            *workspaceSizePtr = 0;
            return null;
        }
    }

    private static nuint HUF_compressWeights(
        void* dst,
        nuint dstSize,
        void* weightTable,
        nuint wtSize,
        void* workspace,
        nuint workspaceSize
    )
    {
        byte* ostart = (byte*)dst;
        byte* op = ostart;
        byte* oend = ostart + dstSize;
        uint maxSymbolValue = 12;
        uint tableLog = 6;
        HUF_CompressWeightsWksp* wksp = (HUF_CompressWeightsWksp*)HUF_alignUpWorkspace(
            workspace,
            &workspaceSize,
            sizeof(uint)
        );
        if (workspaceSize < (nuint)sizeof(HUF_CompressWeightsWksp))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        if (wtSize <= 1)
            return 0;
        {
            /* never fails */
            uint maxCount = HIST_count_simple(wksp->count, &maxSymbolValue, weightTable, wtSize);
            if (maxCount == wtSize)
                return 1;
            if (maxCount == 1)
                return 0;
        }

        tableLog = FSE_optimalTableLog(tableLog, wtSize, maxSymbolValue);
        {
            /* useLowProbCount */
            nuint _var_err__ = FSE_normalizeCount(
                wksp->norm,
                tableLog,
                wksp->count,
                wtSize,
                maxSymbolValue,
                0
            );
            if (ERR_isError(_var_err__))
                return _var_err__;
        }

        {
            nuint hSize = FSE_writeNCount(
                op,
                (nuint)(oend - op),
                wksp->norm,
                maxSymbolValue,
                tableLog
            );
            if (ERR_isError(hSize))
                return hSize;
            op += hSize;
        }

        {
            /* Compress */
            nuint _var_err__ = FSE_buildCTable_wksp(
                wksp->CTable,
                wksp->norm,
                maxSymbolValue,
                tableLog,
                wksp->scratchBuffer,
                sizeof(uint) * 41
            );
            if (ERR_isError(_var_err__))
                return _var_err__;
        }

        {
            nuint cSize = FSE_compress_usingCTable(
                op,
                (nuint)(oend - op),
                weightTable,
                wtSize,
                wksp->CTable
            );
            if (ERR_isError(cSize))
                return cSize;
            if (cSize == 0)
                return 0;
            op += cSize;
        }

        return (nuint)(op - ostart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_getNbBits(nuint elt)
    {
        return elt & 0xFF;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_getNbBitsFast(nuint elt)
    {
        return elt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_getValue(nuint elt)
    {
        return elt & ~(nuint)0xFF;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint HUF_getValueFast(nuint elt)
    {
        return elt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_setNbBits(nuint* elt, nuint nbBits)
    {
        assert(nbBits <= 12);
        *elt = nbBits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_setValue(nuint* elt, nuint value)
    {
        nuint nbBits = HUF_getNbBits(*elt);
        if (nbBits > 0)
        {
            assert(value >> (int)nbBits == 0);
            *elt |= value << (int)((nuint)(sizeof(nuint) * 8) - nbBits);
        }
    }

    /** HUF_readCTableHeader() :
     *  @returns The header from the CTable specifying the tableLog and the maxSymbolValue.
     */
    private static HUF_CTableHeader HUF_readCTableHeader(nuint* ctable)
    {
        HUF_CTableHeader header;
        memcpy(&header, ctable, (uint)sizeof(nuint));
        return header;
    }

    private static void HUF_writeCTableHeader(nuint* ctable, uint tableLog, uint maxSymbolValue)
    {
        HUF_CTableHeader header;
        memset(&header, 0, (uint)sizeof(nuint));
        assert(tableLog < 256);
        header.tableLog = (byte)tableLog;
        assert(maxSymbolValue < 256);
        header.maxSymbolValue = (byte)maxSymbolValue;
        memcpy(ctable, &header, (uint)sizeof(nuint));
    }

    private static nuint HUF_writeCTable_wksp(
        void* dst,
        nuint maxDstSize,
        nuint* CTable,
        uint maxSymbolValue,
        uint huffLog,
        void* workspace,
        nuint workspaceSize
    )
    {
        nuint* ct = CTable + 1;
        byte* op = (byte*)dst;
        uint n;
        HUF_WriteCTableWksp* wksp = (HUF_WriteCTableWksp*)HUF_alignUpWorkspace(
            workspace,
            &workspaceSize,
            sizeof(uint)
        );
        assert(HUF_readCTableHeader(CTable).maxSymbolValue == maxSymbolValue);
        assert(HUF_readCTableHeader(CTable).tableLog == huffLog);
        if (workspaceSize < (nuint)sizeof(HUF_WriteCTableWksp))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        if (maxSymbolValue > 255)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge));
        wksp->bitsToWeight[0] = 0;
        for (n = 1; n < huffLog + 1; n++)
            wksp->bitsToWeight[n] = (byte)(huffLog + 1 - n);
        for (n = 0; n < maxSymbolValue; n++)
            wksp->huffWeight[n] = wksp->bitsToWeight[HUF_getNbBits(ct[n])];
        if (maxDstSize < 1)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        {
            nuint hSize = HUF_compressWeights(
                op + 1,
                maxDstSize - 1,
                wksp->huffWeight,
                maxSymbolValue,
                &wksp->wksp,
                (nuint)sizeof(HUF_CompressWeightsWksp)
            );
            if (ERR_isError(hSize))
                return hSize;
            if (hSize > 1 && hSize < maxSymbolValue / 2)
            {
                op[0] = (byte)hSize;
                return hSize + 1;
            }
        }

        if (maxSymbolValue > 256 - 128)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        if ((maxSymbolValue + 1) / 2 + 1 > maxDstSize)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        op[0] = (byte)(128 + (maxSymbolValue - 1));
        wksp->huffWeight[maxSymbolValue] = 0;
        for (n = 0; n < maxSymbolValue; n += 2)
            op[n / 2 + 1] = (byte)((wksp->huffWeight[n] << 4) + wksp->huffWeight[n + 1]);
        return (maxSymbolValue + 1) / 2 + 1;
    }

    /** HUF_readCTable() :
     *  Loading a CTable saved with HUF_writeCTable() */
    private static nuint HUF_readCTable(
        nuint* CTable,
        uint* maxSymbolValuePtr,
        void* src,
        nuint srcSize,
        uint* hasZeroWeights
    )
    {
        /* init not required, even though some static analyzer may complain */
        byte* huffWeight = stackalloc byte[256];
        /* large enough for values from 0 to 16 */
        uint* rankVal = stackalloc uint[13];
        uint tableLog = 0;
        uint nbSymbols = 0;
        nuint* ct = CTable + 1;
        /* get symbol weights */
        nuint readSize = HUF_readStats(
            huffWeight,
            255 + 1,
            rankVal,
            &nbSymbols,
            &tableLog,
            src,
            srcSize
        );
        if (ERR_isError(readSize))
            return readSize;
        *hasZeroWeights = rankVal[0] > 0 ? 1U : 0U;
        if (tableLog > 12)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        if (nbSymbols > *maxSymbolValuePtr + 1)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooSmall));
        *maxSymbolValuePtr = nbSymbols - 1;
        HUF_writeCTableHeader(CTable, tableLog, *maxSymbolValuePtr);
        {
            uint n,
                nextRankStart = 0;
            for (n = 1; n <= tableLog; n++)
            {
                uint curr = nextRankStart;
                nextRankStart += rankVal[n] << (int)(n - 1);
                rankVal[n] = curr;
            }
        }

        {
            uint n;
            for (n = 0; n < nbSymbols; n++)
            {
                uint w = huffWeight[n];
                HUF_setNbBits(ct + n, (nuint)((byte)(tableLog + 1 - w) & -(w != 0 ? 1 : 0)));
            }
        }

        {
            ushort* nbPerRank = stackalloc ushort[14];
            /* support w=0=>n=tableLog+1 */
            memset(nbPerRank, 0, sizeof(ushort) * 14);
            ushort* valPerRank = stackalloc ushort[14];
            memset(valPerRank, 0, sizeof(ushort) * 14);
            {
                uint n;
                for (n = 0; n < nbSymbols; n++)
                    nbPerRank[HUF_getNbBits(ct[n])]++;
            }

            valPerRank[tableLog + 1] = 0;
            {
                ushort min = 0;
                /* start at n=tablelog <-> w=1 */
                uint n;
                for (n = tableLog; n > 0; n--)
                {
                    valPerRank[n] = min;
                    min += nbPerRank[n];
                    min >>= 1;
                }
            }

            {
                uint n;
                for (n = 0; n < nbSymbols; n++)
                    HUF_setValue(ct + n, valPerRank[HUF_getNbBits(ct[n])]++);
            }
        }

        return readSize;
    }

    /** HUF_getNbBitsFromCTable() :
     *  Read nbBits from CTable symbolTable, for symbol `symbolValue` presumed <= HUF_SYMBOLVALUE_MAX
     *  Note 1 : If symbolValue > HUF_readCTableHeader(symbolTable).maxSymbolValue, returns 0
     *  Note 2 : is not inlined, as HUF_CElt definition is private
     */
    private static uint HUF_getNbBitsFromCTable(nuint* CTable, uint symbolValue)
    {
        nuint* ct = CTable + 1;
        assert(symbolValue <= 255);
        if (symbolValue > HUF_readCTableHeader(CTable).maxSymbolValue)
            return 0;
        return (uint)HUF_getNbBits(ct[symbolValue]);
    }

    /**
     * HUF_setMaxHeight():
     * Try to enforce @targetNbBits on the Huffman tree described in @huffNode.
     *
     * It attempts to convert all nodes with nbBits > @targetNbBits
     * to employ @targetNbBits instead. Then it adjusts the tree
     * so that it remains a valid canonical Huffman tree.
     *
     * @pre               The sum of the ranks of each symbol == 2^largestBits,
     *                    where largestBits == huffNode[lastNonNull].nbBits.
     * @post              The sum of the ranks of each symbol == 2^largestBits,
     *                    where largestBits is the return value (expected <= targetNbBits).
     *
     * @param huffNode    The Huffman tree modified in place to enforce targetNbBits.
     *                    It's presumed sorted, from most frequent to rarest symbol.
     * @param lastNonNull The symbol with the lowest count in the Huffman tree.
     * @param targetNbBits  The allowed number of bits, which the Huffman tree
     *                    may not respect. After this function the Huffman tree will
     *                    respect targetNbBits.
     * @return            The maximum number of bits of the Huffman tree after adjustment.
     */
    private static uint HUF_setMaxHeight(nodeElt_s* huffNode, uint lastNonNull, uint targetNbBits)
    {
        uint largestBits = huffNode[lastNonNull].nbBits;
        if (largestBits <= targetNbBits)
            return largestBits;
        {
            int totalCost = 0;
            uint baseCost = (uint)(1 << (int)(largestBits - targetNbBits));
            int n = (int)lastNonNull;
            while (huffNode[n].nbBits > targetNbBits)
            {
                totalCost += (int)(baseCost - (uint)(1 << (int)(largestBits - huffNode[n].nbBits)));
                huffNode[n].nbBits = (byte)targetNbBits;
                n--;
            }

            assert(huffNode[n].nbBits <= targetNbBits);
            while (huffNode[n].nbBits == targetNbBits)
                --n;
            assert(((uint)totalCost & baseCost - 1) == 0);
            totalCost >>= (int)(largestBits - targetNbBits);
            assert(totalCost > 0);
            {
                const uint noSymbol = 0xF0F0F0F0;
                uint* rankLast = stackalloc uint[14];
                memset(rankLast, 0xF0, sizeof(uint) * 14);
                {
                    uint currentNbBits = targetNbBits;
                    int pos;
                    for (pos = n; pos >= 0; pos--)
                    {
                        if (huffNode[pos].nbBits >= currentNbBits)
                            continue;
                        currentNbBits = huffNode[pos].nbBits;
                        rankLast[targetNbBits - currentNbBits] = (uint)pos;
                    }
                }

                while (totalCost > 0)
                {
                    /* Try to reduce the next power of 2 above totalCost because we
                     * gain back half the rank.
                     */
                    uint nBitsToDecrease = ZSTD_highbit32((uint)totalCost) + 1;
                    for (; nBitsToDecrease > 1; nBitsToDecrease--)
                    {
                        uint highPos = rankLast[nBitsToDecrease];
                        uint lowPos = rankLast[nBitsToDecrease - 1];
                        if (highPos == noSymbol)
                            continue;
                        if (lowPos == noSymbol)
                            break;
                        {
                            uint highTotal = huffNode[highPos].count;
                            uint lowTotal = 2 * huffNode[lowPos].count;
                            if (highTotal <= lowTotal)
                                break;
                        }
                    }

                    assert(rankLast[nBitsToDecrease] != noSymbol || nBitsToDecrease == 1);
                    while (nBitsToDecrease <= 12 && rankLast[nBitsToDecrease] == noSymbol)
                        nBitsToDecrease++;
                    assert(rankLast[nBitsToDecrease] != noSymbol);
                    totalCost -= 1 << (int)(nBitsToDecrease - 1);
                    huffNode[rankLast[nBitsToDecrease]].nbBits++;
                    if (rankLast[nBitsToDecrease - 1] == noSymbol)
                        rankLast[nBitsToDecrease - 1] = rankLast[nBitsToDecrease];
                    if (rankLast[nBitsToDecrease] == 0)
                        rankLast[nBitsToDecrease] = noSymbol;
                    else
                    {
                        rankLast[nBitsToDecrease]--;
                        if (
                            huffNode[rankLast[nBitsToDecrease]].nbBits
                            != targetNbBits - nBitsToDecrease
                        )
                            rankLast[nBitsToDecrease] = noSymbol;
                    }
                }

                while (totalCost < 0)
                {
                    if (rankLast[1] == noSymbol)
                    {
                        while (huffNode[n].nbBits == targetNbBits)
                            n--;
                        huffNode[n + 1].nbBits--;
                        assert(n >= 0);
                        rankLast[1] = (uint)(n + 1);
                        totalCost++;
                        continue;
                    }

                    huffNode[rankLast[1] + 1].nbBits--;
                    rankLast[1]++;
                    totalCost++;
                }
            }
        }

        return targetNbBits;
    }

    /* Return the appropriate bucket index for a given count. See definition of
     * RANK_POSITION_DISTINCT_COUNT_CUTOFF for explanation of bucketing strategy.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HUF_getIndex(uint count)
    {
        return count < 192 - 1 - 32 - 1 + ZSTD_highbit32(192 - 1 - 32 - 1)
            ? count
            : ZSTD_highbit32(count) + (192 - 1 - 32 - 1);
    }

    /* Helper swap function for HUF_quickSortPartition() */
    private static void HUF_swapNodes(nodeElt_s* a, nodeElt_s* b)
    {
        nodeElt_s tmp = *a;
        *a = *b;
        *b = tmp;
    }

    /* Returns 0 if the huffNode array is not sorted by descending count */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HUF_isSorted(nodeElt_s* huffNode, uint maxSymbolValue1)
    {
        uint i;
        for (i = 1; i < maxSymbolValue1; ++i)
        {
            if (huffNode[i].count > huffNode[i - 1].count)
            {
                return 0;
            }
        }

        return 1;
    }

    /* Insertion sort by descending order */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_insertionSort(nodeElt_s* huffNode, int low, int high)
    {
        int i;
        int size = high - low + 1;
        huffNode += low;
        for (i = 1; i < size; ++i)
        {
            nodeElt_s key = huffNode[i];
            int j = i - 1;
            while (j >= 0 && huffNode[j].count < key.count)
            {
                huffNode[j + 1] = huffNode[j];
                j--;
            }

            huffNode[j + 1] = key;
        }
    }

    /* Pivot helper function for quicksort. */
    private static int HUF_quickSortPartition(nodeElt_s* arr, int low, int high)
    {
        /* Simply select rightmost element as pivot. "Better" selectors like
         * median-of-three don't experimentally appear to have any benefit.
         */
        uint pivot = arr[high].count;
        int i = low - 1;
        int j = low;
        for (; j < high; j++)
        {
            if (arr[j].count > pivot)
            {
                i++;
                HUF_swapNodes(&arr[i], &arr[j]);
            }
        }

        HUF_swapNodes(&arr[i + 1], &arr[high]);
        return i + 1;
    }

    /* Classic quicksort by descending with partially iterative calls
     * to reduce worst case callstack size.
     */
    private static void HUF_simpleQuickSort(nodeElt_s* arr, int low, int high)
    {
        const int kInsertionSortThreshold = 8;
        if (high - low < kInsertionSortThreshold)
        {
            HUF_insertionSort(arr, low, high);
            return;
        }

        while (low < high)
        {
            int idx = HUF_quickSortPartition(arr, low, high);
            if (idx - low < high - idx)
            {
                HUF_simpleQuickSort(arr, low, idx - 1);
                low = idx + 1;
            }
            else
            {
                HUF_simpleQuickSort(arr, idx + 1, high);
                high = idx - 1;
            }
        }
    }

    /**
     * HUF_sort():
     * Sorts the symbols [0, maxSymbolValue] by count[symbol] in decreasing order.
     * This is a typical bucket sorting strategy that uses either quicksort or insertion sort to sort each bucket.
     *
     * @param[out] huffNode       Sorted symbols by decreasing count. Only members `.count` and `.byte` are filled.
     *                            Must have (maxSymbolValue + 1) entries.
     * @param[in]  count          Histogram of the symbols.
     * @param[in]  maxSymbolValue Maximum symbol value.
     * @param      rankPosition   This is a scratch workspace. Must have RANK_POSITION_TABLE_SIZE entries.
     */
    private static void HUF_sort(
        nodeElt_s* huffNode,
        uint* count,
        uint maxSymbolValue,
        rankPos* rankPosition
    )
    {
        uint n;
        uint maxSymbolValue1 = maxSymbolValue + 1;
        memset(rankPosition, 0, (uint)(sizeof(rankPos) * 192));
        for (n = 0; n < maxSymbolValue1; ++n)
        {
            uint lowerRank = HUF_getIndex(count[n]);
            assert(lowerRank < 192 - 1);
            rankPosition[lowerRank].@base++;
        }

        assert(rankPosition[192 - 1].@base == 0);
        for (n = 192 - 1; n > 0; --n)
        {
            rankPosition[n - 1].@base += rankPosition[n].@base;
            rankPosition[n - 1].curr = rankPosition[n - 1].@base;
        }

        for (n = 0; n < maxSymbolValue1; ++n)
        {
            uint c = count[n];
            uint r = HUF_getIndex(c) + 1;
            uint pos = rankPosition[r].curr++;
            assert(pos < maxSymbolValue1);
            huffNode[pos].count = c;
            huffNode[pos].@byte = (byte)n;
        }

        for (n = 192 - 1 - 32 - 1 + ZSTD_highbit32(192 - 1 - 32 - 1); n < 192 - 1; ++n)
        {
            int bucketSize = rankPosition[n].curr - rankPosition[n].@base;
            uint bucketStartIdx = rankPosition[n].@base;
            if (bucketSize > 1)
            {
                assert(bucketStartIdx < maxSymbolValue1);
                HUF_simpleQuickSort(huffNode + bucketStartIdx, 0, bucketSize - 1);
            }
        }

        assert(HUF_isSorted(huffNode, maxSymbolValue1) != 0);
    }

    /* HUF_buildTree():
     * Takes the huffNode array sorted by HUF_sort() and builds an unlimited-depth Huffman tree.
     *
     * @param huffNode        The array sorted by HUF_sort(). Builds the Huffman tree in this array.
     * @param maxSymbolValue  The maximum symbol value.
     * @return                The smallest node in the Huffman tree (by count).
     */
    private static int HUF_buildTree(nodeElt_s* huffNode, uint maxSymbolValue)
    {
        nodeElt_s* huffNode0 = huffNode - 1;
        int nonNullRank;
        int lowS,
            lowN;
        int nodeNb = 255 + 1;
        int n,
            nodeRoot;
        nonNullRank = (int)maxSymbolValue;
        while (huffNode[nonNullRank].count == 0)
            nonNullRank--;
        lowS = nonNullRank;
        nodeRoot = nodeNb + lowS - 1;
        lowN = nodeNb;
        huffNode[nodeNb].count = huffNode[lowS].count + huffNode[lowS - 1].count;
        huffNode[lowS].parent = huffNode[lowS - 1].parent = (ushort)nodeNb;
        nodeNb++;
        lowS -= 2;
        for (n = nodeNb; n <= nodeRoot; n++)
            huffNode[n].count = 1U << 30;
        huffNode0[0].count = 1U << 31;
        while (nodeNb <= nodeRoot)
        {
            int n1 = huffNode[lowS].count < huffNode[lowN].count ? lowS-- : lowN++;
            int n2 = huffNode[lowS].count < huffNode[lowN].count ? lowS-- : lowN++;
            huffNode[nodeNb].count = huffNode[n1].count + huffNode[n2].count;
            huffNode[n1].parent = huffNode[n2].parent = (ushort)nodeNb;
            nodeNb++;
        }

        huffNode[nodeRoot].nbBits = 0;
        for (n = nodeRoot - 1; n >= 255 + 1; n--)
            huffNode[n].nbBits = (byte)(huffNode[huffNode[n].parent].nbBits + 1);
        for (n = 0; n <= nonNullRank; n++)
            huffNode[n].nbBits = (byte)(huffNode[huffNode[n].parent].nbBits + 1);
        return nonNullRank;
    }

    /**
     * HUF_buildCTableFromTree():
     * Build the CTable given the Huffman tree in huffNode.
     *
     * @param[out] CTable         The output Huffman CTable.
     * @param      huffNode       The Huffman tree.
     * @param      nonNullRank    The last and smallest node in the Huffman tree.
     * @param      maxSymbolValue The maximum symbol value.
     * @param      maxNbBits      The exact maximum number of bits used in the Huffman tree.
     */
    private static void HUF_buildCTableFromTree(
        nuint* CTable,
        nodeElt_s* huffNode,
        int nonNullRank,
        uint maxSymbolValue,
        uint maxNbBits
    )
    {
        nuint* ct = CTable + 1;
        /* fill result into ctable (val, nbBits) */
        int n;
        ushort* nbPerRank = stackalloc ushort[13];
        memset(nbPerRank, 0, sizeof(ushort) * 13);
        ushort* valPerRank = stackalloc ushort[13];
        memset(valPerRank, 0, sizeof(ushort) * 13);
        int alphabetSize = (int)(maxSymbolValue + 1);
        for (n = 0; n <= nonNullRank; n++)
            nbPerRank[huffNode[n].nbBits]++;
        {
            ushort min = 0;
            for (n = (int)maxNbBits; n > 0; n--)
            {
                valPerRank[n] = min;
                min += nbPerRank[n];
                min >>= 1;
            }
        }

        for (n = 0; n < alphabetSize; n++)
            HUF_setNbBits(ct + huffNode[n].@byte, huffNode[n].nbBits);
        for (n = 0; n < alphabetSize; n++)
            HUF_setValue(ct + n, valPerRank[HUF_getNbBits(ct[n])]++);
        HUF_writeCTableHeader(CTable, maxNbBits, maxSymbolValue);
    }

    private static nuint HUF_buildCTable_wksp(
        nuint* CTable,
        uint* count,
        uint maxSymbolValue,
        uint maxNbBits,
        void* workSpace,
        nuint wkspSize
    )
    {
        HUF_buildCTable_wksp_tables* wksp_tables =
            (HUF_buildCTable_wksp_tables*)HUF_alignUpWorkspace(workSpace, &wkspSize, sizeof(uint));
        nodeElt_s* huffNode0 = &wksp_tables->huffNodeTbl.e0;
        nodeElt_s* huffNode = huffNode0 + 1;
        int nonNullRank;
        if (wkspSize < (nuint)sizeof(HUF_buildCTable_wksp_tables))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall));
        if (maxNbBits == 0)
            maxNbBits = 11;
        if (maxSymbolValue > 255)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge));
        memset(huffNode0, 0, (uint)(sizeof(nodeElt_s) * 512));
        HUF_sort(huffNode, count, maxSymbolValue, &wksp_tables->rankPosition.e0);
        nonNullRank = HUF_buildTree(huffNode, maxSymbolValue);
        maxNbBits = HUF_setMaxHeight(huffNode, (uint)nonNullRank, maxNbBits);
        if (maxNbBits > 12)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        HUF_buildCTableFromTree(CTable, huffNode, nonNullRank, maxSymbolValue, maxNbBits);
        return maxNbBits;
    }

    private static nuint HUF_estimateCompressedSize(nuint* CTable, uint* count, uint maxSymbolValue)
    {
        nuint* ct = CTable + 1;
        nuint nbBits = 0;
        int s;
        for (s = 0; s <= (int)maxSymbolValue; ++s)
        {
            nbBits += HUF_getNbBits(ct[s]) * count[s];
        }

        return nbBits >> 3;
    }

    private static int HUF_validateCTable(nuint* CTable, uint* count, uint maxSymbolValue)
    {
        HUF_CTableHeader header = HUF_readCTableHeader(CTable);
        nuint* ct = CTable + 1;
        int bad = 0;
        int s;
        assert(header.tableLog <= 12);
        if (header.maxSymbolValue < maxSymbolValue)
            return 0;
        for (s = 0; s <= (int)maxSymbolValue; ++s)
        {
            bad |= count[s] != 0 && HUF_getNbBits(ct[s]) == 0 ? 1 : 0;
        }

        return bad == 0 ? 1 : 0;
    }

    private static nuint HUF_compressBound(nuint size)
    {
        return 129 + (size + (size >> 8) + 8);
    }

    /**! HUF_initCStream():
     * Initializes the bitstream.
     * @returns 0 or an error code.
     */
    private static nuint HUF_initCStream(ref HUF_CStream_t bitC, void* startPtr, nuint dstCapacity)
    {
        bitC = new HUF_CStream_t
        {
            startPtr = (byte*)startPtr,
            ptr = (byte*)startPtr,
            endPtr = (byte*)startPtr + dstCapacity - sizeof(nuint),
        };
        if (dstCapacity <= (nuint)sizeof(nuint))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        return 0;
    }

    /*! HUF_addBits():
     * Adds the symbol stored in HUF_CElt elt to the bitstream.
     *
     * @param elt   The element we're adding. This is a (nbBits, value) pair.
     *              See the HUF_CStream_t docs for the format.
     * @param idx   Insert into the bitstream at this idx.
     * @param kFast This is a template parameter. If the bitstream is guaranteed
     *              to have at least 4 unused bits after this call it may be 1,
     *              otherwise it must be 0. HUF_addBits() is faster when fast is set.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_addBits(
        ref nuint bitC_bitContainer_e0,
        ref nuint bitC_bitPos_e0,
        nuint elt,
        int kFast
    )
    {
        assert(HUF_getNbBits(elt) <= 12);
        bitC_bitContainer_e0 >>= (int)HUF_getNbBits(elt);
        bitC_bitContainer_e0 |= kFast != 0 ? HUF_getValueFast(elt) : HUF_getValue(elt);
        bitC_bitPos_e0 += HUF_getNbBitsFast(elt);
        assert((bitC_bitPos_e0 & 0xFF) <= (nuint)(sizeof(nuint) * 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_zeroIndex1(ref nuint bitC_bitContainer_e1, ref nuint bitC_bitPos_e1)
    {
        bitC_bitContainer_e1 = 0;
        bitC_bitPos_e1 = 0;
    }

    /*! HUF_mergeIndex1() :
     * Merges the bit container @ index 1 into the bit container @ index 0
     * and zeros the bit container @ index 1.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_mergeIndex1(
        ref nuint bitC_bitContainer_e0,
        ref nuint bitC_bitPos_e0,
        ref nuint bitC_bitContainer_e1,
        ref nuint bitC_bitPos_e1
    )
    {
        assert((bitC_bitPos_e1 & 0xFF) < (nuint)(sizeof(nuint) * 8));
        bitC_bitContainer_e0 >>= (int)(bitC_bitPos_e1 & 0xFF);
        bitC_bitContainer_e0 |= bitC_bitContainer_e1;
        bitC_bitPos_e0 += bitC_bitPos_e1;
        assert((bitC_bitPos_e0 & 0xFF) <= (nuint)(sizeof(nuint) * 8));
    }

    /*! HUF_flushBits() :
     * Flushes the bits in the bit container @ index 0.
     *
     * @post bitPos will be < 8.
     * @param kFast If kFast is set then we must know a-priori that
     *              the bit container will not overflow.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_flushBits(
        ref nuint bitC_bitContainer_e0,
        ref nuint bitC_bitPos_e0,
        ref byte* bitC_ptr,
        byte* bitC_endPtr,
        int kFast
    )
    {
        /* The upper bits of bitPos are noisy, so we must mask by 0xFF. */
        nuint nbBits = bitC_bitPos_e0 & 0xFF;
        nuint nbBytes = nbBits >> 3;
        /* The top nbBits bits of bitContainer are the ones we need. */
        nuint bitContainer = bitC_bitContainer_e0 >> (int)((nuint)(sizeof(nuint) * 8) - nbBits);
        bitC_bitPos_e0 &= 7;
        assert(nbBits > 0);
        assert(nbBits <= (nuint)(sizeof(nuint) * 8));
        assert(bitC_ptr <= bitC_endPtr);
        MEM_writeLEST(bitC_ptr, bitContainer);
        bitC_ptr += nbBytes;
        assert(kFast == 0 || bitC_ptr <= bitC_endPtr);
        if (kFast == 0 && bitC_ptr > bitC_endPtr)
            bitC_ptr = bitC_endPtr;
    }

    /*! HUF_endMark()
     * @returns The Huffman stream end mark: A 1-bit value = 1.
     */
    private static nuint HUF_endMark()
    {
        nuint endMark;
        HUF_setNbBits(&endMark, 1);
        HUF_setValue(&endMark, 1);
        return endMark;
    }

    /*! HUF_closeCStream() :
     *  @return Size of CStream, in bytes,
     *          or 0 if it could not fit into dstBuffer */
    private static nuint HUF_closeCStream(ref HUF_CStream_t bitC)
    {
        HUF_addBits(ref bitC.bitContainer.e0, ref bitC.bitPos.e0, HUF_endMark(), 0);
        HUF_flushBits(ref bitC.bitContainer.e0, ref bitC.bitPos.e0, ref bitC.ptr, bitC.endPtr, 0);
        {
            nuint nbBits = bitC.bitPos.e0 & 0xFF;
            if (bitC.ptr >= bitC.endPtr)
                return 0;
            return (nuint)(bitC.ptr - bitC.startPtr) + (nuint)(nbBits > 0 ? 1 : 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_encodeSymbol(
        ref nuint bitCPtr_bitContainer_e0,
        ref nuint bitCPtr_bitPos_e0,
        uint symbol,
        nuint* CTable,
        int fast
    )
    {
        HUF_addBits(ref bitCPtr_bitContainer_e0, ref bitCPtr_bitPos_e0, CTable[symbol], fast);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HUF_compress1X_usingCTable_internal_body_loop(
        ref HUF_CStream_t bitC,
        byte* ip,
        nuint srcSize,
        nuint* ct,
        int kUnroll,
        int kFastFlush,
        int kLastFast
    )
    {
        byte* bitC_ptr = bitC.ptr;
        byte* bitC_endPtr = bitC.endPtr;
        nuint bitC_bitContainer_e0 = bitC.bitContainer.e0;
        nuint bitC_bitPos_e0 = bitC.bitPos.e0;
        nuint bitC_bitContainer_e1 = bitC.bitContainer.e1;
        nuint bitC_bitPos_e1 = bitC.bitPos.e1;
        /* Join to kUnroll */
        int n = (int)srcSize;
        int rem = n % kUnroll;
        if (rem > 0)
        {
            for (; rem > 0; --rem)
            {
                HUF_encodeSymbol(ref bitC_bitContainer_e0, ref bitC_bitPos_e0, ip[--n], ct, 0);
            }

            HUF_flushBits(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ref bitC_ptr,
                bitC_endPtr,
                kFastFlush
            );
        }

        assert(n % kUnroll == 0);
        if (n % (2 * kUnroll) != 0)
        {
            int u;
            for (u = 1; u < kUnroll; ++u)
            {
                HUF_encodeSymbol(ref bitC_bitContainer_e0, ref bitC_bitPos_e0, ip[n - u], ct, 1);
            }

            HUF_encodeSymbol(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ip[n - kUnroll],
                ct,
                kLastFast
            );
            HUF_flushBits(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ref bitC_ptr,
                bitC_endPtr,
                kFastFlush
            );
            n -= kUnroll;
        }

        assert(n % (2 * kUnroll) == 0);
        for (; n > 0; n -= 2 * kUnroll)
        {
            /* Encode kUnroll symbols into the bitstream @ index 0. */
            int u;
            for (u = 1; u < kUnroll; ++u)
            {
                HUF_encodeSymbol(ref bitC_bitContainer_e0, ref bitC_bitPos_e0, ip[n - u], ct, 1);
            }

            HUF_encodeSymbol(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ip[n - kUnroll],
                ct,
                kLastFast
            );
            HUF_flushBits(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ref bitC_ptr,
                bitC_endPtr,
                kFastFlush
            );
            HUF_zeroIndex1(ref bitC_bitContainer_e1, ref bitC_bitPos_e1);
            for (u = 1; u < kUnroll; ++u)
            {
                HUF_encodeSymbol(
                    ref bitC_bitContainer_e1,
                    ref bitC_bitPos_e1,
                    ip[n - kUnroll - u],
                    ct,
                    1
                );
            }

            HUF_encodeSymbol(
                ref bitC_bitContainer_e1,
                ref bitC_bitPos_e1,
                ip[n - kUnroll - kUnroll],
                ct,
                kLastFast
            );
            HUF_mergeIndex1(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ref bitC_bitContainer_e1,
                ref bitC_bitPos_e1
            );
            HUF_flushBits(
                ref bitC_bitContainer_e0,
                ref bitC_bitPos_e0,
                ref bitC_ptr,
                bitC_endPtr,
                kFastFlush
            );
        }

        assert(n == 0);
        bitC.ptr = bitC_ptr;
        bitC.endPtr = bitC_endPtr;
        bitC.bitContainer.e0 = bitC_bitContainer_e0;
        bitC.bitPos.e0 = bitC_bitPos_e0;
        bitC.bitContainer.e1 = bitC_bitContainer_e1;
        bitC.bitPos.e1 = bitC_bitPos_e1;
    }

    /**
     * Returns a tight upper bound on the output space needed by Huffman
     * with 8 bytes buffer to handle over-writes. If the output is at least
     * this large we don't need to do bounds checks during Huffman encoding.
     */
    private static nuint HUF_tightCompressBound(nuint srcSize, nuint tableLog)
    {
        return (srcSize * tableLog >> 3) + 8;
    }

    private static nuint HUF_compress1X_usingCTable_internal_body(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        nuint* CTable
    )
    {
        uint tableLog = HUF_readCTableHeader(CTable).tableLog;
        nuint* ct = CTable + 1;
        byte* ip = (byte*)src;
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstSize;
        HUF_CStream_t bitC;
        System.Runtime.CompilerServices.Unsafe.SkipInit(out bitC);
        if (dstSize < 8)
            return 0;
        {
            byte* op = ostart;
            nuint initErr = HUF_initCStream(ref bitC, op, (nuint)(oend - op));
            if (ERR_isError(initErr))
                return 0;
        }

        if (dstSize < HUF_tightCompressBound(srcSize, tableLog) || tableLog > 11)
            HUF_compress1X_usingCTable_internal_body_loop(
                ref bitC,
                ip,
                srcSize,
                ct,
                MEM_32bits ? 2 : 4,
                0,
                0
            );
        else
        {
            if (MEM_32bits)
            {
                switch (tableLog)
                {
                    case 11:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            2,
                            1,
                            0
                        );
                        break;
                    case 10:
                    case 9:
                    case 8:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            2,
                            1,
                            1
                        );
                        break;
                    case 7:
                    default:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            3,
                            1,
                            1
                        );
                        break;
                }
            }
            else
            {
                switch (tableLog)
                {
                    case 11:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            5,
                            1,
                            0
                        );
                        break;
                    case 10:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            5,
                            1,
                            1
                        );
                        break;
                    case 9:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            6,
                            1,
                            0
                        );
                        break;
                    case 8:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            7,
                            1,
                            0
                        );
                        break;
                    case 7:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            8,
                            1,
                            0
                        );
                        break;
                    case 6:
                    default:
                        HUF_compress1X_usingCTable_internal_body_loop(
                            ref bitC,
                            ip,
                            srcSize,
                            ct,
                            9,
                            1,
                            1
                        );
                        break;
                }
            }
        }

        assert(bitC.ptr <= bitC.endPtr);
        return HUF_closeCStream(ref bitC);
    }

    private static nuint HUF_compress1X_usingCTable_internal(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        nuint* CTable,
        int flags
    )
    {
        return HUF_compress1X_usingCTable_internal_body(dst, dstSize, src, srcSize, CTable);
    }

    /* ====================== */
    /* single stream variants */
    /* ====================== */
    private static nuint HUF_compress1X_usingCTable(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        nuint* CTable,
        int flags
    )
    {
        return HUF_compress1X_usingCTable_internal(dst, dstSize, src, srcSize, CTable, flags);
    }

    private static nuint HUF_compress4X_usingCTable_internal(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        nuint* CTable,
        int flags
    )
    {
        /* first 3 segments */
        nuint segmentSize = (srcSize + 3) / 4;
        byte* ip = (byte*)src;
        byte* iend = ip + srcSize;
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstSize;
        byte* op = ostart;
        if (dstSize < 6 + 1 + 1 + 1 + 8)
            return 0;
        if (srcSize < 12)
            return 0;
        op += 6;
        assert(op <= oend);
        {
            nuint cSize = HUF_compress1X_usingCTable_internal(
                op,
                (nuint)(oend - op),
                ip,
                segmentSize,
                CTable,
                flags
            );
            if (ERR_isError(cSize))
                return cSize;
            if (cSize == 0 || cSize > 65535)
                return 0;
            MEM_writeLE16(ostart, (ushort)cSize);
            op += cSize;
        }

        ip += segmentSize;
        assert(op <= oend);
        {
            nuint cSize = HUF_compress1X_usingCTable_internal(
                op,
                (nuint)(oend - op),
                ip,
                segmentSize,
                CTable,
                flags
            );
            if (ERR_isError(cSize))
                return cSize;
            if (cSize == 0 || cSize > 65535)
                return 0;
            MEM_writeLE16(ostart + 2, (ushort)cSize);
            op += cSize;
        }

        ip += segmentSize;
        assert(op <= oend);
        {
            nuint cSize = HUF_compress1X_usingCTable_internal(
                op,
                (nuint)(oend - op),
                ip,
                segmentSize,
                CTable,
                flags
            );
            if (ERR_isError(cSize))
                return cSize;
            if (cSize == 0 || cSize > 65535)
                return 0;
            MEM_writeLE16(ostart + 4, (ushort)cSize);
            op += cSize;
        }

        ip += segmentSize;
        assert(op <= oend);
        assert(ip <= iend);
        {
            nuint cSize = HUF_compress1X_usingCTable_internal(
                op,
                (nuint)(oend - op),
                ip,
                (nuint)(iend - ip),
                CTable,
                flags
            );
            if (ERR_isError(cSize))
                return cSize;
            if (cSize == 0 || cSize > 65535)
                return 0;
            op += cSize;
        }

        return (nuint)(op - ostart);
    }

    private static nuint HUF_compress4X_usingCTable(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        nuint* CTable,
        int flags
    )
    {
        return HUF_compress4X_usingCTable_internal(dst, dstSize, src, srcSize, CTable, flags);
    }

    private static nuint HUF_compressCTable_internal(
        byte* ostart,
        byte* op,
        byte* oend,
        void* src,
        nuint srcSize,
        HUF_nbStreams_e nbStreams,
        nuint* CTable,
        int flags
    )
    {
        nuint cSize =
            nbStreams == HUF_nbStreams_e.HUF_singleStream
                ? HUF_compress1X_usingCTable_internal(
                    op,
                    (nuint)(oend - op),
                    src,
                    srcSize,
                    CTable,
                    flags
                )
                : HUF_compress4X_usingCTable_internal(
                    op,
                    (nuint)(oend - op),
                    src,
                    srcSize,
                    CTable,
                    flags
                );
        if (ERR_isError(cSize))
        {
            return cSize;
        }

        if (cSize == 0)
        {
            return 0;
        }

        op += cSize;
        assert(op >= ostart);
        if ((nuint)(op - ostart) >= srcSize - 1)
        {
            return 0;
        }

        return (nuint)(op - ostart);
    }

    private static uint HUF_cardinality(uint* count, uint maxSymbolValue)
    {
        uint cardinality = 0;
        uint i;
        for (i = 0; i < maxSymbolValue + 1; i++)
        {
            if (count[i] != 0)
                cardinality += 1;
        }

        return cardinality;
    }

    /*! HUF_compress() does the following:
     *  1. count symbol occurrence from source[] into table count[] using FSE_count() (exposed within "fse.h")
     *  2. (optional) refine tableLog using HUF_optimalTableLog()
     *  3. build Huffman table from count using HUF_buildCTable()
     *  4. save Huffman table to memory buffer using HUF_writeCTable()
     *  5. encode the data stream using HUF_compress4X_usingCTable()
     *
     *  The following API allows targeting specific sub-functions for advanced tasks.
     *  For example, it's possible to compress several blocks using the same 'CTable',
     *  or to save and regenerate 'CTable' using external methods.
     */
    private static uint HUF_minTableLog(uint symbolCardinality)
    {
        uint minBitsSymbols = ZSTD_highbit32(symbolCardinality) + 1;
        return minBitsSymbols;
    }

    private static uint HUF_optimalTableLog(
        uint maxTableLog,
        nuint srcSize,
        uint maxSymbolValue,
        void* workSpace,
        nuint wkspSize,
        nuint* table,
        uint* count,
        int flags
    )
    {
        assert(srcSize > 1);
        assert(wkspSize >= (nuint)sizeof(HUF_buildCTable_wksp_tables));
        if ((flags & (int)HUF_flags_e.HUF_flags_optimalDepth) == 0)
        {
            return FSE_optimalTableLog_internal(maxTableLog, srcSize, maxSymbolValue, 1);
        }

        {
            byte* dst = (byte*)workSpace + sizeof(HUF_WriteCTableWksp);
            nuint dstSize = wkspSize - (nuint)sizeof(HUF_WriteCTableWksp);
            nuint hSize,
                newSize;
            uint symbolCardinality = HUF_cardinality(count, maxSymbolValue);
            uint minTableLog = HUF_minTableLog(symbolCardinality);
            nuint optSize = unchecked((nuint)~0) - 1;
            uint optLog = maxTableLog,
                optLogGuess;
            for (optLogGuess = minTableLog; optLogGuess <= maxTableLog; optLogGuess++)
            {
                {
                    nuint maxBits = HUF_buildCTable_wksp(
                        table,
                        count,
                        maxSymbolValue,
                        optLogGuess,
                        workSpace,
                        wkspSize
                    );
                    if (ERR_isError(maxBits))
                        continue;
                    if (maxBits < optLogGuess && optLogGuess > minTableLog)
                        break;
                    hSize = HUF_writeCTable_wksp(
                        dst,
                        dstSize,
                        table,
                        maxSymbolValue,
                        (uint)maxBits,
                        workSpace,
                        wkspSize
                    );
                }

                if (ERR_isError(hSize))
                    continue;
                newSize = HUF_estimateCompressedSize(table, count, maxSymbolValue) + hSize;
                if (newSize > optSize + 1)
                {
                    break;
                }

                if (newSize < optSize)
                {
                    optSize = newSize;
                    optLog = optLogGuess;
                }
            }

            assert(optLog <= 12);
            return optLog;
        }
    }

    /* HUF_compress_internal() :
     * `workSpace_align4` must be aligned on 4-bytes boundaries,
     * and occupies the same space as a table of HUF_WORKSPACE_SIZE_U64 unsigned */
    private static nuint HUF_compress_internal(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        uint maxSymbolValue,
        uint huffLog,
        HUF_nbStreams_e nbStreams,
        void* workSpace,
        nuint wkspSize,
        nuint* oldHufTable,
        HUF_repeat* repeat,
        int flags
    )
    {
        HUF_compress_tables_t* table = (HUF_compress_tables_t*)HUF_alignUpWorkspace(
            workSpace,
            &wkspSize,
            sizeof(ulong)
        );
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstSize;
        byte* op = ostart;
        if (wkspSize < (nuint)sizeof(HUF_compress_tables_t))
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall));
        if (srcSize == 0)
            return 0;
        if (dstSize == 0)
            return 0;
        if (srcSize > 128 * 1024)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong));
        if (huffLog > 12)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge));
        if (maxSymbolValue > 255)
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge));
        if (maxSymbolValue == 0)
            maxSymbolValue = 255;
        if (huffLog == 0)
            huffLog = 11;
        if (
            (flags & (int)HUF_flags_e.HUF_flags_preferRepeat) != 0
            && repeat != null
            && *repeat == HUF_repeat.HUF_repeat_valid
        )
        {
            return HUF_compressCTable_internal(
                ostart,
                op,
                oend,
                src,
                srcSize,
                nbStreams,
                oldHufTable,
                flags
            );
        }

        if ((flags & (int)HUF_flags_e.HUF_flags_suspectUncompressible) != 0 && srcSize >= 4096 * 10)
        {
            nuint largestTotal = 0;
            {
                uint maxSymbolValueBegin = maxSymbolValue;
                nuint largestBegin = HIST_count_simple(
                    table->count,
                    &maxSymbolValueBegin,
                    (byte*)src,
                    4096
                );
                if (ERR_isError(largestBegin))
                    return largestBegin;
                largestTotal += largestBegin;
            }

            {
                uint maxSymbolValueEnd = maxSymbolValue;
                nuint largestEnd = HIST_count_simple(
                    table->count,
                    &maxSymbolValueEnd,
                    (byte*)src + srcSize - 4096,
                    4096
                );
                if (ERR_isError(largestEnd))
                    return largestEnd;
                largestTotal += largestEnd;
            }

            if (largestTotal <= (2 * 4096 >> 7) + 4)
                return 0;
        }

        {
            nuint largest = HIST_count_wksp(
                table->count,
                &maxSymbolValue,
                (byte*)src,
                srcSize,
                table->wksps.hist_wksp,
                sizeof(uint) * 1024
            );
            if (ERR_isError(largest))
                return largest;
            if (largest == srcSize)
            {
                *ostart = ((byte*)src)[0];
                return 1;
            }

            if (largest <= (srcSize >> 7) + 4)
                return 0;
        }

        if (
            repeat != null
            && *repeat == HUF_repeat.HUF_repeat_check
            && HUF_validateCTable(oldHufTable, table->count, maxSymbolValue) == 0
        )
        {
            *repeat = HUF_repeat.HUF_repeat_none;
        }

        if (
            (flags & (int)HUF_flags_e.HUF_flags_preferRepeat) != 0
            && repeat != null
            && *repeat != HUF_repeat.HUF_repeat_none
        )
        {
            return HUF_compressCTable_internal(
                ostart,
                op,
                oend,
                src,
                srcSize,
                nbStreams,
                oldHufTable,
                flags
            );
        }

        huffLog = HUF_optimalTableLog(
            huffLog,
            srcSize,
            maxSymbolValue,
            &table->wksps,
            (nuint)sizeof(_wksps_e__Union),
            &table->CTable.e0,
            table->count,
            flags
        );
        {
            nuint maxBits = HUF_buildCTable_wksp(
                &table->CTable.e0,
                table->count,
                maxSymbolValue,
                huffLog,
                &table->wksps.buildCTable_wksp,
                (nuint)sizeof(HUF_buildCTable_wksp_tables)
            );
            {
                nuint _var_err__ = maxBits;
                if (ERR_isError(_var_err__))
                    return _var_err__;
            }

            huffLog = (uint)maxBits;
        }

        {
            nuint hSize = HUF_writeCTable_wksp(
                op,
                dstSize,
                &table->CTable.e0,
                maxSymbolValue,
                huffLog,
                &table->wksps.writeCTable_wksp,
                (nuint)sizeof(HUF_WriteCTableWksp)
            );
            if (ERR_isError(hSize))
                return hSize;
            if (repeat != null && *repeat != HUF_repeat.HUF_repeat_none)
            {
                nuint oldSize = HUF_estimateCompressedSize(
                    oldHufTable,
                    table->count,
                    maxSymbolValue
                );
                nuint newSize = HUF_estimateCompressedSize(
                    &table->CTable.e0,
                    table->count,
                    maxSymbolValue
                );
                if (oldSize <= hSize + newSize || hSize + 12 >= srcSize)
                {
                    return HUF_compressCTable_internal(
                        ostart,
                        op,
                        oend,
                        src,
                        srcSize,
                        nbStreams,
                        oldHufTable,
                        flags
                    );
                }
            }

            if (hSize + 12U >= srcSize)
            {
                return 0;
            }

            op += hSize;
            if (repeat != null)
            {
                *repeat = HUF_repeat.HUF_repeat_none;
            }

            if (oldHufTable != null)
                memcpy(oldHufTable, &table->CTable.e0, sizeof(ulong) * 257);
        }

        return HUF_compressCTable_internal(
            ostart,
            op,
            oend,
            src,
            srcSize,
            nbStreams,
            &table->CTable.e0,
            flags
        );
    }

    /** HUF_compress1X_repeat() :
     *  Same as HUF_compress1X_wksp(), but considers using hufTable if *repeat != HUF_repeat_none.
     *  If it uses hufTable it does not modify hufTable or repeat.
     *  If it doesn't, it sets *repeat = HUF_repeat_none, and it sets hufTable to the table used.
     *  If preferRepeat then the old table will always be used if valid.
     *  If suspectUncompressible then some sampling checks will be run to potentially skip huffman coding */
    private static nuint HUF_compress1X_repeat(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        uint maxSymbolValue,
        uint huffLog,
        void* workSpace,
        nuint wkspSize,
        nuint* hufTable,
        HUF_repeat* repeat,
        int flags
    )
    {
        return HUF_compress_internal(
            dst,
            dstSize,
            src,
            srcSize,
            maxSymbolValue,
            huffLog,
            HUF_nbStreams_e.HUF_singleStream,
            workSpace,
            wkspSize,
            hufTable,
            repeat,
            flags
        );
    }

    /* HUF_compress4X_repeat():
     * compress input using 4 streams.
     * consider skipping quickly
     * reuse an existing huffman compression table */
    private static nuint HUF_compress4X_repeat(
        void* dst,
        nuint dstSize,
        void* src,
        nuint srcSize,
        uint maxSymbolValue,
        uint huffLog,
        void* workSpace,
        nuint wkspSize,
        nuint* hufTable,
        HUF_repeat* repeat,
        int flags
    )
    {
        return HUF_compress_internal(
            dst,
            dstSize,
            src,
            srcSize,
            maxSymbolValue,
            huffLog,
            HUF_nbStreams_e.HUF_fourStreams,
            workSpace,
            wkspSize,
            hufTable,
            repeat,
            flags
        );
    }
}
