using System;
using static ZstdSharp.UnsafeHelper;

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        /* --- Error management --- */
        public static uint HIST_isError(nuint code)
        {
            return ERR_isError(code);
        }

        /*-**************************************************************
         *  Histogram functions
         ****************************************************************/
        public static uint HIST_count_simple(uint* count, uint* maxSymbolValuePtr, void* src, nuint srcSize)
        {
            byte* ip = (byte*)(src);
            byte* end = ip + srcSize;
            uint maxSymbolValue = *maxSymbolValuePtr;
            uint largestCount = 0;

            memset((void*)(count), (0), ((maxSymbolValue + 1) * (nuint)(sizeof(uint))));
            if (srcSize == 0)
            {
                *maxSymbolValuePtr = 0;
                return 0;
            }

            while (ip < end)
            {
                assert(*ip <= maxSymbolValue);
                count[*ip++]++;
            }

            while ((count[maxSymbolValue]) == 0)
            {
                maxSymbolValue--;
            }

            *maxSymbolValuePtr = maxSymbolValue;

            {
                uint s;

                for (s = 0; s <= maxSymbolValue; s++)
                {
                    if (count[s] > largestCount)
                    {
                        largestCount = count[s];
                    }
                }
            }

            return largestCount;
        }

        /* HIST_count_parallel_wksp() :
         * store histogram into 4 intermediate tables, recombined at the end.
         * this design makes better use of OoO cpus,
         * and is noticeably faster when some values are heavily repeated.
         * But it needs some additional workspace for intermediate tables.
         * `workSpace` must be a U32 table of size >= HIST_WKSP_SIZE_U32.
         * @return : largest histogram frequency,
         *           or an error code (notably when histogram's alphabet is larger than *maxSymbolValuePtr) */
        private static nuint HIST_count_parallel_wksp(uint* count, uint* maxSymbolValuePtr, void* source, nuint sourceSize, HIST_checkInput_e check, uint* workSpace)
        {
            byte* ip = (byte*)(source);
            byte* iend = ip + sourceSize;
            nuint countSize = (*maxSymbolValuePtr + 1) * (nuint)(4);
            uint max = 0;
            uint* Counting1 = workSpace;
            uint* Counting2 = Counting1 + 256;
            uint* Counting3 = Counting2 + 256;
            uint* Counting4 = Counting3 + 256;

            assert(*maxSymbolValuePtr <= 255);
            if (sourceSize == 0)
            {
                memset((void*)(count), (0), (countSize));
                *maxSymbolValuePtr = 0;
                return 0;
            }

            memset((void*)(workSpace), (0), ((uint)(4 * 256) * (nuint)(sizeof(uint))));

            {
                uint cached = MEM_read32((void*)ip);

                ip += 4;
                while (ip < iend - 15)
                {
                    uint c = cached;

                    cached = MEM_read32((void*)ip);
                    ip += 4;
                    Counting1[(byte)(c)]++;
                    Counting2[(byte)(c >> 8)]++;
                    Counting3[(byte)(c >> 16)]++;
                    Counting4[c >> 24]++;
                    c = cached;
                    cached = MEM_read32((void*)ip);
                    ip += 4;
                    Counting1[(byte)(c)]++;
                    Counting2[(byte)(c >> 8)]++;
                    Counting3[(byte)(c >> 16)]++;
                    Counting4[c >> 24]++;
                    c = cached;
                    cached = MEM_read32((void*)ip);
                    ip += 4;
                    Counting1[(byte)(c)]++;
                    Counting2[(byte)(c >> 8)]++;
                    Counting3[(byte)(c >> 16)]++;
                    Counting4[c >> 24]++;
                    c = cached;
                    cached = MEM_read32((void*)ip);
                    ip += 4;
                    Counting1[(byte)(c)]++;
                    Counting2[(byte)(c >> 8)]++;
                    Counting3[(byte)(c >> 16)]++;
                    Counting4[c >> 24]++;
                }

                ip -= 4;
            }

            while (ip < iend)
            {
                Counting1[*ip++]++;
            }


            {
                uint s;

                for (s = 0; s < 256; s++)
                {
                    Counting1[s] += Counting2[s] + Counting3[s] + Counting4[s];
                    if (Counting1[s] > max)
                    {
                        max = Counting1[s];
                    }
                }
            }


            {
                uint maxSymbolValue = 255;

                while ((Counting1[maxSymbolValue]) == 0)
                {
                    maxSymbolValue--;
                }

                if (check != default && maxSymbolValue > *maxSymbolValuePtr)
                {
                    return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_maxSymbolValue_tooSmall)));
                }

                *maxSymbolValuePtr = maxSymbolValue;
                memmove((void*)(count), (void*)(Counting1), (countSize));
            }

            return (nuint)(max);
        }

        /* HIST_countFast_wksp() :
         * Same as HIST_countFast(), but using an externally provided scratch buffer.
         * `workSpace` is a writable buffer which must be 4-bytes aligned,
         * `workSpaceSize` must be >= HIST_WKSP_SIZE
         */
        public static nuint HIST_countFast_wksp(uint* count, uint* maxSymbolValuePtr, void* source, nuint sourceSize, void* workSpace, nuint workSpaceSize)
        {
            if (sourceSize < 1500)
            {
                return HIST_count_simple(count, maxSymbolValuePtr, source, sourceSize);
            }

            if (((nuint)(workSpace) & 3) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (workSpaceSize < (1024 * (nuint)(sizeof(uint))))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall)));
            }

            return HIST_count_parallel_wksp(count, maxSymbolValuePtr, source, sourceSize, HIST_checkInput_e.trustInput, (uint*)(workSpace));
        }

        /* HIST_count_wksp() :
         * Same as HIST_count(), but using an externally provided scratch buffer.
         * `workSpace` size must be table of >= HIST_WKSP_SIZE_U32 unsigned */
        public static nuint HIST_count_wksp(uint* count, uint* maxSymbolValuePtr, void* source, nuint sourceSize, void* workSpace, nuint workSpaceSize)
        {
            if (((nuint)(workSpace) & 3) != 0)
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_GENERIC)));
            }

            if (workSpaceSize < (1024 * (nuint)(sizeof(uint))))
            {
                return (unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_workSpace_tooSmall)));
            }

            if (*maxSymbolValuePtr < 255)
            {
                return HIST_count_parallel_wksp(count, maxSymbolValuePtr, source, sourceSize, HIST_checkInput_e.checkMaxSymbolValue, (uint*)(workSpace));
            }

            *maxSymbolValuePtr = 255;
            return HIST_countFast_wksp(count, maxSymbolValuePtr, source, sourceSize, workSpace, workSpaceSize);
        }

        /* fast variant (unsafe : won't check if src contains values beyond count[] limit) */
        public static nuint HIST_countFast(uint* count, uint* maxSymbolValuePtr, void* source, nuint sourceSize)
        {
            uint* tmpCounters = stackalloc uint[1024];

            return HIST_countFast_wksp(count, maxSymbolValuePtr, source, sourceSize, (void*)tmpCounters, (nuint)(sizeof(uint) * 1024));
        }

        /*! HIST_count():
         *  Provides the precise count of each byte within a table 'count'.
         * 'count' is a table of unsigned int, of minimum size (*maxSymbolValuePtr+1).
         *  Updates *maxSymbolValuePtr with actual largest symbol value detected.
         * @return : count of the most frequent symbol (which isn't identified).
         *           or an error code, which can be tested using HIST_isError().
         *           note : if return == srcSize, there is only one symbol.
         */
        public static nuint HIST_count(uint* count, uint* maxSymbolValuePtr, void* src, nuint srcSize)
        {
            uint* tmpCounters = stackalloc uint[1024];

            return HIST_count_wksp(count, maxSymbolValuePtr, src, srcSize, (void*)tmpCounters, (nuint)(sizeof(uint) * 1024));
        }
    }
}
