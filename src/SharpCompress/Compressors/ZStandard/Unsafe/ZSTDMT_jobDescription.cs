namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTDMT_jobDescription
{
    /* SHARED - set0 by mtctx, then modified by worker AND read by mtctx */
    public nuint consumed;

    /* SHARED - set0 by mtctx, then modified by worker AND read by mtctx, then set0 by mtctx */
    public nuint cSize;

    /* Thread-safe - used by mtctx and worker */
    public void* job_mutex;

    /* Thread-safe - used by mtctx and worker */
    public void* job_cond;

    /* Thread-safe - used by mtctx and (all) workers */
    public ZSTDMT_CCtxPool* cctxPool;

    /* Thread-safe - used by mtctx and (all) workers */
    public ZSTDMT_bufferPool_s* bufPool;

    /* Thread-safe - used by mtctx and (all) workers */
    public ZSTDMT_bufferPool_s* seqPool;

    /* Thread-safe - used by mtctx and (all) workers */
    public SerialState* serial;

    /* set by worker (or mtctx), then read by worker & mtctx, then modified by mtctx => no barrier */
    public buffer_s dstBuff;

    /* set by mtctx, then read by worker & mtctx => no barrier */
    public Range prefix;

    /* set by mtctx, then read by worker & mtctx => no barrier */
    public Range src;

    /* set by mtctx, then read by worker => no barrier */
    public uint jobID;

    /* set by mtctx, then read by worker => no barrier */
    public uint firstJob;

    /* set by mtctx, then read by worker => no barrier */
    public uint lastJob;

    /* set by mtctx, then read by worker => no barrier */
    public ZSTD_CCtx_params_s @params;

    /* set by mtctx, then read by worker => no barrier */
    public ZSTD_CDict_s* cdict;

    /* set by mtctx, then read by worker => no barrier */
    public ulong fullFrameSize;

    /* used only by mtctx */
    public nuint dstFlushed;

    /* used only by mtctx */
    public uint frameChecksumNeeded;
}
