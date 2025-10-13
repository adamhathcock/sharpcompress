namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*!
 * @internal
 * @brief Enum to indicate whether a pointer is aligned.
 */
public enum XXH_alignment
{
    /*!< Aligned */
    XXH_aligned,

    /*!< Possibly unaligned */
    XXH_unaligned,
}
