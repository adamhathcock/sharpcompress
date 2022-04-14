using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using static ZstdSharp.UnsafeHelper;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace ZstdSharp.Unsafe
{
    public static unsafe partial class Methods
    {
        private static void XXH_memcpy(void* dest, void* src, ulong size)
        {
            memcpy((dest), (src), (size));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint XXH_readLE32(void* ptr) => 
            BitConverter.IsLittleEndian ? *(uint*) ptr : BinaryPrimitives.ReverseEndianness(*(uint*) ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong XXH_readLE64(void* ptr) => 
            BitConverter.IsLittleEndian ? *(ulong*) ptr : BinaryPrimitives.ReverseEndianness(*(ulong*) ptr);

        public const ulong PRIME64_1 = 11400714785074694791UL;

        public const ulong PRIME64_2 = 14029467366897019727UL;

        public const ulong PRIME64_3 = 1609587929392839161UL;

        public const ulong PRIME64_4 = 9650029242287828579UL;

        public const ulong PRIME64_5 = 2870177450012600261UL;

        public static uint XXH_versionNumber()
        {
            return 0 * 100 * 100 + 6 * 100 + 2;
        }

        private static ulong XXH64_round(ulong acc, ulong input)
        {
            acc += input * PRIME64_2;
            acc = BitOperations.RotateLeft(acc, 31);
            acc *= PRIME64_1;
            return acc;
        }

        private static ulong XXH64_mergeRound(ulong acc, ulong val)
        {
            val = XXH64_round(0, val);
            acc ^= val;
            acc = acc * PRIME64_1 + PRIME64_4;
            return acc;
        }

        public static void XXH64_reset(XXH64_state_s* statePtr, ulong seed)
        {
            XXH64_state_s state;

            memset(&state, (0), ((ulong)((sizeof(XXH64_state_s) - 8))));
            state.v1 = seed + PRIME64_1 + PRIME64_2;
            state.v2 = seed + PRIME64_2;
            state.v3 = seed + 0;
            state.v4 = seed - PRIME64_1;
            memcpy(statePtr, &state, ((ulong)sizeof(XXH64_state_s)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void XXH64_update(XXH64_state_s* state, void* input, ulong len)
        {
            byte* p = (byte*)(input);
            byte* bEnd = p + len;

            state->total_len += len;
            if (state->memsize + len < 32)
            {
                if (input != null)
                {
                    XXH_memcpy(((byte*)(state->mem64)) + state->memsize, input, len);
                }

                state->memsize += (uint)(len);
                return;
            }

            if (state->memsize != 0)
            {
                XXH_memcpy(((byte*)(state->mem64)) + state->memsize, input, 32 - state->memsize);
                state->v1 = XXH64_round(state->v1, XXH_readLE64(state->mem64 + 0));
                state->v2 = XXH64_round(state->v2, XXH_readLE64(state->mem64 + 1));
                state->v3 = XXH64_round(state->v3, XXH_readLE64(state->mem64 + 2));
                state->v4 = XXH64_round(state->v4, XXH_readLE64(state->mem64 + 3));
                p += 32 - state->memsize;
                state->memsize = 0;
            }

            if (p + 32 <= bEnd)
            {
                byte* limit = bEnd - 32;
                ulong v1 = state->v1;
                ulong v2 = state->v2;
                ulong v3 = state->v3;
                ulong v4 = state->v4;

                do
                {
                    v1 = XXH64_round(v1, XXH_readLE64(p));
                    p += 8;
                    v2 = XXH64_round(v2, XXH_readLE64(p));
                    p += 8;
                    v3 = XXH64_round(v3, XXH_readLE64(p));
                    p += 8;
                    v4 = XXH64_round(v4, XXH_readLE64(p));
                    p += 8;
                }
                while (p <= limit);

                state->v1 = v1;
                state->v2 = v2;
                state->v3 = v3;
                state->v4 = v4;
            }

            if (p < bEnd)
            {
                XXH_memcpy(state->mem64, p, (ulong)(bEnd - p));
                state->memsize = (uint)(bEnd - p);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong XXH64_digest(XXH64_state_s* state)
        {
            byte* p = (byte*)(state->mem64);
            byte* bEnd = (byte*)(state->mem64) + state->memsize;
            ulong h64;

            if (state->total_len >= 32)
            {
                ulong v1 = state->v1;
                ulong v2 = state->v2;
                ulong v3 = state->v3;
                ulong v4 = state->v4;

                h64 = BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) + BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
                h64 = XXH64_mergeRound(h64, v1);
                h64 = XXH64_mergeRound(h64, v2);
                h64 = XXH64_mergeRound(h64, v3);
                h64 = XXH64_mergeRound(h64, v4);
            }
            else
            {
                h64 = state->v3 + PRIME64_5;
            }

            h64 += state->total_len;
            while (p + 8 <= bEnd)
            {
                ulong k1 = XXH64_round(0, XXH_readLE64(p));

                h64 ^= k1;
                h64 = BitOperations.RotateLeft(h64, 27) * PRIME64_1 + PRIME64_4;
                p += 8;
            }

            if (p + 4 <= bEnd)
            {
                h64 ^= XXH_readLE32(p) * PRIME64_1;
                h64 = BitOperations.RotateLeft(h64, 23) * PRIME64_2 + PRIME64_3;
                p += 4;
            }

            while (p < bEnd)
            {
                h64 ^= *p * PRIME64_5;
                h64 = BitOperations.RotateLeft(h64, 11) * PRIME64_1;
                p++;
            }

            h64 ^= h64>>33;
            h64 *= PRIME64_2;
            h64 ^= h64>>29;
            h64 *= PRIME64_3;
            h64 ^= h64>>32;
            return h64;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong XXH64(void* input, ulong len, ulong seed)
        {
            byte* p = (byte*)(input);
            byte* bEnd = p + len;
            ulong h64;

            if (len >= ((ulong)(32)))
            {
                byte* limit = bEnd - 32;
                ulong v1 = seed + PRIME64_1 + PRIME64_2;
                ulong v2 = seed + PRIME64_2;
                ulong v3 = seed + ((ulong)(0));
                ulong v4 = seed - PRIME64_1;

                do
                {
                    v1 = XXH64_round(v1, XXH_readLE64(((void*)(p))));
                    p += 8;
                    v2 = XXH64_round(v2, XXH_readLE64(((void*)(p))));
                    p += 8;
                    v3 = XXH64_round(v3, XXH_readLE64(((void*)(p))));
                    p += 8;
                    v4 = XXH64_round(v4, XXH_readLE64(((void*)(p))));
                    p += 8;
                }
                while (p <= limit);

                h64 = BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) + BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
                h64 = XXH64_mergeRound(h64, v1);
                h64 = XXH64_mergeRound(h64, v2);
                h64 = XXH64_mergeRound(h64, v3);
                h64 = XXH64_mergeRound(h64, v4);
            }
            else
            {
                h64 = seed + PRIME64_5;
            }

            h64 += (ulong)(len);
            while (p + 8 <= bEnd)
            {
                ulong k1 = XXH64_round(((ulong)(0)), XXH_readLE64(((void*)(p))));

                h64 ^= k1;
                h64 = BitOperations.RotateLeft(h64, 27) * PRIME64_1 + PRIME64_4;
                p += 8;
            }

            if (p + 4 <= bEnd)
            {
                h64 ^= (ulong)(XXH_readLE32(((void*)(p)))) * PRIME64_1;
                h64 = BitOperations.RotateLeft(h64, 23) * PRIME64_2 + PRIME64_3;
                p += 4;
            }

            while (p < bEnd)
            {
                h64 ^= ((ulong)((*p))) * PRIME64_5;
                h64 = BitOperations.RotateLeft(h64, 11) * PRIME64_1;
                p++;
            }

            h64 ^= h64 >> 33;
            h64 *= PRIME64_2;
            h64 ^= h64 >> 29;
            h64 *= PRIME64_3;
            h64 ^= h64 >> 32;
            return h64;
        }
    }
}
