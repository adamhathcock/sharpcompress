namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*!
 * @internal
 * @brief Structure for XXH32 streaming API.
 *
 * @note This is only defined when @ref XXH_STATIC_LINKING_ONLY,
 * @ref XXH_INLINE_ALL, or @ref XXH_IMPLEMENTATION is defined. Otherwise it is
 * an opaque type. This allows fields to safely be changed.
 *
 * Typedef'd to @ref XXH32_state_t.
 * Do not access the members of this struct directly.
 * @see XXH64_state_s, XXH3_state_s
 */
public unsafe struct XXH32_state_s
{
    /*!< Total length hashed, modulo 2^32 */
    public uint total_len_32;

    /*!< Whether the hash is >= 16 (handles @ref total_len_32 overflow) */
    public uint large_len;

    /*!< Accumulator lanes */
    public fixed uint v[4];

    /*!< Internal buffer for partial reads. Treated as unsigned char[16]. */
    public fixed uint mem32[4];

    /*!< Amount of data in @ref mem32 */
    public uint memsize;

    /*!< Reserved field. Do not read nor write to it. */
    public uint reserved;
}
