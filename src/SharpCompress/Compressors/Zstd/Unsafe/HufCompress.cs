using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /* **************************************************************
        *  Utils
        ****************************************************************/
        public static uint HUF_optimalTableLog(uint maxTableLog, nuint srcSize, uint maxSymbolValue)
        {
            return FSE_optimalTableLog_internal(maxTableLog, srcSize, maxSymbolValue, 1);
        }

        private static nuint HUF_compressWeights(void* dst, nuint dstSize, void* weightTable, nuint wtSize, void* workspace, nuint workspaceSize)
        {
            byte* ostart = (byte*)(dst);
            byte* op = ostart;
            byte* oend = ostart + dstSize;
            uint maxSymbolValue = 12;
            uint tableLog = 6;
            HUF_CompressWeightsWksp* wksp = (HUF_CompressWeightsWksp*)(workspace);

            if (workspaceSize < (nuint)(sizeof(HUF_CompressWeightsWksp)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (wtSize <= 1)
            {
                return 0;
            }


            {
                uint maxCount = HIST_count_simple((uint*)wksp->count, &maxSymbolValue, weightTable, wtSize);

                if (maxCount == wtSize)
                {
                    return 1;
                }

                if (maxCount == 1)
                {
                    return 0;
                }
            }

            tableLog = FSE_optimalTableLog(tableLog, wtSize, maxSymbolValue);

            {
                nuint _var_err__ = FSE_normalizeCount((short*)wksp->norm, tableLog, (uint*)wksp->count, wtSize, maxSymbolValue, 0);

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }


            {
                nuint hSize = FSE_writeNCount((void*)op, (nuint)(oend - op), (short*)wksp->norm, maxSymbolValue, tableLog);

                if ((ERR_isError(hSize)) != 0)
                {
                    return hSize;
                }

                op += hSize;
            }


            {
                nuint _var_err__ = FSE_buildCTable_wksp((uint*)wksp->CTable, (short*)wksp->norm, maxSymbolValue, tableLog, (void*)wksp->scratchBuffer, (nuint)(120));

                if ((ERR_isError(_var_err__)) != 0)
                {
                    return _var_err__;
                }
            }


            {
                nuint cSize = FSE_compress_usingCTable((void*)op, (nuint)(oend - op), weightTable, wtSize, (uint*)wksp->CTable);

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

            return (nuint)(op - ostart);
        }

        public static nuint HUF_writeCTable_wksp(void* dst, nuint maxDstSize, HUF_CElt_s* CTable, uint maxSymbolValue, uint huffLog, void* workspace, nuint workspaceSize)
        {
            byte* op = (byte*)(dst);
            uint n;
            HUF_WriteCTableWksp* wksp = (HUF_WriteCTableWksp*)(workspace);

            if (workspaceSize < (nuint)(sizeof(HUF_WriteCTableWksp)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (maxSymbolValue > 255)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge)));
            }

            wksp->bitsToWeight[0] = 0;
            for (n = 1; n < huffLog + 1; n++)
            {
                wksp->bitsToWeight[n] = (byte)(huffLog + 1 - n);
            }

            for (n = 0; n < maxSymbolValue; n++)
            {
                wksp->huffWeight[n] = wksp->bitsToWeight[CTable[n].nbBits];
            }


            {
                nuint hSize = HUF_compressWeights((void*)(op + 1), maxDstSize - 1, (void*)wksp->huffWeight, maxSymbolValue, (void*)&wksp->wksp, (nuint)(436));

                if ((ERR_isError(hSize)) != 0)
                {
                    return hSize;
                }

                if (((hSize > 1) && (hSize < maxSymbolValue / 2)))
                {
                    op[0] = (byte)(hSize);
                    return hSize + 1;
                }
            }

            if (maxSymbolValue > (uint)((256 - 128)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (((maxSymbolValue + 1) / 2) + 1 > maxDstSize)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            op[0] = (byte)(128 + (maxSymbolValue - 1));
            wksp->huffWeight[maxSymbolValue] = 0;
            for (n = 0; n < maxSymbolValue; n += 2)
            {
                op[(n / 2) + 1] = (byte)((wksp->huffWeight[n] << 4) + wksp->huffWeight[n + 1]);
            }

            return ((maxSymbolValue + 1) / 2) + 1;
        }

        /*! HUF_writeCTable() :
            `CTable` : Huffman tree to save, using huf representation.
            @return : size of saved CTable */
        public static nuint HUF_writeCTable(void* dst, nuint maxDstSize, HUF_CElt_s* CTable, uint maxSymbolValue, uint huffLog)
        {
            HUF_WriteCTableWksp wksp;

            return HUF_writeCTable_wksp(dst, maxDstSize, CTable, maxSymbolValue, huffLog, (void*)&wksp, (nuint)(sizeof(HUF_WriteCTableWksp)));
        }

        /** HUF_readCTable() :
         *  Loading a CTable saved with HUF_writeCTable() */
        public static nuint HUF_readCTable(HUF_CElt_s* CTable, uint* maxSymbolValuePtr, void* src, nuint srcSize, uint* hasZeroWeights)
        {
            byte* huffWeight = stackalloc byte[256];
            uint* rankVal = stackalloc uint[16];
            uint tableLog = 0;
            uint nbSymbols = 0;
            nuint readSize = HUF_readStats((byte*)huffWeight, (nuint)(255 + 1), (uint*)rankVal, &nbSymbols, &tableLog, src, srcSize);

            if ((ERR_isError(readSize)) != 0)
            {
                return readSize;
            }

            *hasZeroWeights = (((rankVal[0] > 0)) ? 1U : 0U);
            if (tableLog > 12)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            if (nbSymbols > *maxSymbolValuePtr + 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooSmall)));
            }


            {
                uint n, nextRankStart = 0;

                for (n = 1; n <= tableLog; n++)
                {
                    uint curr = nextRankStart;

                    nextRankStart += (rankVal[n] << (int)(n - 1));
                    rankVal[n] = curr;
                }
            }


            {
                uint n;

                for (n = 0; n < nbSymbols; n++)
                {
                    uint w = huffWeight[n];

                    CTable[n].nbBits = (byte)(unchecked((byte)(tableLog + 1 - w) & -((w != 0) ? 1 : 0)));
                }
            }


            {
                ushort* nbPerRank = stackalloc ushort[14];
                memset(nbPerRank, 0, sizeof(ushort) * 14);
                ushort* valPerRank = stackalloc ushort[14];
                memset(valPerRank, 0, sizeof(ushort) * 14);


                {
                    uint n;

                    for (n = 0; n < nbSymbols; n++)
                    {
                        nbPerRank[CTable[n].nbBits]++;
                    }
                }

                valPerRank[tableLog + 1] = 0;

                {
                    ushort min = 0;
                    uint n;

                    for (n = tableLog; n > 0; n--)
                    {
                        valPerRank[n] = min;
                        min += (ushort)(nbPerRank[n]);
                        min >>= 1;
                    }
                }


                {
                    uint n;

                    for (n = 0; n < nbSymbols; n++)
                    {
                        CTable[n].val = valPerRank[CTable[n].nbBits]++;
                    }
                }
            }

            *maxSymbolValuePtr = nbSymbols - 1;
            return readSize;
        }

        /** HUF_getNbBits() :
         *  Read nbBits from CTable symbolTable, for symbol `symbolValue` presumed <= HUF_SYMBOLVALUE_MAX
         *  Note 1 : is not inlined, as HUF_CElt definition is private
         *  Note 2 : const void* used, so that it can provide a statically allocated table as argument (which uses type U32) */
        public static uint HUF_getNbBits(void* symbolTable, uint symbolValue)
        {
            HUF_CElt_s* table = (HUF_CElt_s*)(symbolTable);

            assert(symbolValue <= 255);
            return table[symbolValue].nbBits;
        }

        /**
         * HUF_setMaxHeight():
         * Enforces maxNbBits on the Huffman tree described in huffNode.
         *
         * It sets all nodes with nbBits > maxNbBits to be maxNbBits. Then it adjusts
         * the tree to so that it is a valid canonical Huffman tree.
         *
         * @pre               The sum of the ranks of each symbol == 2^largestBits,
         *                    where largestBits == huffNode[lastNonNull].nbBits.
         * @post              The sum of the ranks of each symbol == 2^largestBits,
         *                    where largestBits is the return value <= maxNbBits.
         *
         * @param huffNode    The Huffman tree modified in place to enforce maxNbBits.
         * @param lastNonNull The symbol with the lowest count in the Huffman tree.
         * @param maxNbBits   The maximum allowed number of bits, which the Huffman tree
         *                    may not respect. After this function the Huffman tree will
         *                    respect maxNbBits.
         * @return            The maximum number of bits of the Huffman tree after adjustment,
         *                    necessarily no more than maxNbBits.
         */
        private static uint HUF_setMaxHeight(nodeElt_s* huffNode, uint lastNonNull, uint maxNbBits)
        {
            uint largestBits = huffNode[lastNonNull].nbBits;

            if (largestBits <= maxNbBits)
            {
                return largestBits;
            }


            {
                int totalCost = 0;
                uint baseCost = (uint)(1 << (int)(largestBits - maxNbBits));
                int n = (int)(lastNonNull);

                while (huffNode[n].nbBits > maxNbBits)
                {
                    totalCost += (int)((int)(baseCost - (uint)((1 << (int)(largestBits - huffNode[n].nbBits)))));
                    huffNode[n].nbBits = (byte)(maxNbBits);
                    n--;
                }

                assert(huffNode[n].nbBits <= maxNbBits);
                while (huffNode[n].nbBits == maxNbBits)
                {
                    --n;
                }

                assert(((uint)totalCost & (baseCost - 1)) == 0);
                totalCost >>= (int)(largestBits - maxNbBits);
                assert(totalCost > 0);

                {
                    uint noSymbol = 0xF0F0F0F0;
                    uint* rankLast = stackalloc uint[14];

                    memset((void*)(rankLast), (0xF0), ((nuint)(sizeof(uint) * 14)));

                    {
                        uint currentNbBits = maxNbBits;
                        int pos;

                        for (pos = n; pos >= 0; pos--)
                        {
                            if (huffNode[pos].nbBits >= currentNbBits)
                            {
                                continue;
                            }

                            currentNbBits = huffNode[pos].nbBits;
                            rankLast[maxNbBits - currentNbBits] = (uint)(pos);
                        }
                    }

                    while (totalCost > 0)
                    {
                        uint nBitsToDecrease = BIT_highbit32((uint)(totalCost)) + 1;

                        for (; nBitsToDecrease > 1; nBitsToDecrease--)
                        {
                            uint highPos = rankLast[nBitsToDecrease];
                            uint lowPos = rankLast[nBitsToDecrease - 1];

                            if (highPos == noSymbol)
                            {
                                continue;
                            }

                            if (lowPos == noSymbol)
                            {
                                break;
                            }


                            {
                                uint highTotal = huffNode[highPos].count;
                                uint lowTotal = 2 * huffNode[lowPos].count;

                                if (highTotal <= lowTotal)
                                {
                                    break;
                                }
                            }
                        }

                        assert(rankLast[nBitsToDecrease] != noSymbol || nBitsToDecrease == 1);
                        while ((nBitsToDecrease <= 12) && (rankLast[nBitsToDecrease] == noSymbol))
                        {
                            nBitsToDecrease++;
                        }

                        assert(rankLast[nBitsToDecrease] != noSymbol);
                        totalCost -= 1 << (int)(nBitsToDecrease - 1);
                        huffNode[rankLast[nBitsToDecrease]].nbBits++;
                        if (rankLast[nBitsToDecrease - 1] == noSymbol)
                        {
                            rankLast[nBitsToDecrease - 1] = rankLast[nBitsToDecrease];
                        }

                        if (rankLast[nBitsToDecrease] == 0)
                        {
                            rankLast[nBitsToDecrease] = noSymbol;
                        }
                        else
                        {
                            rankLast[nBitsToDecrease]--;
                            if (huffNode[rankLast[nBitsToDecrease]].nbBits != maxNbBits - nBitsToDecrease)
                            {
                                rankLast[nBitsToDecrease] = noSymbol;
                            }
                        }
                    }

                    while (totalCost < 0)
                    {
                        if (rankLast[1] == noSymbol)
                        {
                            while (huffNode[n].nbBits == maxNbBits)
                            {
                                n--;
                            }

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

            return maxNbBits;
        }

        /**
         * HUF_sort():
         * Sorts the symbols [0, maxSymbolValue] by count[symbol] in decreasing order.
         *
         * @param[out] huffNode       Sorted symbols by decreasing count. Only members `.count` and `.byte` are filled.
         *                            Must have (maxSymbolValue + 1) entries.
         * @param[in]  count          Histogram of the symbols.
         * @param[in]  maxSymbolValue Maximum symbol value.
         * @param      rankPosition   This is a scratch workspace. Must have RANK_POSITION_TABLE_SIZE entries.
         */
        private static void HUF_sort(nodeElt_s* huffNode, uint* count, uint maxSymbolValue, rankPos* rankPosition)
        {
            int n;
            int maxSymbolValue1 = (int)(maxSymbolValue) + 1;

            memset((void*)(rankPosition), (0), ((nuint)(sizeof(rankPos)) * 32));
            for (n = 0; n < maxSymbolValue1; ++n)
            {
                uint lowerRank = BIT_highbit32(count[n] + 1);

                rankPosition[lowerRank].@base++;
            }

            assert(rankPosition[32 - 1].@base == 0);
            for (n = 32 - 1; n > 0; --n)
            {
                rankPosition[n - 1].@base += rankPosition[n].@base;
                rankPosition[n - 1].curr = rankPosition[n - 1].@base;
            }

            for (n = 0; n < maxSymbolValue1; ++n)
            {
                uint c = count[n];
                uint r = BIT_highbit32(c + 1) + 1;
                uint pos = rankPosition[r].curr++;

                while ((pos > rankPosition[r].@base) && (c > huffNode[pos - 1].count))
                {
                    huffNode[pos] = huffNode[pos - 1];
                    pos--;
                }

                huffNode[pos].count = c;
                huffNode[pos].@byte = (byte)(n);
            }
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
            int lowS, lowN;
            int nodeNb = (255 + 1);
            int n, nodeRoot;

            nonNullRank = (int)(maxSymbolValue);
            while (huffNode[nonNullRank].count == 0)
            {
                nonNullRank--;
            }

            lowS = nonNullRank;
            nodeRoot = nodeNb + lowS - 1;
            lowN = nodeNb;
            huffNode[nodeNb].count = huffNode[lowS].count + huffNode[lowS - 1].count;
            huffNode[lowS].parent = huffNode[lowS - 1].parent = (ushort)(nodeNb);
            nodeNb++;
            lowS -= 2;
            for (n = nodeNb; n <= nodeRoot; n++)
            {
                huffNode[n].count = (uint)(1U << 30);
            }

            huffNode0[0].count = (uint)(1U << 31);
            while (nodeNb <= nodeRoot)
            {
                int n1 = (huffNode[lowS].count < huffNode[lowN].count) ? lowS-- : lowN++;
                int n2 = (huffNode[lowS].count < huffNode[lowN].count) ? lowS-- : lowN++;

                huffNode[nodeNb].count = huffNode[n1].count + huffNode[n2].count;
                huffNode[n1].parent = huffNode[n2].parent = (ushort)(nodeNb);
                nodeNb++;
            }

            huffNode[nodeRoot].nbBits = 0;
            for (n = nodeRoot - 1; n >= (255 + 1); n--)
            {
                huffNode[n].nbBits = (byte)(huffNode[huffNode[n].parent].nbBits + 1);
            }

            for (n = 0; n <= nonNullRank; n++)
            {
                huffNode[n].nbBits = (byte)(huffNode[huffNode[n].parent].nbBits + 1);
            }

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
        private static void HUF_buildCTableFromTree(HUF_CElt_s* CTable, nodeElt_s* huffNode, int nonNullRank, uint maxSymbolValue, uint maxNbBits)
        {
            int n;
            ushort* nbPerRank = stackalloc ushort[13];
            memset(nbPerRank, 0, sizeof(ushort) * 13);
            ushort* valPerRank = stackalloc ushort[13];
            memset(valPerRank, 0, sizeof(ushort) * 13);
            int alphabetSize = (int)(maxSymbolValue + 1);

            for (n = 0; n <= nonNullRank; n++)
            {
                nbPerRank[huffNode[n].nbBits]++;
            }


            {
                ushort min = 0;

                for (n = (int)(maxNbBits); n > 0; n--)
                {
                    valPerRank[n] = min;
                    min += (ushort)(nbPerRank[n]);
                    min >>= 1;
                }
            }

            for (n = 0; n < alphabetSize; n++)
            {
                CTable[huffNode[n].@byte].nbBits = huffNode[n].nbBits;
            }

            for (n = 0; n < alphabetSize; n++)
            {
                CTable[n].val = valPerRank[CTable[n].nbBits]++;
            }
        }

        public static nuint HUF_buildCTable_wksp(HUF_CElt_s* tree, uint* count, uint maxSymbolValue, uint maxNbBits, void* workSpace, nuint wkspSize)
        {
            HUF_buildCTable_wksp_tables* wksp_tables = (HUF_buildCTable_wksp_tables*)(workSpace);
            nodeElt_s* huffNode0 = (nodeElt_s*)wksp_tables->huffNodeTbl;
            nodeElt_s* huffNode = huffNode0 + 1;
            int nonNullRank;

            if (((nuint)(workSpace) & 3) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (wkspSize < (nuint)(sizeof(HUF_buildCTable_wksp_tables)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall)));
            }

            if (maxNbBits == 0)
            {
                maxNbBits = 11;
            }

            if (maxSymbolValue > 255)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge)));
            }

            memset((void*)(huffNode0), (0), ((nuint)(sizeof(nodeElt_s) * 512)));
            HUF_sort(huffNode, count, maxSymbolValue, (rankPos*)wksp_tables->rankPosition);
            nonNullRank = HUF_buildTree(huffNode, maxSymbolValue);
            maxNbBits = HUF_setMaxHeight(huffNode, (uint)(nonNullRank), maxNbBits);
            if (maxNbBits > 12)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            HUF_buildCTableFromTree(tree, huffNode, nonNullRank, maxSymbolValue, maxNbBits);
            return maxNbBits;
        }

        public static nuint HUF_estimateCompressedSize(HUF_CElt_s* CTable, uint* count, uint maxSymbolValue)
        {
            nuint nbBits = 0;
            int s;

            for (s = 0; s <= (int)(maxSymbolValue); ++s)
            {
                nbBits += CTable[s].nbBits * count[s];
            }

            return nbBits >> 3;
        }

        public static int HUF_validateCTable(HUF_CElt_s* CTable, uint* count, uint maxSymbolValue)
        {
            int bad = 0;
            int s;

            for (s = 0; s <= (int)(maxSymbolValue); ++s)
            {
                bad |= ((((count[s] != 0) && (CTable[s].nbBits == 0))) ? 1 : 0);
            }

            return (bad == 0 ? 1 : 0);
        }

        public static nuint HUF_compressBound(nuint size)
        {
            return (129 + (size + (size >> 8) + 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HUF_encodeSymbol(BIT_CStream_t* bitCPtr, uint symbol, HUF_CElt_s* CTable)
        {
            BIT_addBitsFast(bitCPtr, CTable[symbol].val, CTable[symbol].nbBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint HUF_compress1X_usingCTable_internal_body(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable)
        {
            byte* ip = (byte*)(src);
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstSize;
            byte* op = ostart;
            nuint n;
            BIT_CStream_t bitC;

            if (dstSize < 8)
            {
                return 0;
            }


            {
                nuint initErr = BIT_initCStream(&bitC, (void*)op, (nuint)(oend - op));

                if ((ERR_isError(initErr)) != 0)
                {
                    return 0;
                }
            }

            n = srcSize & unchecked((nuint)unchecked(~3));
            switch (srcSize & 3)
            {
                case 3:
                {
                    HUF_encodeSymbol(&bitC, ip[n + 2], CTable);
                }

                if ((nuint)(sizeof(nuint)) * 8 < (uint)(12 * 4 + 7))
                {
                    BIT_flushBits(&bitC);
                }


                goto case 2;
                case 2:
                {
                    HUF_encodeSymbol(&bitC, ip[n + 1], CTable);
                }

                if ((nuint)(sizeof(nuint)) * 8 < (uint)(12 * 2 + 7))
                {
                    BIT_flushBits(&bitC);
                }


                goto case 1;
                case 1:
                {
                    HUF_encodeSymbol(&bitC, ip[n + 0], CTable);
                }

                BIT_flushBits(&bitC);

                goto case 0;
                case 0:
                default:
                {
                    break;
                }
            }

            for (; n > 0; n -= 4)
            {
                HUF_encodeSymbol(&bitC, ip[n - 1], CTable);
                if ((nuint)(sizeof(nuint)) * 8 < (uint)(12 * 2 + 7))
                {
                    BIT_flushBits(&bitC);
                }

                HUF_encodeSymbol(&bitC, ip[n - 2], CTable);
                if ((nuint)(sizeof(nuint)) * 8 < (uint)(12 * 4 + 7))
                {
                    BIT_flushBits(&bitC);
                }

                HUF_encodeSymbol(&bitC, ip[n - 3], CTable);
                if ((nuint)(sizeof(nuint)) * 8 < (uint)(12 * 2 + 7))
                {
                    BIT_flushBits(&bitC);
                }

                HUF_encodeSymbol(&bitC, ip[n - 4], CTable);
                BIT_flushBits(&bitC);
            }

            return BIT_closeCStream(&bitC);
        }

        private static nuint HUF_compress1X_usingCTable_internal_bmi2(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable)
        {
            return HUF_compress1X_usingCTable_internal_body(dst, dstSize, src, srcSize, CTable);
        }

        private static nuint HUF_compress1X_usingCTable_internal_default(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable)
        {
            return HUF_compress1X_usingCTable_internal_body(dst, dstSize, src, srcSize, CTable);
        }

        private static nuint HUF_compress1X_usingCTable_internal(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable, int bmi2)
        {
            if (bmi2 != 0)
            {
                return HUF_compress1X_usingCTable_internal_bmi2(dst, dstSize, src, srcSize, CTable);
            }

            return HUF_compress1X_usingCTable_internal_default(dst, dstSize, src, srcSize, CTable);
        }

        public static nuint HUF_compress1X_usingCTable(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable)
        {
            return HUF_compress1X_usingCTable_internal(dst, dstSize, src, srcSize, CTable, 0);
        }

        private static nuint HUF_compress4X_usingCTable_internal(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable, int bmi2)
        {
            nuint segmentSize = (srcSize + 3) / 4;
            byte* ip = (byte*)(src);
            byte* iend = ip + srcSize;
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstSize;
            byte* op = ostart;

            if (dstSize < (uint)(6 + 1 + 1 + 1 + 8))
            {
                return 0;
            }

            if (srcSize < 12)
            {
                return 0;
            }

            op += 6;
            assert(op <= oend);

            {
                nuint cSize = HUF_compress1X_usingCTable_internal((void*)op, (nuint)(oend - op), (void*)ip, segmentSize, CTable, bmi2);

                if ((ERR_isError(cSize)) != 0)
                {
                    return cSize;
                }

                if (cSize == 0)
                {
                    return 0;
                }

                assert(cSize <= 65535);
                MEM_writeLE16((void*)ostart, (ushort)(cSize));
                op += cSize;
            }

            ip += segmentSize;
            assert(op <= oend);

            {
                nuint cSize = HUF_compress1X_usingCTable_internal((void*)op, (nuint)(oend - op), (void*)ip, segmentSize, CTable, bmi2);

                if ((ERR_isError(cSize)) != 0)
                {
                    return cSize;
                }

                if (cSize == 0)
                {
                    return 0;
                }

                assert(cSize <= 65535);
                MEM_writeLE16((void*)(ostart + 2), (ushort)(cSize));
                op += cSize;
            }

            ip += segmentSize;
            assert(op <= oend);

            {
                nuint cSize = HUF_compress1X_usingCTable_internal((void*)op, (nuint)(oend - op), (void*)ip, segmentSize, CTable, bmi2);

                if ((ERR_isError(cSize)) != 0)
                {
                    return cSize;
                }

                if (cSize == 0)
                {
                    return 0;
                }

                assert(cSize <= 65535);
                MEM_writeLE16((void*)(ostart + 4), (ushort)(cSize));
                op += cSize;
            }

            ip += segmentSize;
            assert(op <= oend);
            assert(ip <= iend);

            {
                nuint cSize = HUF_compress1X_usingCTable_internal((void*)op, (nuint)(oend - op), (void*)ip, (nuint)(iend - ip), CTable, bmi2);

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

            return (nuint)(op - ostart);
        }

        public static nuint HUF_compress4X_usingCTable(void* dst, nuint dstSize, void* src, nuint srcSize, HUF_CElt_s* CTable)
        {
            return HUF_compress4X_usingCTable_internal(dst, dstSize, src, srcSize, CTable, 0);
        }

        private static nuint HUF_compressCTable_internal(byte* ostart, byte* op, byte* oend, void* src, nuint srcSize, HUF_nbStreams_e nbStreams, HUF_CElt_s* CTable, int bmi2)
        {
            nuint cSize = (nbStreams == HUF_nbStreams_e.HUF_singleStream) ? HUF_compress1X_usingCTable_internal((void*)op, (nuint)(oend - op), src, srcSize, CTable, bmi2) : HUF_compress4X_usingCTable_internal((void*)op, (nuint)(oend - op), src, srcSize, CTable, bmi2);

            if ((ERR_isError(cSize)) != 0)
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

        /* HUF_compress_internal() :
         * `workSpace_align4` must be aligned on 4-bytes boundaries,
         * and occupies the same space as a table of HUF_WORKSPACE_SIZE_U32 unsigned */
        private static nuint HUF_compress_internal(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog, HUF_nbStreams_e nbStreams, void* workSpace_align4, nuint wkspSize, HUF_CElt_s* oldHufTable, HUF_repeat* repeat, int preferRepeat, int bmi2)
        {
            HUF_compress_tables_t* table = (HUF_compress_tables_t*)(workSpace_align4);
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstSize;
            byte* op = ostart;

            assert(((nuint)(workSpace_align4) & 3) == 0);
            if (wkspSize < (uint)(((6 << 10) + 256)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall)));
            }

            if (srcSize == 0)
            {
                return 0;
            }

            if (dstSize == 0)
            {
                return 0;
            }

            if (srcSize > (uint)((128 * 1024)))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_srcSize_wrong)));
            }

            if (huffLog > 12)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_tableLog_tooLarge)));
            }

            if (maxSymbolValue > 255)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooLarge)));
            }

            if (maxSymbolValue == 0)
            {
                maxSymbolValue = 255;
            }

            if (huffLog == 0)
            {
                huffLog = 11;
            }

            if (preferRepeat != 0 && repeat != null && *repeat == HUF_repeat.HUF_repeat_valid)
            {
                return HUF_compressCTable_internal(ostart, op, oend, src, srcSize, nbStreams, oldHufTable, bmi2);
            }


            {
                nuint largest = HIST_count_wksp((uint*)table->count, &maxSymbolValue, (void*)(byte*)(src), srcSize, workSpace_align4, wkspSize);

                if ((ERR_isError(largest)) != 0)
                {
                    return largest;
                }

                if (largest == srcSize)
                {
                    *ostart = ((byte*)(src))[0];
                    return 1;
                }

                if (largest <= (srcSize >> 7) + 4)
                {
                    return 0;
                }
            }

            if (repeat != null && *repeat == HUF_repeat.HUF_repeat_check && (HUF_validateCTable(oldHufTable, (uint*)table->count, maxSymbolValue)) == 0)
            {
                *repeat = HUF_repeat.HUF_repeat_none;
            }

            if (preferRepeat != 0 && repeat != null && *repeat != HUF_repeat.HUF_repeat_none)
            {
                return HUF_compressCTable_internal(ostart, op, oend, src, srcSize, nbStreams, oldHufTable, bmi2);
            }

            huffLog = HUF_optimalTableLog(huffLog, srcSize, maxSymbolValue);

            {
                nuint maxBits = HUF_buildCTable_wksp((HUF_CElt_s*)table->CTable, (uint*)table->count, maxSymbolValue, huffLog, (void*)&table->wksps.buildCTable_wksp, (nuint)(4352));


                {
                    nuint _var_err__ = maxBits;

                    if ((ERR_isError(_var_err__)) != 0)
                    {
                        return _var_err__;
                    }
                }

                huffLog = (uint)(maxBits);
                memset((void*)((table->CTable + (maxSymbolValue + 1))), (0), ((nuint)(sizeof(HUF_CElt_s) * 256) - ((maxSymbolValue + 1) * (nuint)(sizeof(HUF_CElt_s)))));
            }


            {
                nuint hSize = HUF_writeCTable_wksp((void*)op, dstSize, (HUF_CElt_s*)table->CTable, maxSymbolValue, huffLog, (void*)&table->wksps.writeCTable_wksp, (nuint)(704));

                if ((ERR_isError(hSize)) != 0)
                {
                    return hSize;
                }

                if (repeat != null && *repeat != HUF_repeat.HUF_repeat_none)
                {
                    nuint oldSize = HUF_estimateCompressedSize(oldHufTable, (uint*)table->count, maxSymbolValue);
                    nuint newSize = HUF_estimateCompressedSize((HUF_CElt_s*)table->CTable, (uint*)table->count, maxSymbolValue);

                    if (oldSize <= hSize + newSize || hSize + 12 >= srcSize)
                    {
                        return HUF_compressCTable_internal(ostart, op, oend, src, srcSize, nbStreams, oldHufTable, bmi2);
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
                {
                    memcpy((void*)(oldHufTable), (void*)(table->CTable), ((nuint)(sizeof(HUF_CElt_s) * 256)));
                }
            }

            return HUF_compressCTable_internal(ostart, op, oend, src, srcSize, nbStreams, (HUF_CElt_s*)table->CTable, bmi2);
        }

        public static nuint HUF_compress1X_wksp(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog, void* workSpace, nuint wkspSize)
        {
            return HUF_compress_internal(dst, dstSize, src, srcSize, maxSymbolValue, huffLog, HUF_nbStreams_e.HUF_singleStream, workSpace, wkspSize, (HUF_CElt_s*)null, (HUF_repeat*)null, 0, 0);
        }

        /** HUF_compress1X_repeat() :
         *  Same as HUF_compress1X_wksp(), but considers using hufTable if *repeat != HUF_repeat_none.
         *  If it uses hufTable it does not modify hufTable or repeat.
         *  If it doesn't, it sets *repeat = HUF_repeat_none, and it sets hufTable to the table used.
         *  If preferRepeat then the old table will always be used if valid. */
        public static nuint HUF_compress1X_repeat(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog, void* workSpace, nuint wkspSize, HUF_CElt_s* hufTable, HUF_repeat* repeat, int preferRepeat, int bmi2)
        {
            return HUF_compress_internal(dst, dstSize, src, srcSize, maxSymbolValue, huffLog, HUF_nbStreams_e.HUF_singleStream, workSpace, wkspSize, hufTable, repeat, preferRepeat, bmi2);
        }

        /* HUF_compress4X_repeat():
         * compress input using 4 streams.
         * provide workspace to generate compression tables */
        public static nuint HUF_compress4X_wksp(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog, void* workSpace, nuint wkspSize)
        {
            return HUF_compress_internal(dst, dstSize, src, srcSize, maxSymbolValue, huffLog, HUF_nbStreams_e.HUF_fourStreams, workSpace, wkspSize, (HUF_CElt_s*)null, (HUF_repeat*)null, 0, 0);
        }

        /* HUF_compress4X_repeat():
         * compress input using 4 streams.
         * re-use an existing huffman compression table */
        public static nuint HUF_compress4X_repeat(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog, void* workSpace, nuint wkspSize, HUF_CElt_s* hufTable, HUF_repeat* repeat, int preferRepeat, int bmi2)
        {
            return HUF_compress_internal(dst, dstSize, src, srcSize, maxSymbolValue, huffLog, HUF_nbStreams_e.HUF_fourStreams, workSpace, wkspSize, hufTable, repeat, preferRepeat, bmi2);
        }

        /** HUF_buildCTable() :
         * @return : maxNbBits
         *  Note : count is used before tree is written, so they can safely overlap
         */
        public static nuint HUF_buildCTable(HUF_CElt_s* tree, uint* count, uint maxSymbolValue, uint maxNbBits)
        {
            HUF_buildCTable_wksp_tables workspace;

            return HUF_buildCTable_wksp(tree, count, maxSymbolValue, maxNbBits, (void*)&workspace, (nuint)(sizeof(HUF_buildCTable_wksp_tables)));
        }

        /* ====================== */
        /* single stream variants */
        /* ====================== */
        public static nuint HUF_compress1X(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog)
        {
            uint* workSpace = stackalloc uint[1600];

            return HUF_compress1X_wksp(dst, dstSize, src, srcSize, maxSymbolValue, huffLog, (void*)workSpace, (nuint)(sizeof(uint) * 1600));
        }

        /** HUF_compress2() :
         *  Same as HUF_compress(), but offers control over `maxSymbolValue` and `tableLog`.
         * `maxSymbolValue` must be <= HUF_SYMBOLVALUE_MAX .
         * `tableLog` must be `<= HUF_TABLELOG_MAX` . */
        public static nuint HUF_compress2(void* dst, nuint dstSize, void* src, nuint srcSize, uint maxSymbolValue, uint huffLog)
        {
            uint* workSpace = stackalloc uint[1600];

            return HUF_compress4X_wksp(dst, dstSize, src, srcSize, maxSymbolValue, huffLog, (void*)workSpace, (nuint)(sizeof(uint) * 1600));
        }

        /** HUF_compress() :
         *  Compress content from buffer 'src', of size 'srcSize', into buffer 'dst'.
         * 'dst' buffer must be already allocated.
         *  Compression runs faster if `dstCapacity` >= HUF_compressBound(srcSize).
         * `srcSize` must be <= `HUF_BLOCKSIZE_MAX` == 128 KB.
         * @return : size of compressed data (<= `dstCapacity`).
         *  Special values : if return == 0, srcData is not compressible => Nothing is stored within dst !!!
         *                   if HUF_isError(return), compression failed (more details using HUF_getErrorName())
         */
        public static nuint HUF_compress(void* dst, nuint maxDstSize, void* src, nuint srcSize)
        {
            return HUF_compress2(dst, maxDstSize, src, srcSize, 255, 11);
        }
    }
}
