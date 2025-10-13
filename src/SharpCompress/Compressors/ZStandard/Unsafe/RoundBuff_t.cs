namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct RoundBuff_t
{
    /* The round input buffer. All jobs get references
     * to pieces of the buffer. ZSTDMT_tryGetInputRange()
     * handles handing out job input buffers, and makes
     * sure it doesn't overlap with any pieces still in use.
     */
    public byte* buffer;

    /* The capacity of buffer. */
    public nuint capacity;

    /* The position of the current inBuff in the round
     * buffer. Updated past the end if the inBuff once
     * the inBuff is sent to the worker thread.
     * pos <= capacity.
     */
    public nuint pos;

    public RoundBuff_t(byte* buffer, nuint capacity, nuint pos)
    {
        this.buffer = buffer;
        this.capacity = capacity;
        this.pos = pos;
    }
}
