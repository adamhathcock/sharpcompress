namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*!
 * @internal
 * @brief Structure for XXH64 streaming API.
 *
 * @note This is only defined when @ref XXH_STATIC_LINKING_ONLY,
 * @ref XXH_INLINE_ALL, or @ref XXH_IMPLEMENTATION is defined. Otherwise it is
 * an opaque type. This allows fields to safely be changed.
 *
 * Typedef'd to @ref XXH64_state_t.
 * Do not access the members of this struct directly.
 * @see XXH32_state_s, XXH3_state_s
 */
public unsafe struct XXH64_state_s
{
    /*!< Total length hashed. This is always 64-bit. */
    public ulong total_len;

    /*!< Accumulator lanes */
    public fixed ulong v[4];

    /*!< Internal buffer for partial reads. Treated as unsigned char[32]. */
    public fixed ulong mem64[4];

    /*!< Amount of data in @ref mem64 */
    public uint memsize;

    /*!< Reserved field, needed for padding anyways*/
    public uint reserved32;

    /*!< Reserved field. Do not read or write to it. */
    public ulong reserved64;
}
