namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_outBuffer_s
{
    /**< start of output buffer */
    public void* dst;

    /**< size of output buffer */
    public nuint size;

    /**< position where writing stopped. Will be updated. Necessarily 0 <= pos <= size */
    public nuint pos;
}
