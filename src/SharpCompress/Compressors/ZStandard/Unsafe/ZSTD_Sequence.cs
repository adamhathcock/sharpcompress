namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_Sequence
{
    /* The offset of the match. (NOT the same as the offset code)
     * If offset == 0 and matchLength == 0, this sequence represents the last
     * literals in the block of litLength size.
     */
    public uint offset;

    /* Literal length of the sequence. */
    public uint litLength;

    /* Match length of the sequence. */
    public uint matchLength;

    /* Represents which repeat offset is represented by the field 'offset'.
     * Ranges from [0, 3].
     *
     * Repeat offsets are essentially previous offsets from previous sequences sorted in
     * recency order. For more detail, see doc/zstd_compression_format.md
     *
     * If rep == 0, then 'offset' does not contain a repeat offset.
     * If rep > 0:
     *  If litLength != 0:
     *      rep == 1 --> offset == repeat_offset_1
     *      rep == 2 --> offset == repeat_offset_2
     *      rep == 3 --> offset == repeat_offset_3
     *  If litLength == 0:
     *      rep == 1 --> offset == repeat_offset_2
     *      rep == 2 --> offset == repeat_offset_3
     *      rep == 3 --> offset == repeat_offset_1 - 1
     *
     * Note: This field is optional. ZSTD_generateSequences() will calculate the value of
     * 'rep', but repeat offsets do not necessarily need to be calculated from an external
     * sequence provider perspective. For example, ZSTD_compressSequences() does not
     * use this 'rep' field at all (as of now).
     */
    public uint rep;
}
