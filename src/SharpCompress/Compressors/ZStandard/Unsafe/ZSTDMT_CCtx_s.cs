namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTDMT_CCtx_s
{
    public void* factory;
    public ZSTDMT_jobDescription* jobs;
    public ZSTDMT_bufferPool_s* bufPool;
    public ZSTDMT_CCtxPool* cctxPool;
    public ZSTDMT_bufferPool_s* seqPool;
    public ZSTD_CCtx_params_s @params;
    public nuint targetSectionSize;
    public nuint targetPrefixSize;

    /* 1 => one job is already prepared, but pool has shortage of workers. Don't create a new job. */
    public int jobReady;
    public InBuff_t inBuff;
    public RoundBuff_t roundBuff;
    public SerialState serial;
    public RSyncState_t rsync;
    public uint jobIDMask;
    public uint doneJobID;
    public uint nextJobID;
    public uint frameEnded;
    public uint allJobsCompleted;
    public ulong frameContentSize;
    public ulong consumed;
    public ulong produced;
    public ZSTD_customMem cMem;
    public ZSTD_CDict_s* cdictLocal;
    public ZSTD_CDict_s* cdict;
    public uint providedFactory;
}
