using System;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        public static uint* kInverseProbabilityLog256 = GetArrayPointer(new uint[256]
        {
            0,
            2048,
            1792,
            1642,
            1536,
            1453,
            1386,
            1329,
            1280,
            1236,
            1197,
            1162,
            1130,
            1100,
            1073,
            1047,
            1024,
            1001,
            980,
            960,
            941,
            923,
            906,
            889,
            874,
            859,
            844,
            830,
            817,
            804,
            791,
            779,
            768,
            756,
            745,
            734,
            724,
            714,
            704,
            694,
            685,
            676,
            667,
            658,
            650,
            642,
            633,
            626,
            618,
            610,
            603,
            595,
            588,
            581,
            574,
            567,
            561,
            554,
            548,
            542,
            535,
            529,
            523,
            517,
            512,
            506,
            500,
            495,
            489,
            484,
            478,
            473,
            468,
            463,
            458,
            453,
            448,
            443,
            438,
            434,
            429,
            424,
            420,
            415,
            411,
            407,
            402,
            398,
            394,
            390,
            386,
            382,
            377,
            373,
            370,
            366,
            362,
            358,
            354,
            350,
            347,
            343,
            339,
            336,
            332,
            329,
            325,
            322,
            318,
            315,
            311,
            308,
            305,
            302,
            298,
            295,
            292,
            289,
            286,
            282,
            279,
            276,
            273,
            270,
            267,
            264,
            261,
            258,
            256,
            253,
            250,
            247,
            244,
            241,
            239,
            236,
            233,
            230,
            228,
            225,
            222,
            220,
            217,
            215,
            212,
            209,
            207,
            204,
            202,
            199,
            197,
            194,
            192,
            190,
            187,
            185,
            182,
            180,
            178,
            175,
            173,
            171,
            168,
            166,
            164,
            162,
            159,
            157,
            155,
            153,
            151,
            149,
            146,
            144,
            142,
            140,
            138,
            136,
            134,
            132,
            130,
            128,
            126,
            123,
            121,
            119,
            117,
            115,
            114,
            112,
            110,
            108,
            106,
            104,
            102,
            100,
            98,
            96,
            94,
            93,
            91,
            89,
            87,
            85,
            83,
            82,
            80,
            78,
            76,
            74,
            73,
            71,
            69,
            67,
            66,
            64,
            62,
            61,
            59,
            57,
            55,
            54,
            52,
            50,
            49,
            47,
            46,
            44,
            42,
            41,
            39,
            37,
            36,
            34,
            33,
            31,
            30,
            28,
            26,
            25,
            23,
            22,
            20,
            19,
            17,
            16,
            14,
            13,
            11,
            10,
            8,
            7,
            5,
            4,
            2,
            1,
        });

        private static uint ZSTD_getFSEMaxSymbolValue(uint* ctable)
        {
            void* ptr = (void*)ctable;
            ushort* u16ptr = (ushort*)(ptr);
            uint maxSymbolValue = MEM_read16((void*)(u16ptr + 1));

            return maxSymbolValue;
        }

        /**
         * Returns true if we should use ncount=-1 else we should
         * use ncount=1 for low probability symbols instead.
         */
        private static uint ZSTD_useLowProbCount(nuint nbSeq)
        {
            return ((nbSeq >= 2048) ? 1U : 0U);
        }

        /**
         * Returns the cost in bytes of encoding the normalized count header.
         * Returns an error if any of the helper functions return an error.
         */
        private static nuint ZSTD_NCountCost(uint* count, uint max, nuint nbSeq, uint FSELog)
        {
            byte* wksp = stackalloc byte[512];
            short* norm = stackalloc short[53];
            uint tableLog = FSE_optimalTableLog(FSELog, nbSeq, max);


            {
                nuint err_code = (FSE_normalizeCount((short*)norm, tableLog, count, nbSeq, max, ZSTD_useLowProbCount(nbSeq)));

                if ((ERR_isError(err_code)) != 0)
                {
                    return err_code;
                }
            }

            return FSE_writeNCount((void*)wksp, (nuint)(sizeof(byte) * 512), (short*)norm, max, tableLog);
        }

        /**
         * Returns the cost in bits of encoding the distribution described by count
         * using the entropy bound.
         */
        private static nuint ZSTD_entropyCost(uint* count, uint max, nuint total)
        {
            uint cost = 0;
            uint s;

            assert(total > 0);
            for (s = 0; s <= max; ++s)
            {
                uint norm = (uint)((256 * count[s]) / total);

                if (count[s] != 0 && norm == 0)
                {
                    norm = 1;
                }

                assert(count[s] < total);
                cost += count[s] * kInverseProbabilityLog256[norm];
            }

            return cost >> 8;
        }

        /**
         * Returns the cost in bits of encoding the distribution in count using ctable.
         * Returns an error if ctable cannot represent all the symbols in count.
         */
        public static nuint ZSTD_fseBitCost(uint* ctable, uint* count, uint max)
        {
            uint kAccuracyLog = 8;
            nuint cost = 0;
            uint s;
            FSE_CState_t cstate;

            FSE_initCState(&cstate, ctable);
            if (ZSTD_getFSEMaxSymbolValue(ctable) < max)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            for (s = 0; s <= max; ++s)
            {
                uint tableLog = cstate.stateLog;
                uint badCost = (tableLog + 1) << (int)kAccuracyLog;
                uint bitCost = FSE_bitCost(cstate.symbolTT, tableLog, s, kAccuracyLog);

                if (count[s] == 0)
                {
                    continue;
                }

                if (bitCost >= badCost)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                }

                cost += (nuint)(count[s]) * bitCost;
            }

            return cost >> (int)kAccuracyLog;
        }

        /**
         * Returns the cost in bits of encoding the distribution in count using the
         * table described by norm. The max symbol support by norm is assumed >= max.
         * norm must be valid for every symbol with non-zero probability in count.
         */
        public static nuint ZSTD_crossEntropyCost(short* norm, uint accuracyLog, uint* count, uint max)
        {
            uint shift = 8 - accuracyLog;
            nuint cost = 0;
            uint s;

            assert(accuracyLog <= 8);
            for (s = 0; s <= max; ++s)
            {
                uint normAcc = (norm[s] != -1) ? (uint)(norm[s]) : 1;
                uint norm256 = normAcc << (int)shift;

                assert(norm256 > 0);
                assert(norm256 < 256);
                cost += count[s] * kInverseProbabilityLog256[norm256];
            }

            return cost >> 8;
        }

        public static symbolEncodingType_e ZSTD_selectEncodingType(FSE_repeat* repeatMode, uint* count, uint max, nuint mostFrequent, nuint nbSeq, uint FSELog, uint* prevCTable, short* defaultNorm, uint defaultNormLog, ZSTD_defaultPolicy_e isDefaultAllowed, ZSTD_strategy strategy)
        {
            if (mostFrequent == nbSeq)
            {
                *repeatMode = FSE_repeat.FSE_repeat_none;
                if (isDefaultAllowed != default && nbSeq <= 2)
                {
                    return symbolEncodingType_e.set_basic;
                }

                return symbolEncodingType_e.set_rle;
            }

            if (strategy < ZSTD_strategy.ZSTD_lazy)
            {
                if (isDefaultAllowed != default)
                {
                    nuint staticFse_nbSeq_max = 1000;
                    nuint mult = (nuint)(10 - strategy);
                    nuint baseLog = 3;
                    nuint dynamicFse_nbSeq_min = (((nuint)(1) << (int)defaultNormLog) * mult) >> (int)baseLog;

                    assert(defaultNormLog >= 5 && defaultNormLog <= 6);
                    assert(mult <= 9 && mult >= 7);
                    if ((*repeatMode == FSE_repeat.FSE_repeat_valid) && (nbSeq < staticFse_nbSeq_max))
                    {
                        return symbolEncodingType_e.set_repeat;
                    }

                    if ((nbSeq < dynamicFse_nbSeq_min) || (mostFrequent < (nbSeq >> (int)(defaultNormLog - 1))))
                    {
                        *repeatMode = FSE_repeat.FSE_repeat_none;
                        return symbolEncodingType_e.set_basic;
                    }
                }
            }
            else
            {
                nuint basicCost = isDefaultAllowed != default ? ZSTD_crossEntropyCost(defaultNorm, defaultNormLog, count, max) : (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                nuint repeatCost = *repeatMode != FSE_repeat.FSE_repeat_none ? ZSTD_fseBitCost(prevCTable, count, max) : (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                nuint NCountCost = ZSTD_NCountCost(count, max, nbSeq, FSELog);
                nuint compressedCost = (NCountCost << 3) + ZSTD_entropyCost(count, max, nbSeq);

                if (isDefaultAllowed != default)
                {
                    assert((ERR_isError(basicCost)) == 0);
                    assert(!(*repeatMode == FSE_repeat.FSE_repeat_valid && (ERR_isError(repeatCost)) != 0));
                }

                assert((ERR_isError(NCountCost)) == 0);
                assert(compressedCost < (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxCode))));
                if (basicCost <= repeatCost && basicCost <= compressedCost)
                {
                    assert(isDefaultAllowed != default);
                    *repeatMode = FSE_repeat.FSE_repeat_none;
                    return symbolEncodingType_e.set_basic;
                }

                if (repeatCost <= compressedCost)
                {
                    assert((ERR_isError(repeatCost)) == 0);
                    return symbolEncodingType_e.set_repeat;
                }

                assert(compressedCost < basicCost && compressedCost < repeatCost);
            }

            *repeatMode = FSE_repeat.FSE_repeat_check;
            return symbolEncodingType_e.set_compressed;
        }

        public static nuint ZSTD_buildCTable(void* dst, nuint dstCapacity, uint* nextCTable, uint FSELog, symbolEncodingType_e type, uint* count, uint max, byte* codeTable, nuint nbSeq, short* defaultNorm, uint defaultNormLog, uint defaultMax, uint* prevCTable, nuint prevCTableSize, void* entropyWorkspace, nuint entropyWorkspaceSize)
        {
            byte* op = (byte*)(dst);
            byte* oend = op + dstCapacity;

            switch (type)
            {
                case symbolEncodingType_e.set_rle:
                {

                    {
                        nuint err_code = (FSE_buildCTable_rle(nextCTable, (byte)(max)));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }
                }

                if (dstCapacity == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                *op = codeTable[0];
                return 1;
                case symbolEncodingType_e.set_repeat:
                {
                    memcpy((void*)(nextCTable), (void*)(prevCTable), (prevCTableSize));
                }

                return 0;
                case symbolEncodingType_e.set_basic:
                {

                    {
                        nuint err_code = (FSE_buildCTable_wksp(nextCTable, defaultNorm, defaultMax, defaultNormLog, entropyWorkspace, entropyWorkspaceSize));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }
                }

                return 0;
                case symbolEncodingType_e.set_compressed:
                {
                    ZSTD_BuildCTableWksp* wksp = (ZSTD_BuildCTableWksp*)(entropyWorkspace);
                    nuint nbSeq_1 = nbSeq;
                    uint tableLog = FSE_optimalTableLog(FSELog, nbSeq, max);

                    if (count[codeTable[nbSeq - 1]] > 1)
                    {
                        count[codeTable[nbSeq - 1]]--;
                        nbSeq_1--;
                    }

                    assert(nbSeq_1 > 1);
                    assert(entropyWorkspaceSize >= (nuint)(sizeof(ZSTD_BuildCTableWksp)));

                    {
                        nuint err_code = (FSE_normalizeCount((short*)wksp->norm, tableLog, count, nbSeq_1, max, ZSTD_useLowProbCount(nbSeq_1)));

                        if ((ERR_isError(err_code)) != 0)
                        {
                            return err_code;
                        }
                    }


                    {
                        nuint NCountSize = FSE_writeNCount((void*)op, (nuint)(oend - op), (short*)wksp->norm, max, tableLog);


                        {
                            nuint err_code = (NCountSize);

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }


                        {
                            nuint err_code = (FSE_buildCTable_wksp(nextCTable, (short*)wksp->norm, max, tableLog, (void*)wksp->wksp, (nuint)(728)));

                            if ((ERR_isError(err_code)) != 0)
                            {
                                return err_code;
                            }
                        }

                        return NCountSize;
                    }
                }

                default:
                {
                    assert(0 != 0);
                }


                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
                }

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint ZSTD_encodeSequences_body(void* dst, nuint dstCapacity, uint* CTable_MatchLength, byte* mlCodeTable, uint* CTable_OffsetBits, byte* ofCodeTable, uint* CTable_LitLength, byte* llCodeTable, seqDef_s* sequences, nuint nbSeq, int longOffsets)
        {
            BIT_CStream_t blockStream;
            FSE_CState_t stateMatchLength;
            FSE_CState_t stateOffsetBits;
            FSE_CState_t stateLitLength;

            if ((ERR_isError(BIT_initCStream(&blockStream, dst, dstCapacity))) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
            }

            FSE_initCState2(&stateMatchLength, CTable_MatchLength, mlCodeTable[nbSeq - 1]);
            FSE_initCState2(&stateOffsetBits, CTable_OffsetBits, ofCodeTable[nbSeq - 1]);
            FSE_initCState2(&stateLitLength, CTable_LitLength, llCodeTable[nbSeq - 1]);
            BIT_addBits(&blockStream, sequences[nbSeq - 1].litLength, LL_bits[llCodeTable[nbSeq - 1]]);
            if (MEM_32bits)
            {
                BIT_flushBits(&blockStream);
            }

            BIT_addBits(&blockStream, sequences[nbSeq - 1].matchLength, ML_bits[mlCodeTable[nbSeq - 1]]);
            if (MEM_32bits)
            {
                BIT_flushBits(&blockStream);
            }

            if (longOffsets != 0)
            {
                uint ofBits = ofCodeTable[nbSeq - 1];
                uint extraBits = ofBits - ((ofBits) < (((uint)(MEM_32bits ? 25 : 57)) - 1) ? (ofBits) : (((uint)(MEM_32bits ? 25 : 57)) - 1));

                if (extraBits != 0)
                {
                    BIT_addBits(&blockStream, sequences[nbSeq - 1].offset, extraBits);
                    BIT_flushBits(&blockStream);
                }

                BIT_addBits(&blockStream, sequences[nbSeq - 1].offset >> (int)extraBits, ofBits - extraBits);
            }
            else
            {
                BIT_addBits(&blockStream, sequences[nbSeq - 1].offset, ofCodeTable[nbSeq - 1]);
            }

            BIT_flushBits(&blockStream);

            {
                nuint n;

                for (n = nbSeq - 2; n < nbSeq; n--)
                {
                    byte llCode = llCodeTable[n];
                    byte ofCode = ofCodeTable[n];
                    byte mlCode = mlCodeTable[n];
                    uint llBits = LL_bits[llCode];
                    uint ofBits = ofCode;
                    uint mlBits = ML_bits[mlCode];

                    FSE_encodeSymbol(&blockStream, &stateOffsetBits, ofCode);
                    FSE_encodeSymbol(&blockStream, &stateMatchLength, mlCode);
                    if (MEM_32bits)
                    {
                        BIT_flushBits(&blockStream);
                    }

                    FSE_encodeSymbol(&blockStream, &stateLitLength, llCode);
                    if (MEM_32bits || (ofBits + mlBits + llBits >= (uint)(64 - 7 - (9 + 9 + 8))))
                    {
                        BIT_flushBits(&blockStream);
                    }

                    BIT_addBits(&blockStream, sequences[n].litLength, llBits);
                    if (MEM_32bits && ((llBits + mlBits) > 24))
                    {
                        BIT_flushBits(&blockStream);
                    }

                    BIT_addBits(&blockStream, sequences[n].matchLength, mlBits);
                    if (MEM_32bits || (ofBits + mlBits + llBits > 56))
                    {
                        BIT_flushBits(&blockStream);
                    }

                    if (longOffsets != 0)
                    {
                        uint extraBits = ofBits - ((ofBits) < (((uint)(MEM_32bits ? 25 : 57)) - 1) ? (ofBits) : (((uint)(MEM_32bits ? 25 : 57)) - 1));

                        if (extraBits != 0)
                        {
                            BIT_addBits(&blockStream, sequences[n].offset, extraBits);
                            BIT_flushBits(&blockStream);
                        }

                        BIT_addBits(&blockStream, sequences[n].offset >> (int)extraBits, ofBits - extraBits);
                    }
                    else
                    {
                        BIT_addBits(&blockStream, sequences[n].offset, ofBits);
                    }

                    BIT_flushBits(&blockStream);
                }
            }

            FSE_flushCState(&blockStream, &stateMatchLength);
            FSE_flushCState(&blockStream, &stateOffsetBits);
            FSE_flushCState(&blockStream, &stateLitLength);

            {
                nuint streamSize = BIT_closeCStream(&blockStream);

                if (streamSize == 0)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall)));
                }

                return streamSize;
            }
        }

        private static nuint ZSTD_encodeSequences_default(void* dst, nuint dstCapacity, uint* CTable_MatchLength, byte* mlCodeTable, uint* CTable_OffsetBits, byte* ofCodeTable, uint* CTable_LitLength, byte* llCodeTable, seqDef_s* sequences, nuint nbSeq, int longOffsets)
        {
            return ZSTD_encodeSequences_body(dst, dstCapacity, CTable_MatchLength, mlCodeTable, CTable_OffsetBits, ofCodeTable, CTable_LitLength, llCodeTable, sequences, nbSeq, longOffsets);
        }

        private static nuint ZSTD_encodeSequences_bmi2(void* dst, nuint dstCapacity, uint* CTable_MatchLength, byte* mlCodeTable, uint* CTable_OffsetBits, byte* ofCodeTable, uint* CTable_LitLength, byte* llCodeTable, seqDef_s* sequences, nuint nbSeq, int longOffsets)
        {
            return ZSTD_encodeSequences_body(dst, dstCapacity, CTable_MatchLength, mlCodeTable, CTable_OffsetBits, ofCodeTable, CTable_LitLength, llCodeTable, sequences, nbSeq, longOffsets);
        }

        public static nuint ZSTD_encodeSequences(void* dst, nuint dstCapacity, uint* CTable_MatchLength, byte* mlCodeTable, uint* CTable_OffsetBits, byte* ofCodeTable, uint* CTable_LitLength, byte* llCodeTable, seqDef_s* sequences, nuint nbSeq, int longOffsets, int bmi2)
        {
            if (bmi2 != 0)
            {
                return ZSTD_encodeSequences_bmi2(dst, dstCapacity, CTable_MatchLength, mlCodeTable, CTable_OffsetBits, ofCodeTable, CTable_LitLength, llCodeTable, sequences, nbSeq, longOffsets);
            }

            return ZSTD_encodeSequences_default(dst, dstCapacity, CTable_MatchLength, mlCodeTable, CTable_OffsetBits, ofCodeTable, CTable_LitLength, llCodeTable, sequences, nbSeq, longOffsets);
        }
    }
}
