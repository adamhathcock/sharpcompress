namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_customMem
{
    public void* customAlloc;
    public void* customFree;
    public void* opaque;

    public ZSTD_customMem(void* customAlloc, void* customFree, void* opaque)
    {
        this.customAlloc = customAlloc;
        this.customFree = customFree;
        this.opaque = opaque;
    }
}
