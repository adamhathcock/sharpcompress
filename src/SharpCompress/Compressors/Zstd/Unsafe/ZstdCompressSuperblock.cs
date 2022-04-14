using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
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
         *            Or 0 if it unable to compress.
         *            Or error code */
        private static nuint ZSTD_compressSubBlock_literal(HUF_CElt_s* hufTable, ZSTD_hufCTablesMetadata_t* hufMetadata, byte* literals, nuint litSize, void* dst, nuint dstSize, int bmi2, int writeEntropy, int* entropyWritten)
        {
            nuint header = (nuint)(writeEntropy != 0 ? 200 : 0);
            nuint lhSize = (nuint)(3 + ((litSize >= ((uint)(1 * (1 << 10)) - header)) ? 1 : 0) + ((litSize >= ((uint)(16 * (1 << 10)) - header)) ? 1 : 0));
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstSize;
            byte* op = ostart + lhSize;
            uint singleStream = ((lhSize == 3) ? 1U : 0U);
            symbolEncodingType_e hType = writeEntropy != 0 ? hufMetadata->hType : symbolEncodingType_e.set_repeat;
            nuint cLitSize = 0;

            *entropyWritten = 0;
            if (litSize == 0 || hufMetadata->hType == symbolEncodingType_e.set_basic)
            {
                return ZSTD_noCompressLiterals(dst, dstSize, (void*)literals, litSize);
            }
            else if (hufMetadata->hType == symbolEncodingType_e.set_rle)
            {
                return ZSTD_compressRleLiteralsBlock(dst, dstSize, (void*)literals, litSize);
            }

            assert(litSize > 0);
            assert(hufMetadata->hType == symbolEncodingType_e.set_compressed || hufMetadata->hType == symbolEncodingType_e.set_repeat);
            if (writeEntropy != 0 && hufMetadata->hType == symbolEncodingType_e.set_compressed)
            {
                memcpy((void*)(op), (void*)(hufMetadata->hufDesBuffer), (hufMetadata->hufDesSize));
                op += hufMetadata->hufDesSize;
                cLitSize += hufMetadata->hufDesSize;
            }


            {
                nuint cSize = singleStream != 0 ? HUF_compress1X_usingCTable((void*)op, (nuint)(oend - op), (void*)literals, litSize, hufTable) : HUF_compress4X_usingCTable((void*)op, (nuint)(oend - op), (void*)literals, litSize, hufTable);

                op += cSize;
                cLitSize += cSize;
                if (cSize == 0 || (ERR_isError(cSize)) != 0)
                {
                    return 0;
                }

                if (writeEntropy == 0 && cLitSize >= litSize)
                {
                    return ZSTD_noCompressLiterals(dst, dstSize, (void*)literals, litSize);
                }

                if (lhSize < (nuint)(3 + ((cLitSize >= (uint)(1 * (1 << 10))) ? 1 : 0) + ((cLitSize >= (uint)(16 * (1 << 10))) ? 1 : 0)))
                {
                    assert(cLitSize > litSize);
                    return ZSTD_noCompressLiterals(dst, dstSize, (void*)literals, litSize);
                }

            }

            switch (lhSize)
            {
                case 3:
                {
                    uint lhc = (uint)(hType + ((singleStream == 0 ? 1 : 0) << 2)) + ((uint)(litSize) << 4) + ((uint)(cLitSize) << 14);

                    MEM_writeLE24((void*)ostart, lhc);
                    break;
                }

                case 4:
                {
                    uint lhc = (uint)(hType + (2 << 2)) + ((uint)(litSize) << 4) + ((uint)(cLitSize) << 18);

                    MEM_writeLE32((void*)ostart, lhc);
                    break;
                }

                case 5:
                {
                    uint lhc = (uint)(hType + (3 << 2)) + ((uint)(litSize) << 4) + ((uint)(cLitSize) << 22);

                    MEM_writeLE32((void*)ostart, lhc);
                    ostart[4] = (byte)(cLitSize >> 10);
                    break;
                }

                default:
                {
                    assert(0 != 0);
                }
                break;
            }

            *entropyWritten = 1;
            return (nuint)(op - ostart);
        }

        private static nuint ZSTD_seqDecompressedSize(seqStore_t* seqStore, seqDef_s* sequences, nuint nbSeq, nuint litSize, int lastSequence)
        {
            seqDef_s* sstart = sequences;
            seqDef_s* send = sequences + nbSeq;
            seqDef_s* sp = sstart;
            nuint matchLengthSum = 0;
            nuint litLengthSum = 0;

            while (send - sp > 0)
            {
                ZSTD_sequenceLength seqLen = ZSTD_getSequenceLength(seqStore, sp);

                litLengthSum += seqLen.litLength;
                matchLengthSum += seqLen.matchLength;
                sp++;
            }

            assert(litLengthSum <= litSize);
            if (lastSequence == 0)
            {
                assert(litLengthSum == litSize);
            }

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
        private static nuint ZSTD_compressSubBlock_sequences(ZSTD_fseCTables_t* fseTables, ZSTD_fseCTablesMetadata_t* fseMetadata, seqDef_s* sequences, nuint nbSeq, byte* llCode, byte* mlCode, byte* ofCode, ZSTD_CCtx_params_s* cctxParams, void* dst, nuint dstCapacity, int bmi2, int writeEntropy, int* entropyWritten)
        {
            int longOffsets = ((cctxParams->cParams.windowLog > ((uint)(MEM_32bits ? 25 : 57))) ? 1 : 0);
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstCapacity;
            byte* op = ostart;
            byte* seqHead;

            *entropyWritten = 0;
            if ((oend - op) < 3 + 1)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            if (nbSeq < 0x7F)
            {
                *op++ = (byte)(nbSeq);
            }
            else if (nbSeq < 0x7F00)
            {
                op[0] = (byte)((nbSeq >> 8) + 0x80); op[1] = (byte)(nbSeq); op += 2;
            }
            else
            {
                op[0] = 0xFF; MEM_writeLE16((void*)(op + 1), (ushort)(nbSeq - 0x7F00)); op += 3;
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
                memcpy((void*)(op), (void*)(fseMetadata->fseTablesBuffer), (fseMetadata->fseTablesSize));
                op += fseMetadata->fseTablesSize;
            }
            else
            {
                uint repeat = (uint)symbolEncodingType_e.set_repeat;

                *seqHead = (byte)((repeat << 6) + (repeat << 4) + (repeat << 2));
            }


            {
                nuint bitstreamSize = ZSTD_encodeSequences((void*)op, (nuint)(oend - op), (uint*)fseTables->matchlengthCTable, mlCode, (uint*)fseTables->offcodeCTable, ofCode, (uint*)fseTables->litlengthCTable, llCode, sequences, nbSeq, longOffsets, bmi2);


                {
                    nuint err_code = (bitstreamSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                op += bitstreamSize;
                if (writeEntropy != 0 && fseMetadata->lastCountSize != 0 && fseMetadata->lastCountSize + bitstreamSize < 4)
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
        private static nuint ZSTD_compressSubBlock(ZSTD_entropyCTables_t* entropy, ZSTD_entropyCTablesMetadata_t* entropyMetadata, seqDef_s* sequences, nuint nbSeq, byte* literals, nuint litSize, byte* llCode, byte* mlCode, byte* ofCode, ZSTD_CCtx_params_s* cctxParams, void* dst, nuint dstCapacity, int bmi2, int writeLitEntropy, int writeSeqEntropy, int* litEntropyWritten, int* seqEntropyWritten, uint lastBlock)
        {
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstCapacity;
            byte* op = ostart + ZSTD_blockHeaderSize;


            {
                nuint cLitSize = ZSTD_compressSubBlock_literal((HUF_CElt_s*)(entropy->huf.CTable), &entropyMetadata->hufMetadata, literals, litSize, (void*)op, (nuint)(oend - op), bmi2, writeLitEntropy, litEntropyWritten);


                {
                    nuint err_code = (cLitSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (cLitSize == 0)
                {
                    return 0;
                }

                op += cLitSize;
            }


            {
                nuint cSeqSize = ZSTD_compressSubBlock_sequences(&entropy->fse, &entropyMetadata->fseMetadata, sequences, nbSeq, llCode, mlCode, ofCode, cctxParams, (void*)op, (nuint)(oend - op), bmi2, writeSeqEntropy, seqEntropyWritten);


                {
                    nuint err_code = (cSeqSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                if (cSeqSize == 0)
                {
                    return 0;
                }

                op += cSeqSize;
            }


            {
                nuint cSize = (nuint)((ulong)((op - ostart)) - ZSTD_blockHeaderSize);
                uint cBlockHeader24 = lastBlock + (((uint)(blockType_e.bt_compressed)) << 1) + (uint)(cSize << 3);

                MEM_writeLE24((void*)ostart, cBlockHeader24);
            }

            return (nuint)(op - ostart);
        }

        private static nuint ZSTD_estimateSubBlockSize_literal(byte* literals, nuint litSize, ZSTD_hufCTables_t* huf, ZSTD_hufCTablesMetadata_t* hufMetadata, void* workspace, nuint wkspSize, int writeEntropy)
        {
            uint* countWksp = (uint*)(workspace);
            uint maxSymbolValue = 255;
            nuint literalSectionHeaderSize = 3;

            if (hufMetadata->hType == symbolEncodingType_e.set_basic)
            {
                return litSize;
            }
            else if (hufMetadata->hType == symbolEncodingType_e.set_rle)
            {
                return 1;
            }
            else if (hufMetadata->hType == symbolEncodingType_e.set_compressed || hufMetadata->hType == symbolEncodingType_e.set_repeat)
            {
                nuint largest = HIST_count_wksp(countWksp, &maxSymbolValue, (void*)(byte*)(literals), litSize, workspace, wkspSize);

                if ((ERR_isError(largest)) != 0)
                {
                    return litSize;
                }


                {
                    nuint cLitSizeEstimate = HUF_estimateCompressedSize((HUF_CElt_s*)(huf->CTable), countWksp, maxSymbolValue);

                    if (writeEntropy != 0)
                    {
                        cLitSizeEstimate += hufMetadata->hufDesSize;
                    }

                    return cLitSizeEstimate + literalSectionHeaderSize;
                }
            }

            assert(0 != 0);
            return 0;
        }

        private static nuint ZSTD_estimateSubBlockSize_symbolType(symbolEncodingType_e type, byte* codeTable, uint maxCode, nuint nbSeq, uint* fseCTable, uint* additionalBits, short* defaultNorm, uint defaultNormLog, uint defaultMax, void* workspace, nuint wkspSize)
        {
            uint* countWksp = (uint*)(workspace);
            byte* ctp = codeTable;
            byte* ctStart = ctp;
            byte* ctEnd = ctStart + nbSeq;
            nuint cSymbolTypeSizeEstimateInBits = 0;
            uint max = maxCode;

            HIST_countFast_wksp(countWksp, &max, (void*)codeTable, nbSeq, workspace, wkspSize);
            if (type == symbolEncodingType_e.set_basic)
            {
                assert(max <= defaultMax);
                cSymbolTypeSizeEstimateInBits = max <= defaultMax ? ZSTD_crossEntropyCost(defaultNorm, defaultNormLog, countWksp, max) : (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }
            else if (type == symbolEncodingType_e.set_rle)
            {
                cSymbolTypeSizeEstimateInBits = 0;
            }
            else if (type == symbolEncodingType_e.set_compressed || type == symbolEncodingType_e.set_repeat)
            {
                cSymbolTypeSizeEstimateInBits = ZSTD_fseBitCost(fseCTable, countWksp, max);
            }

            if ((ERR_isError(cSymbolTypeSizeEstimateInBits)) != 0)
            {
                return nbSeq * 10;
            }

            while (ctp < ctEnd)
            {
                if (additionalBits != null)
                {
                    cSymbolTypeSizeEstimateInBits += additionalBits[*ctp];
                }
                else
                {
                    cSymbolTypeSizeEstimateInBits += *ctp;
                }

                ctp++;
            }

            return cSymbolTypeSizeEstimateInBits / 8;
        }

        private static nuint ZSTD_estimateSubBlockSize_sequences(byte* ofCodeTable, byte* llCodeTable, byte* mlCodeTable, nuint nbSeq, ZSTD_fseCTables_t* fseTables, ZSTD_fseCTablesMetadata_t* fseMetadata, void* workspace, nuint wkspSize, int writeEntropy)
        {
            nuint sequencesSectionHeaderSize = 3;
            nuint cSeqSizeEstimate = 0;

            if (nbSeq == 0)
            {
                return sequencesSectionHeaderSize;
            }

            cSeqSizeEstimate += ZSTD_estimateSubBlockSize_symbolType(fseMetadata->ofType, ofCodeTable, 31, nbSeq, (uint*)fseTables->offcodeCTable, (uint*)null, (short*)OF_defaultNorm, OF_defaultNormLog, 28, workspace, wkspSize);
            cSeqSizeEstimate += ZSTD_estimateSubBlockSize_symbolType(fseMetadata->llType, llCodeTable, 35, nbSeq, (uint*)fseTables->litlengthCTable, (uint*)LL_bits, (short*)LL_defaultNorm, LL_defaultNormLog, 35, workspace, wkspSize);
            cSeqSizeEstimate += ZSTD_estimateSubBlockSize_symbolType(fseMetadata->mlType, mlCodeTable, 52, nbSeq, (uint*)fseTables->matchlengthCTable, (uint*)ML_bits, (short*)ML_defaultNorm, ML_defaultNormLog, 52, workspace, wkspSize);
            if (writeEntropy != 0)
            {
                cSeqSizeEstimate += fseMetadata->fseTablesSize;
            }

            return cSeqSizeEstimate + sequencesSectionHeaderSize;
        }

        private static nuint ZSTD_estimateSubBlockSize(byte* literals, nuint litSize, byte* ofCodeTable, byte* llCodeTable, byte* mlCodeTable, nuint nbSeq, ZSTD_entropyCTables_t* entropy, ZSTD_entropyCTablesMetadata_t* entropyMetadata, void* workspace, nuint wkspSize, int writeLitEntropy, int writeSeqEntropy)
        {
            nuint cSizeEstimate = 0;

            cSizeEstimate += ZSTD_estimateSubBlockSize_literal(literals, litSize, &entropy->huf, &entropyMetadata->hufMetadata, workspace, wkspSize, writeLitEntropy);
            cSizeEstimate += ZSTD_estimateSubBlockSize_sequences(ofCodeTable, llCodeTable, mlCodeTable, nbSeq, &entropy->fse, &entropyMetadata->fseMetadata, workspace, wkspSize, writeSeqEntropy);
            return cSizeEstimate + ZSTD_blockHeaderSize;
        }

        private static int ZSTD_needSequenceEntropyTables(ZSTD_fseCTablesMetadata_t* fseMetadata)
        {
            if (fseMetadata->llType == symbolEncodingType_e.set_compressed || fseMetadata->llType == symbolEncodingType_e.set_rle)
            {
                return 1;
            }

            if (fseMetadata->mlType == symbolEncodingType_e.set_compressed || fseMetadata->mlType == symbolEncodingType_e.set_rle)
            {
                return 1;
            }

            if (fseMetadata->ofType == symbolEncodingType_e.set_compressed || fseMetadata->ofType == symbolEncodingType_e.set_rle)
            {
                return 1;
            }

            return 0;
        }

        /** ZSTD_compressSubBlock_multi() :
         *  Breaks super-block into multiple sub-blocks and compresses them.
         *  Entropy will be written to the first block.
         *  The following blocks will use repeat mode to compress.
         *  All sub-blocks are compressed blocks (no raw or rle blocks).
         *  @return : compressed size of the super block (which is multiple ZSTD blocks)
         *            Or 0 if it failed to compress. */
        private static nuint ZSTD_compressSubBlock_multi(seqStore_t* seqStorePtr, ZSTD_compressedBlockState_t* prevCBlock, ZSTD_compressedBlockState_t* nextCBlock, ZSTD_entropyCTablesMetadata_t* entropyMetadata, ZSTD_CCtx_params_s* cctxParams, void* dst, nuint dstCapacity, void* src, nuint srcSize, int bmi2, uint lastBlock, void* workspace, nuint wkspSize)
        {
            seqDef_s* sstart = seqStorePtr->sequencesStart;
            seqDef_s* send = seqStorePtr->sequences;
            seqDef_s* sp = sstart;
            byte* lstart = seqStorePtr->litStart;
            byte* lend = seqStorePtr->lit;
            byte* lp = lstart;
            byte* ip = (byte*)(src);
            byte* iend = ip + srcSize;
            byte* ostart = (byte*)(dst);
            byte* oend = ostart + dstCapacity;
            byte* op = ostart;
            byte* llCodePtr = seqStorePtr->llCode;
            byte* mlCodePtr = seqStorePtr->mlCode;
            byte* ofCodePtr = seqStorePtr->ofCode;
            nuint targetCBlockSize = cctxParams->targetCBlockSize;
            nuint litSize, seqCount;
            int writeLitEntropy = ((entropyMetadata->hufMetadata.hType == symbolEncodingType_e.set_compressed) ? 1 : 0);
            int writeSeqEntropy = 1;
            int lastSequence = 0;

            litSize = 0;
            seqCount = 0;
            do
            {
                nuint cBlockSizeEstimate = 0;

                if (sstart == send)
                {
                    lastSequence = 1;
                }
                else
                {
                    seqDef_s* sequence = sp + seqCount;

                    lastSequence = ((sequence == send - 1) ? 1 : 0);
                    litSize += ZSTD_getSequenceLength(seqStorePtr, sequence).litLength;
                    seqCount++;
                }

                if (lastSequence != 0)
                {
                    assert(lp <= lend);
                    assert(litSize <= (nuint)(lend - lp));
                    litSize = (nuint)(lend - lp);
                }

                cBlockSizeEstimate = ZSTD_estimateSubBlockSize(lp, litSize, ofCodePtr, llCodePtr, mlCodePtr, seqCount, &nextCBlock->entropy, entropyMetadata, workspace, wkspSize, writeLitEntropy, writeSeqEntropy);
                if (cBlockSizeEstimate > targetCBlockSize || lastSequence != 0)
                {
                    int litEntropyWritten = 0;
                    int seqEntropyWritten = 0;
                    nuint decompressedSize = ZSTD_seqDecompressedSize(seqStorePtr, sp, seqCount, litSize, lastSequence);
                    nuint cSize = ZSTD_compressSubBlock(&nextCBlock->entropy, entropyMetadata, sp, seqCount, lp, litSize, llCodePtr, mlCodePtr, ofCodePtr, cctxParams, (void*)op, (nuint)(oend - op), bmi2, writeLitEntropy, writeSeqEntropy, &litEntropyWritten, &seqEntropyWritten, ((lastBlock != 0 && lastSequence != 0) ? 1U : 0U));


                    {
                        nuint err_code = (cSize);

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }

                    if (cSize > 0 && cSize < decompressedSize)
                    {
                        assert(ip + decompressedSize <= iend);
                        ip += decompressedSize;
                        sp += seqCount;
                        lp += litSize;
                        op += cSize;
                        llCodePtr += seqCount;
                        mlCodePtr += seqCount;
                        ofCodePtr += seqCount;
                        litSize = 0;
                        seqCount = 0;
                        if (litEntropyWritten != 0)
                        {
                            writeLitEntropy = 0;
                        }

                        if (seqEntropyWritten != 0)
                        {
                            writeSeqEntropy = 0;
                        }
                    }
                }
            }
            while (lastSequence == 0);

            if (writeLitEntropy != 0)
            {
                memcpy((void*)(&nextCBlock->entropy.huf), (void*)(&prevCBlock->entropy.huf), ((nuint)(sizeof(ZSTD_hufCTables_t))));
            }

            if (writeSeqEntropy != 0 && (ZSTD_needSequenceEntropyTables(&entropyMetadata->fseMetadata)) != 0)
            {
                return 0;
            }

            if (ip < iend)
            {
                nuint cSize = ZSTD_noCompressBlock((void*)op, (nuint)(oend - op), (void*)ip, (nuint)(iend - ip), lastBlock);


                {
                    nuint err_code = (cSize);

                    if ((ERR_isError(err_code)) != 0)
                    {
                        return err_code;
                    }
                }

                assert(cSize != 0);
                op += cSize;
                if (sp < send)
                {
                    seqDef_s* seq;
                    repcodes_s rep;

                    memcpy((void*)(&rep), (void*)(prevCBlock->rep), ((nuint)(sizeof(repcodes_s))));
                    for (seq = sstart; seq < sp; ++seq)
                    {
                        rep = ZSTD_updateRep(rep.rep, seq->offset - 1, ((ZSTD_getSequenceLength(seqStorePtr, seq).litLength == 0) ? 1U : 0U));
                    }

                    memcpy((void*)(nextCBlock->rep), (void*)(&rep), ((nuint)(sizeof(repcodes_s))));
                }
            }

            return (nuint)(op - ostart);
        }

        /* ZSTD_compressSuperBlock() :
         * Used to compress a super block when targetCBlockSize is being used.
         * The given block will be compressed into multiple sub blocks that are around targetCBlockSize. */
        public static nuint ZSTD_compressSuperBlock(ZSTD_CCtx_s* zc, void* dst, nuint dstCapacity, void* src, nuint srcSize, uint lastBlock)
        {
            ZSTD_entropyCTablesMetadata_t entropyMetadata;


            {
                nuint err_code = (ZSTD_buildBlockEntropyStats(&zc->seqStore, &zc->blockState.prevCBlock->entropy, &zc->blockState.nextCBlock->entropy, &zc->appliedParams, &entropyMetadata, (void*)zc->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(4) * (uint)((((35) > (52) ? (35) : (52)) + 2))))));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return ZSTD_compressSubBlock_multi(&zc->seqStore, zc->blockState.prevCBlock, zc->blockState.nextCBlock, &entropyMetadata, &zc->appliedParams, dst, dstCapacity, src, srcSize, zc->bmi2, lastBlock, (void*)zc->entropyWorkspace, ((uint)(((6 << 10) + 256)) + ((nuint)(sizeof(uint)) * (uint)((((35) > (52) ? (35) : (52)) + 2)))));
        }
    }
}
