namespace SharpCompress.Compressors.ZStandard.Unsafe;

/****************************
 *  Streaming
 ****************************/
public unsafe struct ZSTD_inBuffer_s
{
    /// <summary>start of input buffer</summary>
    public void* src;

    /// <summary>size of input buffer</summary>
    public nuint size;

    /// <summary>position where reading stopped. Will be updated. Necessarily 0 &lt;= pos &lt;= size</summary>
    public nuint pos;
}
