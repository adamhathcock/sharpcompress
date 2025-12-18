namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*!
 * @brief Canonical (big endian) representation of @ref XXH64_hash_t.
 */
public unsafe struct XXH64_canonical_t
{
    public fixed byte digest[8];
}
