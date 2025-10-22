namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_window_t
{
    /* next block here to continue on current prefix */
    public byte* nextSrc;

    /* All regular indexes relative to this position */
    public byte* @base;

    /* extDict indexes relative to this position */
    public byte* dictBase;

    /* below that point, need extDict */
    public uint dictLimit;

    /* below that point, no more valid data */
    public uint lowLimit;

    /* Number of times overflow correction has run since
     * ZSTD_window_init(). Useful for debugging coredumps
     * and for ZSTD_WINDOW_OVERFLOW_CORRECT_FREQUENTLY.
     */
    public uint nbOverflowCorrections;
}
