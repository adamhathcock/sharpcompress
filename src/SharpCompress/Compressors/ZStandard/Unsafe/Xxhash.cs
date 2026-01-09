using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /*!
     * @internal
     * @brief Modify this function to use a different routine than malloc().
     */
    private static void* XXH_malloc(nuint s)
    {
        return malloc(s);
    }

    /*!
     * @internal
     * @brief Modify this function to use a different routine than free().
     */
    private static void XXH_free(void* p)
    {
        free(p);
    }

    /*!
     * @internal
     * @brief Modify this function to use a different routine than memcpy().
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void XXH_memcpy(void* dest, void* src, nuint size)
    {
        memcpy(dest, src, (uint)size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint XXH_readLE32(void* ptr)
    {
        return BitConverter.IsLittleEndian
            ? MEM_read32(ptr)
            : BinaryPrimitives.ReverseEndianness(MEM_read32(ptr));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint XXH_readBE32(void* ptr)
    {
        return BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReverseEndianness(MEM_read32(ptr))
            : MEM_read32(ptr);
    }

    private static uint XXH_readLE32_align(void* ptr, XXH_alignment align)
    {
        if (align == XXH_alignment.XXH_unaligned)
        {
            return XXH_readLE32(ptr);
        }
        else
        {
            return BitConverter.IsLittleEndian
                ? *(uint*)ptr
                : BinaryPrimitives.ReverseEndianness(*(uint*)ptr);
        }
    }

    /* *************************************
     *  Misc
     ***************************************/
    /*! @ingroup public */
    private static uint ZSTD_XXH_versionNumber()
    {
        return 0 * 100 * 100 + 8 * 100 + 2;
    }

    /*!
     * @internal
     * @brief Normal stripe processing routine.
     *
     * This shuffles the bits so that any bit from @p input impacts several bits in
     * @p acc.
     *
     * @param acc The accumulator lane.
     * @param input The stripe of input to mix.
     * @return The mixed accumulator lane.
     */
    private static uint XXH32_round(uint acc, uint input)
    {
        acc += input * 0x85EBCA77U;
        acc = BitOperations.RotateLeft(acc, 13);
        acc *= 0x9E3779B1U;
        return acc;
    }

    /*!
     * @internal
     * @brief Mixes all bits to finalize the hash.
     *
     * The final mix ensures that all input bits have a chance to impact any bit in
     * the output digest, resulting in an unbiased distribution.
     *
     * @param hash The hash to avalanche.
     * @return The avalanched hash.
     */
    private static uint XXH32_avalanche(uint hash)
    {
        hash ^= hash >> 15;
        hash *= 0x85EBCA77U;
        hash ^= hash >> 13;
        hash *= 0xC2B2AE3DU;
        hash ^= hash >> 16;
        return hash;
    }

    /*!
     * @internal
     * @brief Processes the last 0-15 bytes of @p ptr.
     *
     * There may be up to 15 bytes remaining to consume from the input.
     * This final stage will digest them to ensure that all input bytes are present
     * in the final mix.
     *
     * @param hash The hash to finalize.
     * @param ptr The pointer to the remaining input.
     * @param len The remaining length, modulo 16.
     * @param align Whether @p ptr is aligned.
     * @return The finalized hash.
     * @see XXH64_finalize().
     */
    private static uint XXH32_finalize(uint hash, byte* ptr, nuint len, XXH_alignment align)
    {
        len &= 15;
        while (len >= 4)
        {
            {
                hash += XXH_readLE32_align(ptr, align) * 0xC2B2AE3DU;
                ptr += 4;
                hash = BitOperations.RotateLeft(hash, 17) * 0x27D4EB2FU;
            }

            len -= 4;
        }

        while (len > 0)
        {
            {
                hash += *ptr++ * 0x165667B1U;
                hash = BitOperations.RotateLeft(hash, 11) * 0x9E3779B1U;
            }

            --len;
        }

        return XXH32_avalanche(hash);
    }

    /*!
     * @internal
     * @brief The implementation for @ref XXH32().
     *
     * @param input , len , seed Directly passed from @ref XXH32().
     * @param align Whether @p input is aligned.
     * @return The calculated hash.
     */
    private static uint XXH32_endian_align(byte* input, nuint len, uint seed, XXH_alignment align)
    {
        uint h32;
        if (len >= 16)
        {
            byte* bEnd = input + len;
            byte* limit = bEnd - 15;
            uint v1 = seed + 0x9E3779B1U + 0x85EBCA77U;
            uint v2 = seed + 0x85EBCA77U;
            uint v3 = seed + 0;
            uint v4 = seed - 0x9E3779B1U;
            do
            {
                v1 = XXH32_round(v1, XXH_readLE32_align(input, align));
                input += 4;
                v2 = XXH32_round(v2, XXH_readLE32_align(input, align));
                input += 4;
                v3 = XXH32_round(v3, XXH_readLE32_align(input, align));
                input += 4;
                v4 = XXH32_round(v4, XXH_readLE32_align(input, align));
                input += 4;
            } while (input < limit);
            h32 =
                BitOperations.RotateLeft(v1, 1)
                + BitOperations.RotateLeft(v2, 7)
                + BitOperations.RotateLeft(v3, 12)
                + BitOperations.RotateLeft(v4, 18);
        }
        else
        {
            h32 = seed + 0x165667B1U;
        }

        h32 += (uint)len;
        return XXH32_finalize(h32, input, len & 15, align);
    }

    /*! @ingroup XXH32_family */
    private static uint ZSTD_XXH32(void* input, nuint len, uint seed)
    {
        return XXH32_endian_align((byte*)input, len, seed, XXH_alignment.XXH_unaligned);
    }

    /*! @ingroup XXH32_family */
    private static XXH32_state_s* ZSTD_XXH32_createState()
    {
        return (XXH32_state_s*)XXH_malloc((nuint)sizeof(XXH32_state_s));
    }

    /*! @ingroup XXH32_family */
    private static XXH_errorcode ZSTD_XXH32_freeState(XXH32_state_s* statePtr)
    {
        XXH_free(statePtr);
        return XXH_errorcode.XXH_OK;
    }

    /*! @ingroup XXH32_family */
    private static void ZSTD_XXH32_copyState(XXH32_state_s* dstState, XXH32_state_s* srcState)
    {
        XXH_memcpy(dstState, srcState, (nuint)sizeof(XXH32_state_s));
    }

    /*! @ingroup XXH32_family */
    private static XXH_errorcode ZSTD_XXH32_reset(XXH32_state_s* statePtr, uint seed)
    {
        *statePtr = new XXH32_state_s();
        statePtr->v[0] = seed + 0x9E3779B1U + 0x85EBCA77U;
        statePtr->v[1] = seed + 0x85EBCA77U;
        statePtr->v[2] = seed + 0;
        statePtr->v[3] = seed - 0x9E3779B1U;
        return XXH_errorcode.XXH_OK;
    }

    /*! @ingroup XXH32_family */
    private static XXH_errorcode ZSTD_XXH32_update(XXH32_state_s* state, void* input, nuint len)
    {
        if (input == null)
        {
            return XXH_errorcode.XXH_OK;
        }

        {
            byte* p = (byte*)input;
            byte* bEnd = p + len;
            state->total_len_32 += (uint)len;
            state->large_len |= len >= 16 || state->total_len_32 >= 16 ? 1U : 0U;
            if (state->memsize + len < 16)
            {
                XXH_memcpy((byte*)state->mem32 + state->memsize, input, len);
                state->memsize += (uint)len;
                return XXH_errorcode.XXH_OK;
            }

            if (state->memsize != 0)
            {
                XXH_memcpy((byte*)state->mem32 + state->memsize, input, 16 - state->memsize);
                {
                    uint* p32 = state->mem32;
                    state->v[0] = XXH32_round(state->v[0], XXH_readLE32(p32));
                    p32++;
                    state->v[1] = XXH32_round(state->v[1], XXH_readLE32(p32));
                    p32++;
                    state->v[2] = XXH32_round(state->v[2], XXH_readLE32(p32));
                    p32++;
                    state->v[3] = XXH32_round(state->v[3], XXH_readLE32(p32));
                }

                p += 16 - state->memsize;
                state->memsize = 0;
            }

            if (p <= bEnd - 16)
            {
                byte* limit = bEnd - 16;
                do
                {
                    state->v[0] = XXH32_round(state->v[0], XXH_readLE32(p));
                    p += 4;
                    state->v[1] = XXH32_round(state->v[1], XXH_readLE32(p));
                    p += 4;
                    state->v[2] = XXH32_round(state->v[2], XXH_readLE32(p));
                    p += 4;
                    state->v[3] = XXH32_round(state->v[3], XXH_readLE32(p));
                    p += 4;
                } while (p <= limit);
            }

            if (p < bEnd)
            {
                XXH_memcpy(state->mem32, p, (nuint)(bEnd - p));
                state->memsize = (uint)(bEnd - p);
            }
        }

        return XXH_errorcode.XXH_OK;
    }

    /*! @ingroup XXH32_family */
    private static uint ZSTD_XXH32_digest(XXH32_state_s* state)
    {
        uint h32;
        if (state->large_len != 0)
        {
            h32 =
                BitOperations.RotateLeft(state->v[0], 1)
                + BitOperations.RotateLeft(state->v[1], 7)
                + BitOperations.RotateLeft(state->v[2], 12)
                + BitOperations.RotateLeft(state->v[3], 18);
        }
        else
        {
            h32 = state->v[2] + 0x165667B1U;
        }

        h32 += state->total_len_32;
        return XXH32_finalize(h32, (byte*)state->mem32, state->memsize, XXH_alignment.XXH_aligned);
    }

    /*! @ingroup XXH32_family */
    private static void ZSTD_XXH32_canonicalFromHash(XXH32_canonical_t* dst, uint hash)
    {
        assert(sizeof(XXH32_canonical_t) == sizeof(uint));
        if (BitConverter.IsLittleEndian)
            hash = BinaryPrimitives.ReverseEndianness(hash);
        XXH_memcpy(dst, &hash, (nuint)sizeof(XXH32_canonical_t));
    }

    /*! @ingroup XXH32_family */
    private static uint ZSTD_XXH32_hashFromCanonical(XXH32_canonical_t* src)
    {
        return XXH_readBE32(src);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong XXH_readLE64(void* ptr)
    {
        return BitConverter.IsLittleEndian
            ? MEM_read64(ptr)
            : BinaryPrimitives.ReverseEndianness(MEM_read64(ptr));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong XXH_readBE64(void* ptr)
    {
        return BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReverseEndianness(MEM_read64(ptr))
            : MEM_read64(ptr);
    }

    private static ulong XXH_readLE64_align(void* ptr, XXH_alignment align)
    {
        if (align == XXH_alignment.XXH_unaligned)
            return XXH_readLE64(ptr);
        else
            return BitConverter.IsLittleEndian
                ? *(ulong*)ptr
                : BinaryPrimitives.ReverseEndianness(*(ulong*)ptr);
    }

    /*! @copydoc XXH32_round */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong XXH64_round(ulong acc, ulong input)
    {
        acc += input * 0xC2B2AE3D27D4EB4FUL;
        acc = BitOperations.RotateLeft(acc, 31);
        acc *= 0x9E3779B185EBCA87UL;
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong XXH64_mergeRound(ulong acc, ulong val)
    {
        val = XXH64_round(0, val);
        acc ^= val;
        acc = acc * 0x9E3779B185EBCA87UL + 0x85EBCA77C2B2AE63UL;
        return acc;
    }

    /*! @copydoc XXH32_avalanche */
    private static ulong XXH64_avalanche(ulong hash)
    {
        hash ^= hash >> 33;
        hash *= 0xC2B2AE3D27D4EB4FUL;
        hash ^= hash >> 29;
        hash *= 0x165667B19E3779F9UL;
        hash ^= hash >> 32;
        return hash;
    }

    /*!
     * @internal
     * @brief Processes the last 0-31 bytes of @p ptr.
     *
     * There may be up to 31 bytes remaining to consume from the input.
     * This final stage will digest them to ensure that all input bytes are present
     * in the final mix.
     *
     * @param hash The hash to finalize.
     * @param ptr The pointer to the remaining input.
     * @param len The remaining length, modulo 32.
     * @param align Whether @p ptr is aligned.
     * @return The finalized hash
     * @see XXH32_finalize().
     */
    private static ulong XXH64_finalize(ulong hash, byte* ptr, nuint len, XXH_alignment align)
    {
        len &= 31;
        while (len >= 8)
        {
            ulong k1 = XXH64_round(0, XXH_readLE64_align(ptr, align));
            ptr += 8;
            hash ^= k1;
            hash = BitOperations.RotateLeft(hash, 27) * 0x9E3779B185EBCA87UL + 0x85EBCA77C2B2AE63UL;
            len -= 8;
        }

        if (len >= 4)
        {
            hash ^= XXH_readLE32_align(ptr, align) * 0x9E3779B185EBCA87UL;
            ptr += 4;
            hash = BitOperations.RotateLeft(hash, 23) * 0xC2B2AE3D27D4EB4FUL + 0x165667B19E3779F9UL;
            len -= 4;
        }

        while (len > 0)
        {
            hash ^= *ptr++ * 0x27D4EB2F165667C5UL;
            hash = BitOperations.RotateLeft(hash, 11) * 0x9E3779B185EBCA87UL;
            --len;
        }

        return XXH64_avalanche(hash);
    }

    /*!
     * @internal
     * @brief The implementation for @ref XXH64().
     *
     * @param input , len , seed Directly passed from @ref XXH64().
     * @param align Whether @p input is aligned.
     * @return The calculated hash.
     */
    private static ulong XXH64_endian_align(byte* input, nuint len, ulong seed, XXH_alignment align)
    {
        ulong h64;
        if (len >= 32)
        {
            byte* bEnd = input + len;
            byte* limit = bEnd - 31;
            ulong v1 = seed + 0x9E3779B185EBCA87UL + 0xC2B2AE3D27D4EB4FUL;
            ulong v2 = seed + 0xC2B2AE3D27D4EB4FUL;
            ulong v3 = seed + 0;
            ulong v4 = seed - 0x9E3779B185EBCA87UL;
            do
            {
                v1 = XXH64_round(v1, XXH_readLE64_align(input, align));
                input += 8;
                v2 = XXH64_round(v2, XXH_readLE64_align(input, align));
                input += 8;
                v3 = XXH64_round(v3, XXH_readLE64_align(input, align));
                input += 8;
                v4 = XXH64_round(v4, XXH_readLE64_align(input, align));
                input += 8;
            } while (input < limit);
            h64 =
                BitOperations.RotateLeft(v1, 1)
                + BitOperations.RotateLeft(v2, 7)
                + BitOperations.RotateLeft(v3, 12)
                + BitOperations.RotateLeft(v4, 18);
            h64 = XXH64_mergeRound(h64, v1);
            h64 = XXH64_mergeRound(h64, v2);
            h64 = XXH64_mergeRound(h64, v3);
            h64 = XXH64_mergeRound(h64, v4);
        }
        else
        {
            h64 = seed + 0x27D4EB2F165667C5UL;
        }

        h64 += len;
        return XXH64_finalize(h64, input, len, align);
    }

    /*! @ingroup XXH64_family */
    private static ulong ZSTD_XXH64(void* input, nuint len, ulong seed)
    {
        return XXH64_endian_align((byte*)input, len, seed, XXH_alignment.XXH_unaligned);
    }

    /*! @ingroup XXH64_family*/
    private static XXH64_state_s* ZSTD_XXH64_createState()
    {
        return (XXH64_state_s*)XXH_malloc((nuint)sizeof(XXH64_state_s));
    }

    /*! @ingroup XXH64_family */
    private static XXH_errorcode ZSTD_XXH64_freeState(XXH64_state_s* statePtr)
    {
        XXH_free(statePtr);
        return XXH_errorcode.XXH_OK;
    }

    /*! @ingroup XXH64_family */
    private static void ZSTD_XXH64_copyState(XXH64_state_s* dstState, XXH64_state_s* srcState)
    {
        XXH_memcpy(dstState, srcState, (nuint)sizeof(XXH64_state_s));
    }

    /*! @ingroup XXH64_family */
    private static XXH_errorcode ZSTD_XXH64_reset(XXH64_state_s* statePtr, ulong seed)
    {
        *statePtr = new XXH64_state_s();
        statePtr->v[0] = seed + 0x9E3779B185EBCA87UL + 0xC2B2AE3D27D4EB4FUL;
        statePtr->v[1] = seed + 0xC2B2AE3D27D4EB4FUL;
        statePtr->v[2] = seed + 0;
        statePtr->v[3] = seed - 0x9E3779B185EBCA87UL;
        return XXH_errorcode.XXH_OK;
    }

    /*! @ingroup XXH64_family */
    private static XXH_errorcode ZSTD_XXH64_update(XXH64_state_s* state, void* input, nuint len)
    {
        if (input == null)
        {
            return XXH_errorcode.XXH_OK;
        }

        {
            byte* p = (byte*)input;
            byte* bEnd = p + len;
            state->total_len += len;
            if (state->memsize + len < 32)
            {
                XXH_memcpy((byte*)state->mem64 + state->memsize, input, len);
                state->memsize += (uint)len;
                return XXH_errorcode.XXH_OK;
            }

            if (state->memsize != 0)
            {
                XXH_memcpy((byte*)state->mem64 + state->memsize, input, 32 - state->memsize);
                state->v[0] = XXH64_round(state->v[0], XXH_readLE64(state->mem64 + 0));
                state->v[1] = XXH64_round(state->v[1], XXH_readLE64(state->mem64 + 1));
                state->v[2] = XXH64_round(state->v[2], XXH_readLE64(state->mem64 + 2));
                state->v[3] = XXH64_round(state->v[3], XXH_readLE64(state->mem64 + 3));
                p += 32 - state->memsize;
                state->memsize = 0;
            }

            if (p + 32 <= bEnd)
            {
                byte* limit = bEnd - 32;
                do
                {
                    state->v[0] = XXH64_round(state->v[0], XXH_readLE64(p));
                    p += 8;
                    state->v[1] = XXH64_round(state->v[1], XXH_readLE64(p));
                    p += 8;
                    state->v[2] = XXH64_round(state->v[2], XXH_readLE64(p));
                    p += 8;
                    state->v[3] = XXH64_round(state->v[3], XXH_readLE64(p));
                    p += 8;
                } while (p <= limit);
            }

            if (p < bEnd)
            {
                XXH_memcpy(state->mem64, p, (nuint)(bEnd - p));
                state->memsize = (uint)(bEnd - p);
            }
        }

        return XXH_errorcode.XXH_OK;
    }

    /*! @ingroup XXH64_family */
    private static ulong ZSTD_XXH64_digest(XXH64_state_s* state)
    {
        ulong h64;
        if (state->total_len >= 32)
        {
            h64 =
                BitOperations.RotateLeft(state->v[0], 1)
                + BitOperations.RotateLeft(state->v[1], 7)
                + BitOperations.RotateLeft(state->v[2], 12)
                + BitOperations.RotateLeft(state->v[3], 18);
            h64 = XXH64_mergeRound(h64, state->v[0]);
            h64 = XXH64_mergeRound(h64, state->v[1]);
            h64 = XXH64_mergeRound(h64, state->v[2]);
            h64 = XXH64_mergeRound(h64, state->v[3]);
        }
        else
        {
            h64 = state->v[2] + 0x27D4EB2F165667C5UL;
        }

        h64 += state->total_len;
        return XXH64_finalize(
            h64,
            (byte*)state->mem64,
            (nuint)state->total_len,
            XXH_alignment.XXH_aligned
        );
    }

    /*! @ingroup XXH64_family */
    private static void ZSTD_XXH64_canonicalFromHash(XXH64_canonical_t* dst, ulong hash)
    {
        assert(sizeof(XXH64_canonical_t) == sizeof(ulong));
        if (BitConverter.IsLittleEndian)
            hash = BinaryPrimitives.ReverseEndianness(hash);
        XXH_memcpy(dst, &hash, (nuint)sizeof(XXH64_canonical_t));
    }

    /*! @ingroup XXH64_family */
    private static ulong ZSTD_XXH64_hashFromCanonical(XXH64_canonical_t* src)
    {
        return XXH_readBE64(src);
    }
}
