namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*!
 * @brief Canonical (big endian) representation of @ref XXH32_hash_t.
 */
public unsafe struct XXH32_canonical_t
{
    /*!< Hash bytes, big endian */
    public fixed byte digest[4];
}
