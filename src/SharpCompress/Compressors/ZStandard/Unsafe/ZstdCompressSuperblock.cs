using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /** ZSTD_compressSubBlock_literal() :
     *  Compresses literals section for a sub-block.
     *  When we have to write the Huffman table we will sometimes choose a header
     *  size larger than necessary. This is because we have to pick the header size
     *  before we know the table size + compressed size, so we have a bound on the
     *  table size. If we guessed incorrectly, we fall back to uncompressed literals.
     *
     *  We write the header when writeEntropy=1 and set entropyWritten=1 when we succeeded
     *  in writing the header, otherwise it is set to 0.
     *
     *  hufMetadata->hType has literals block type info.
     *      If it is set_basic, all sub-blocks literals section will be Raw_Literals_Block.
     *      If it is set_rle, all sub-blocks literals section will be RLE_Literals_Block.
     *      If it is set_compressed, first sub-block's literals section will be Compressed_Literals_Block
     *      If it is set_compressed, first sub-block's literals section will be Treeless_Literals_Block
     *      and the following sub-blocks' literals sections will be Treeless_Literals_Block.
     *  @return : compressed size of literals section of a sub-block
     *            Or 0 if unable to compress.
     *            Or error code */
    private static nuint ZSTD_compressSubBlock_literal(
        nuint* hufTable,
        ZSTD_hufCTablesMetadata_t* hufMetadata,
        byte* literals,
        nuint litSize,
        void* dst,
        nuint dstSize,
        int bmi2,
        int writeEntropy,
        int* entropyWritten
    )
    {
        nuint header = (nuint)(writeEntropy != 0 ? 200 : 0);
        nuint lhSize = (nuint)(
            3
            + (litSize >= 1 * (1 << 10) - header ? 1 : 0)
            + (litSize >= 16 * (1 << 10) - header ? 1 : 0)
        );
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstSize;
        byte* op = ostart + lhSize;
        uint singleStream = lhSize == 3 ? 1U : 0U;
        SymbolEncodingType_e hType =
            writeEntropy != 0 ? hufMetadata->hType : SymbolEncodingType_e.set_repeat;
        nuint cLitSize = 0;
        *entropyWritten = 0;
        if (litSize == 0 || hufMetadata->hType == SymbolEncodingType_e.set_basic)
        {
            return ZSTD_noCompressLiterals(dst, dstSize, literals, litSize);
        }
        else if (hufMetadata->hType == SymbolEncodingType_e.set_rle)
        {
            return ZSTD_compressRleLiteralsBlock(dst, dstSize, literals, litSize);
        }

        assert(litSize > 0);
        assert(
            hufMetadata->hType == SymbolEncodingType_e.set_compressed
                || hufMetadata->hType == SymbolEncodingType_e.set_repeat
        );
        if (writeEntropy != 0 && hufMetadata->hType == SymbolEncodingType_e.set_compressed)
        {
            memcpy(op, hufMetadata->hufDesBuffer, (uint)hufMetadata->hufDesSize);
            op += hufMetadata->hufDesSize;
            cLitSize += hufMetadata->hufDesSize;
        }

        {
            int flags = bmi2 != 0 ? (int)HUF_flags_e.HUF_flags_bmi2 : 0;
            nuint cSize =
                singleStream != 0
                    ? HUF_compress1X_usingCTable(
                        op,
                        (nuint)(oend - op),
                        literals,
                        litSize,
                        hufTable,
                        flags
                    )
                    : HUF_compress4X_usingCTable(
                        op,
                        (nuint)(oend - op),
                        literals,
                        litSize,
                        hufTable,
                        flags
                    );
            op += cSize;
            cLitSize += cSize;
            if (cSize == 0 || ERR_isError(cSize))
            {
                return 0;
            }

            if (writeEntropy == 0 && cLitSize >= litSize)
            {
                return ZSTD_noCompressLiterals(dst, dstSize, literals, litSize);
            }

            if (
                lhSize
                < (nuint)(
                    3 + (cLitSize >= 1 * (1 << 10) ? 1 : 0) + (cLitSize >= 16 * (1 << 10) ? 1 : 0)
                )
            )
            {
                assert(cLitSize > litSize);
                return ZSTD_noCompressLiterals(dst, dstSize, literals, litSize);
            }
        }

        switch (lhSize)
        {
            case 3:
            {
                uint lhc =
                    (uint)hType
                    + ((singleStream == 0 ? 1U : 0U) << 2)
                    + ((uint)litSize << 4)
                    + ((uint)cLitSize << 14);
                MEM_writeLE24(ostart, lhc);
                break;
            }

            case 4:
            {
                uint lhc = (uint)(hType + (2 << 2)) + ((uint)litSize << 4) + ((uint)cLitSize << 18);
                MEM_writeLE32(ostart, lhc);
                break;
            }

            case 5:
            {
                uint lhc = (uint)(hType + (3 << 2)) + ((uint)litSize << 4) + ((uint)cLitSize << 22);
                MEM_writeLE32(ostart, lhc);
                ostart[4] = (byte)(cLitSize >> 10);
                break;
            }

            default:
                assert(0 != 0);
                break;
        }

        *entropyWritten = 1;
        return (nuint)(op - ostart);
    }

    private static nuint ZSTD_seqDecompressedSize(
        SeqStore_t* seqStore,
        SeqDef_s* sequences,
        nuint nbSeqs,
        nuint litSize,
        int lastSubBlock
    )
    {
        nuint matchLengthSum = 0;
        nuint litLengthSum = 0;
        nuint n;
        for (n = 0; n < nbSeqs; n++)
        {
            ZSTD_SequenceLength seqLen = ZSTD_getSequenceLength(seqStore, sequences + n);
            litLengthSum += seqLen.litLength;
            matchLengthSum += seqLen.matchLength;
        }

        if (lastSubBlock == 0)
            assert(litLengthSum == litSize);
        else
            assert(litLengthSum <= litSize);
        return matchLengthSum + litSize;
    }

    /** ZSTD_compressSubBlock_sequences() :
     *  Compresses sequences section for a sub-block.
     *  fseMetadata->llType, fseMetadata->ofType, and fseMetadata->mlType have
     *  symbol compression modes for the super-block.
     *  The first successfully compressed block will have these in its header.
     *  We set entropyWritten=1 when we succeed in compressing the sequences.
     *  The following sub-blocks will always have repeat mode.
     *  @return : compressed size of sequences section of a sub-block
     *            Or 0 if it is unable to compress
     *            Or error code. */
    private static nuint ZSTD_compressSubBlock_sequences(
        ZSTD_fseCTables_t* fseTables,
        ZSTD_fseCTablesMetadata_t* fseMetadata,
        SeqDef_s* sequences,
        nuint nbSeq,
        byte* llCode,
        byte* mlCode,
        byte* ofCode,
        ZSTD_CCtx_params_s* cctxParams,
        void* dst,
        nuint dstCapacity,
        int bmi2,
        int writeEntropy,
        int* entropyWritten
    )
    {
        int longOffsets = cctxParams->cParams.windowLog > (uint)(MEM_32bits ? 25 : 57) ? 1 : 0;
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstCapacity;
        byte* op = ostart;
        byte* seqHead;
        *entropyWritten = 0;
        if (oend - op < 3 + 1)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall));
        }

        if (nbSeq < 128)
            *op++ = (byte)nbSeq;
        else if (nbSeq < 0x7F00)
        {
            op[0] = (byte)((nbSeq >> 8) + 0x80);
            op[1] = (byte)nbSeq;
            op += 2;
        }
        else
        {
            op[0] = 0xFF;
            MEM_writeLE16(op + 1, (ushort)(nbSeq - 0x7F00));
            op += 3;
        }

        if (nbSeq == 0)
        {
            return (nuint)(op - ostart);
        }

        seqHead = op++;
        if (writeEntropy != 0)
        {
            uint LLtype = (uint)fseMetadata->llType;
            uint Offtype = (uint)fseMetadata->ofType;
            uint MLtype = (uint)fseMetadata->mlType;
            *seqHead = (byte)((LLtype << 6) + (Offtype << 4) + (MLtype << 2));
            memcpy(op, fseMetadata->fseTablesBuffer, (uint)fseMetadata->fseTablesSize);
            op += fseMetadata->fseTablesSize;
        }
        else
        {
            uint repeat = (uint)SymbolEncodingType_e.set_repeat;
            *seqHead = (byte)((repeat << 6) + (repeat << 4) + (repeat << 2));
        }

        {
            nuint bitstreamSize = ZSTD_encodeSequences(
                op,
                (nuint)(oend - op),
                fseTables->matchlengthCTable,
                mlCode,
                fseTables->offcodeCTable,
                ofCode,
                fseTables->litlengthCTable,
                llCode,
                sequences,
                nbSeq,
                longOffsets,
                bmi2
            );
            {
                nuint err_code = bitstreamSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            op += bitstreamSize;
            if (
                writeEntropy != 0
                && fseMetadata->lastCountSize != 0
                && fseMetadata->lastCountSize + bitstreamSize < 4
            )
            {
                assert(fseMetadata->lastCountSize + bitstreamSize == 3);
                return 0;
            }
        }

        if (op - seqHead < 4)
        {
            return 0;
        }

        *entropyWritten = 1;
        return (nuint)(op - ostart);
    }

    /** ZSTD_compressSubBlock() :
     *  Compresses a single sub-block.
     *  @return : compressed size of the sub-block
     *            Or 0 if it failed to compress. */
    private static nuint ZSTD_compressSubBlock(
        ZSTD_entropyCTables_t* entropy,
        ZSTD_entropyCTablesMetadata_t* entropyMetadata,
        SeqDef_s* sequences,
        nuint nbSeq,
        byte* literals,
        nuint litSize,
        byte* llCode,
        byte* mlCode,
        byte* ofCode,
        ZSTD_CCtx_params_s* cctxParams,
        void* dst,
        nuint dstCapacity,
        int bmi2,
        int writeLitEntropy,
        int writeSeqEntropy,
        int* litEntropyWritten,
        int* seqEntropyWritten,
        uint lastBlock
    )
    {
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstCapacity;
        byte* op = ostart + ZSTD_blockHeaderSize;
        {
            nuint cLitSize = ZSTD_compressSubBlock_literal(
                &entropy->huf.CTable.e0,
                &entropyMetadata->hufMetadata,
                literals,
                litSize,
                op,
                (nuint)(oend - op),
                bmi2,
                writeLitEntropy,
                litEntropyWritten
            );
            {
                nuint err_code = cLitSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (cLitSize == 0)
                return 0;
            op += cLitSize;
        }

        {
            nuint cSeqSize = ZSTD_compressSubBlock_sequences(
                &entropy->fse,
                &entropyMetadata->fseMetadata,
                sequences,
                nbSeq,
                llCode,
                mlCode,
                ofCode,
                cctxParams,
                op,
                (nuint)(oend - op),
                bmi2,
                writeSeqEntropy,
                seqEntropyWritten
            );
            {
                nuint err_code = cSeqSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (cSeqSize == 0)
                return 0;
            op += cSeqSize;
        }

        {
            nuint cSize = (nuint)(op - ostart) - ZSTD_blockHeaderSize;
            uint cBlockHeader24 =
                lastBlock + ((uint)blockType_e.bt_compressed << 1) + (uint)(cSize << 3);
            MEM_writeLE24(ostart, cBlockHeader24);
        }

        return (nuint)(op - ostart);
    }

    private static nuint ZSTD_estimateSubBlockSize_literal(
        byte* literals,
        nuint litSize,
        ZSTD_hufCTables_t* huf,
        ZSTD_hufCTablesMetadata_t* hufMetadata,
        void* workspace,
        nuint wkspSize,
        int writeEntropy
    )
    {
        uint* countWksp = (uint*)workspace;
        uint maxSymbolValue = 255;
        /* Use hard coded size of 3 bytes */
        nuint literalSectionHeaderSize = 3;
        if (hufMetadata->hType == SymbolEncodingType_e.set_basic)
            return litSize;
        else if (hufMetadata->hType == SymbolEncodingType_e.set_rle)
            return 1;
        else if (
            hufMetadata->hType == SymbolEncodingType_e.set_compressed
            || hufMetadata->hType == SymbolEncodingType_e.set_repeat
        )
        {
            nuint largest = HIST_count_wksp(
                countWksp,
                &maxSymbolValue,
                literals,
                litSize,
                workspace,
                wkspSize
            );
            if (ERR_isError(largest))
                return litSize;
            {
                nuint cLitSizeEstimate = HUF_estimateCompressedSize(
                    &huf->CTable.e0,
                    countWksp,
                    maxSymbolValue
                );
                if (writeEntropy != 0)
                    cLitSizeEstimate += hufMetadata->hufDesSize;
                return cLitSizeEstimate + literalSectionHeaderSize;
            }
        }

        assert(0 != 0);
        return 0;
    }

    private static nuint ZSTD_estimateSubBlockSize_symbolType(
        SymbolEncodingType_e type,
        byte* codeTable,
        uint maxCode,
        nuint nbSeq,
        uint* fseCTable,
        byte* additionalBits,
        short* defaultNorm,
        uint defaultNormLog,
        uint defaultMax,
        void* workspace,
        nuint wkspSize
    )
    {
        uint* countWksp = (uint*)workspace;
        byte* ctp = codeTable;
        byte* ctStart = ctp;
        byte* ctEnd = ctStart + nbSeq;
        nuint cSymbolTypeSizeEstimateInBits = 0;
        uint max = maxCode;
        HIST_countFast_wksp(countWksp, &max, codeTable, nbSeq, workspace, wkspSize);
        if (type == SymbolEncodingType_e.set_basic)
        {
            assert(max <= defaultMax);
            cSymbolTypeSizeEstimateInBits =
                max <= defaultMax
                    ? ZSTD_crossEntropyCost(defaultNorm, defaultNormLog, countWksp, max)
                    : unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC));
        }
        else if (type == SymbolEncodingType_e.set_rle)
        {
            cSymbolTypeSizeEstimateInBits = 0;
        }
        else if (
            type == SymbolEncodingType_e.set_compressed
            || type == SymbolEncodingType_e.set_repeat
        )
        {
            cSymbolTypeSizeEstimateInBits = ZSTD_fseBitCost(fseCTable, countWksp, max);
        }

        if (ERR_isError(cSymbolTypeSizeEstimateInBits))
            return nbSeq * 10;
        while (ctp < ctEnd)
        {
            if (additionalBits != null)
                cSymbolTypeSizeEstimateInBits += additionalBits[*ctp];
            else
                cSymbolTypeSizeEstimateInBits += *ctp;
            ctp++;
        }

        return cSymbolTypeSizeEstimateInBits / 8;
    }

    private static nuint ZSTD_estimateSubBlockSize_sequences(
        byte* ofCodeTable,
        byte* llCodeTable,
        byte* mlCodeTable,
        nuint nbSeq,
        ZSTD_fseCTables_t* fseTables,
        ZSTD_fseCTablesMetadata_t* fseMetadata,
        void* workspace,
        nuint wkspSize,
        int writeEntropy
    )
    {
        /* Use hard coded size of 3 bytes */
        const nuint sequencesSectionHeaderSize = 3;
        nuint cSeqSizeEstimate = 0;
        if (nbSeq == 0)
            return sequencesSectionHeaderSize;
        cSeqSizeEstimate += ZSTD_estimateSubBlockSize_symbolType(
            fseMetadata->ofType,
            ofCodeTable,
            31,
            nbSeq,
            fseTables->offcodeCTable,
            null,
            OF_defaultNorm,
            OF_defaultNormLog,
            28,
            workspace,
            wkspSize
        );
        cSeqSizeEstimate += ZSTD_estimateSubBlockSize_symbolType(
            fseMetadata->llType,
            llCodeTable,
            35,
            nbSeq,
            fseTables->litlengthCTable,
            LL_bits,
            LL_defaultNorm,
            LL_defaultNormLog,
            35,
            workspace,
            wkspSize
        );
        cSeqSizeEstimate += ZSTD_estimateSubBlockSize_symbolType(
            fseMetadata->mlType,
            mlCodeTable,
            52,
            nbSeq,
            fseTables->matchlengthCTable,
            ML_bits,
            ML_defaultNorm,
            ML_defaultNormLog,
            52,
            workspace,
            wkspSize
        );
        if (writeEntropy != 0)
            cSeqSizeEstimate += fseMetadata->fseTablesSize;
        return cSeqSizeEstimate + sequencesSectionHeaderSize;
    }

    private static EstimatedBlockSize ZSTD_estimateSubBlockSize(
        byte* literals,
        nuint litSize,
        byte* ofCodeTable,
        byte* llCodeTable,
        byte* mlCodeTable,
        nuint nbSeq,
        ZSTD_entropyCTables_t* entropy,
        ZSTD_entropyCTablesMetadata_t* entropyMetadata,
        void* workspace,
        nuint wkspSize,
        int writeLitEntropy,
        int writeSeqEntropy
    )
    {
        EstimatedBlockSize ebs;
        ebs.estLitSize = ZSTD_estimateSubBlockSize_literal(
            literals,
            litSize,
            &entropy->huf,
            &entropyMetadata->hufMetadata,
            workspace,
            wkspSize,
            writeLitEntropy
        );
        ebs.estBlockSize = ZSTD_estimateSubBlockSize_sequences(
            ofCodeTable,
            llCodeTable,
            mlCodeTable,
            nbSeq,
            &entropy->fse,
            &entropyMetadata->fseMetadata,
            workspace,
            wkspSize,
            writeSeqEntropy
        );
        ebs.estBlockSize += ebs.estLitSize + ZSTD_blockHeaderSize;
        return ebs;
    }

    private static int ZSTD_needSequenceEntropyTables(ZSTD_fseCTablesMetadata_t* fseMetadata)
    {
        if (
            fseMetadata->llType == SymbolEncodingType_e.set_compressed
            || fseMetadata->llType == SymbolEncodingType_e.set_rle
        )
            return 1;
        if (
            fseMetadata->mlType == SymbolEncodingType_e.set_compressed
            || fseMetadata->mlType == SymbolEncodingType_e.set_rle
        )
            return 1;
        if (
            fseMetadata->ofType == SymbolEncodingType_e.set_compressed
            || fseMetadata->ofType == SymbolEncodingType_e.set_rle
        )
            return 1;
        return 0;
    }

    private static nuint countLiterals(SeqStore_t* seqStore, SeqDef_s* sp, nuint seqCount)
    {
        nuint n,
            total = 0;
        assert(sp != null);
        for (n = 0; n < seqCount; n++)
        {
            total += ZSTD_getSequenceLength(seqStore, sp + n).litLength;
        }

        return total;
    }

    private static nuint sizeBlockSequences(
        SeqDef_s* sp,
        nuint nbSeqs,
        nuint targetBudget,
        nuint avgLitCost,
        nuint avgSeqCost,
        int firstSubBlock
    )
    {
        nuint n,
            budget = 0,
            inSize = 0;
        /* generous estimate */
        nuint headerSize = (nuint)firstSubBlock * 120 * 256;
        assert(firstSubBlock == 0 || firstSubBlock == 1);
        budget += headerSize;
        budget += sp[0].litLength * avgLitCost + avgSeqCost;
        if (budget > targetBudget)
            return 1;
        inSize = (nuint)(sp[0].litLength + (sp[0].mlBase + 3));
        for (n = 1; n < nbSeqs; n++)
        {
            nuint currentCost = sp[n].litLength * avgLitCost + avgSeqCost;
            budget += currentCost;
            inSize += (nuint)(sp[n].litLength + (sp[n].mlBase + 3));
            if (budget > targetBudget && budget < inSize * 256)
                break;
        }

        return n;
    }

    /** ZSTD_compressSubBlock_multi() :
     *  Breaks super-block into multiple sub-blocks and compresses them.
     *  Entropy will be written into the first block.
     *  The following blocks use repeat_mode to compress.
     *  Sub-blocks are all compressed, except the last one when beneficial.
     *  @return : compressed size of the super block (which features multiple ZSTD blocks)
     *            or 0 if it failed to compress. */
    private static nuint ZSTD_compressSubBlock_multi(
        SeqStore_t* seqStorePtr,
        ZSTD_compressedBlockState_t* prevCBlock,
        ZSTD_compressedBlockState_t* nextCBlock,
        ZSTD_entropyCTablesMetadata_t* entropyMetadata,
        ZSTD_CCtx_params_s* cctxParams,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        int bmi2,
        uint lastBlock,
        void* workspace,
        nuint wkspSize
    )
    {
        SeqDef_s* sstart = seqStorePtr->sequencesStart;
        SeqDef_s* send = seqStorePtr->sequences;
        /* tracks progresses within seqStorePtr->sequences */
        SeqDef_s* sp = sstart;
        nuint nbSeqs = (nuint)(send - sstart);
        byte* lstart = seqStorePtr->litStart;
        byte* lend = seqStorePtr->lit;
        byte* lp = lstart;
        nuint nbLiterals = (nuint)(lend - lstart);
        byte* ip = (byte*)src;
        byte* iend = ip + srcSize;
        byte* ostart = (byte*)dst;
        byte* oend = ostart + dstCapacity;
        byte* op = ostart;
        byte* llCodePtr = seqStorePtr->llCode;
        byte* mlCodePtr = seqStorePtr->mlCode;
        byte* ofCodePtr = seqStorePtr->ofCode;
        /* enforce minimum size, to reduce undesirable side effects */
        const nuint minTarget = 1340;
        nuint targetCBlockSize =
            minTarget > cctxParams->targetCBlockSize ? minTarget : cctxParams->targetCBlockSize;
        int writeLitEntropy =
            entropyMetadata->hufMetadata.hType == SymbolEncodingType_e.set_compressed ? 1 : 0;
        int writeSeqEntropy = 1;
        if (nbSeqs > 0)
        {
            EstimatedBlockSize ebs = ZSTD_estimateSubBlockSize(
                lp,
                nbLiterals,
                ofCodePtr,
                llCodePtr,
                mlCodePtr,
                nbSeqs,
                &nextCBlock->entropy,
                entropyMetadata,
                workspace,
                wkspSize,
                writeLitEntropy,
                writeSeqEntropy
            );
            /* quick estimation */
            nuint avgLitCost = nbLiterals != 0 ? ebs.estLitSize * 256 / nbLiterals : 256;
            nuint avgSeqCost = (ebs.estBlockSize - ebs.estLitSize) * 256 / nbSeqs;
            nuint nbSubBlocks =
                (ebs.estBlockSize + targetCBlockSize / 2) / targetCBlockSize > 1
                    ? (ebs.estBlockSize + targetCBlockSize / 2) / targetCBlockSize
                    : 1;
            nuint n,
                avgBlockBudget,
                blockBudgetSupp = 0;
            avgBlockBudget = ebs.estBlockSize * 256 / nbSubBlocks;
            if (ebs.estBlockSize > srcSize)
                return 0;
            assert(nbSubBlocks > 0);
            for (n = 0; n < nbSubBlocks - 1; n++)
            {
                /* determine nb of sequences for current sub-block + nbLiterals from next sequence */
                nuint seqCount = sizeBlockSequences(
                    sp,
                    (nuint)(send - sp),
                    avgBlockBudget + blockBudgetSupp,
                    avgLitCost,
                    avgSeqCost,
                    n == 0 ? 1 : 0
                );
                assert(seqCount <= (nuint)(send - sp));
                if (sp + seqCount == send)
                    break;
                assert(seqCount > 0);
                {
                    int litEntropyWritten = 0;
                    int seqEntropyWritten = 0;
                    nuint litSize = countLiterals(seqStorePtr, sp, seqCount);
                    nuint decompressedSize = ZSTD_seqDecompressedSize(
                        seqStorePtr,
                        sp,
                        seqCount,
                        litSize,
                        0
                    );
                    nuint cSize = ZSTD_compressSubBlock(
                        &nextCBlock->entropy,
                        entropyMetadata,
                        sp,
                        seqCount,
                        lp,
                        litSize,
                        llCodePtr,
                        mlCodePtr,
                        ofCodePtr,
                        cctxParams,
                        op,
                        (nuint)(oend - op),
                        bmi2,
                        writeLitEntropy,
                        writeSeqEntropy,
                        &litEntropyWritten,
                        &seqEntropyWritten,
                        0
                    );
                    {
                        nuint err_code = cSize;
                        if (ERR_isError(err_code))
                        {
                            return err_code;
                        }
                    }

                    if (cSize > 0 && cSize < decompressedSize)
                    {
                        assert(ip + decompressedSize <= iend);
                        ip += decompressedSize;
                        lp += litSize;
                        op += cSize;
                        llCodePtr += seqCount;
                        mlCodePtr += seqCount;
                        ofCodePtr += seqCount;
                        if (litEntropyWritten != 0)
                        {
                            writeLitEntropy = 0;
                        }

                        if (seqEntropyWritten != 0)
                        {
                            writeSeqEntropy = 0;
                        }

                        sp += seqCount;
                        blockBudgetSupp = 0;
                    }
                }
            }
        }

        {
            int litEntropyWritten = 0;
            int seqEntropyWritten = 0;
            nuint litSize = (nuint)(lend - lp);
            nuint seqCount = (nuint)(send - sp);
            nuint decompressedSize = ZSTD_seqDecompressedSize(
                seqStorePtr,
                sp,
                seqCount,
                litSize,
                1
            );
            nuint cSize = ZSTD_compressSubBlock(
                &nextCBlock->entropy,
                entropyMetadata,
                sp,
                seqCount,
                lp,
                litSize,
                llCodePtr,
                mlCodePtr,
                ofCodePtr,
                cctxParams,
                op,
                (nuint)(oend - op),
                bmi2,
                writeLitEntropy,
                writeSeqEntropy,
                &litEntropyWritten,
                &seqEntropyWritten,
                lastBlock
            );
            {
                nuint err_code = cSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            if (cSize > 0 && cSize < decompressedSize)
            {
                assert(ip + decompressedSize <= iend);
                ip += decompressedSize;
                lp += litSize;
                op += cSize;
                llCodePtr += seqCount;
                mlCodePtr += seqCount;
                ofCodePtr += seqCount;
                if (litEntropyWritten != 0)
                {
                    writeLitEntropy = 0;
                }

                if (seqEntropyWritten != 0)
                {
                    writeSeqEntropy = 0;
                }

                sp += seqCount;
            }
        }

        if (writeLitEntropy != 0)
        {
            memcpy(
                &nextCBlock->entropy.huf,
                &prevCBlock->entropy.huf,
                (uint)sizeof(ZSTD_hufCTables_t)
            );
        }

        if (
            writeSeqEntropy != 0
            && ZSTD_needSequenceEntropyTables(&entropyMetadata->fseMetadata) != 0
        )
        {
            return 0;
        }

        if (ip < iend)
        {
            /* some data left : last part of the block sent uncompressed */
            nuint rSize = (nuint)(iend - ip);
            nuint cSize = ZSTD_noCompressBlock(op, (nuint)(oend - op), ip, rSize, lastBlock);
            {
                nuint err_code = cSize;
                if (ERR_isError(err_code))
                {
                    return err_code;
                }
            }

            assert(cSize != 0);
            op += cSize;
            if (sp < send)
            {
                SeqDef_s* seq;
                repcodes_s rep;
                memcpy(&rep, prevCBlock->rep, (uint)sizeof(repcodes_s));
                for (seq = sstart; seq < sp; ++seq)
                {
                    ZSTD_updateRep(
                        rep.rep,
                        seq->offBase,
                        ZSTD_getSequenceLength(seqStorePtr, seq).litLength == 0 ? 1U : 0U
                    );
                }

                memcpy(nextCBlock->rep, &rep, (uint)sizeof(repcodes_s));
            }
        }

        return (nuint)(op - ostart);
    }

    /* ZSTD_compressSuperBlock() :
     * Used to compress a super block when targetCBlockSize is being used.
     * The given block will be compressed into multiple sub blocks that are around targetCBlockSize. */
    private static nuint ZSTD_compressSuperBlock(
        ZSTD_CCtx_s* zc,
        void* dst,
        nuint dstCapacity,
        void* src,
        nuint srcSize,
        uint lastBlock
    )
    {
        ZSTD_entropyCTablesMetadata_t entropyMetadata;
        {
            nuint err_code = ZSTD_buildBlockEntropyStats(
                &zc->seqStore,
                &zc->blockState.prevCBlock->entropy,
                &zc->blockState.nextCBlock->entropy,
                &zc->appliedParams,
                &entropyMetadata,
                zc->tmpWorkspace,
                zc->tmpWkspSize
            );
            if (ERR_isError(err_code))
            {
                return err_code;
            }
        }

        return ZSTD_compressSubBlock_multi(
            &zc->seqStore,
            zc->blockState.prevCBlock,
            zc->blockState.nextCBlock,
            &entropyMetadata,
            &zc->appliedParams,
            dst,
            dstCapacity,
            src,
            srcSize,
            zc->bmi2,
            lastBlock,
            zc->tmpWorkspace,
            zc->tmpWkspSize
        );
    }
}
