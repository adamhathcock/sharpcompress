namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* =====   CCtx Pool   ===== */
/* a single CCtx Pool can be invoked from multiple threads in parallel */
public unsafe struct ZSTDMT_CCtxPool
{
    public void* poolMutex;
    public int totalCCtx;
    public int availCCtx;
    public ZSTD_customMem cMem;
    public ZSTD_CCtx_s** cctxs;
}
