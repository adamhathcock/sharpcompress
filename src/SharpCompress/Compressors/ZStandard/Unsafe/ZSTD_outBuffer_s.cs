namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_outBuffer_s
{
    /// <summary>start of output buffer</summary>
    public void* dst;

    /// <summary>size of output buffer</summary>
    public nuint size;

    /// <summary>position where writing stopped. Will be updated. Necessarily 0 to size inclusive.</summary>
    public nuint pos;
}
