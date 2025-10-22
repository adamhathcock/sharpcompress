namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_CCtx_s
{
    public ZSTD_compressionStage_e stage;

    /* == 1 if cParams(except wlog) or compression level are changed in requestedParams. Triggers transmission of new params to ZSTDMT (if available) then reset to 0. */
    public int cParamsChanged;

    /* == 1 if the CPU supports BMI2 and 0 otherwise. CPU support is determined dynamically once per context lifetime. */
    public int bmi2;
    public ZSTD_CCtx_params_s requestedParams;
    public ZSTD_CCtx_params_s appliedParams;

    /* Param storage used by the simple API - not sticky. Must only be used in top-level simple API functions for storage. */
    public ZSTD_CCtx_params_s simpleApiParams;
    public uint dictID;
    public nuint dictContentSize;

    /* manages buffer for dynamic allocations */
    public ZSTD_cwksp workspace;
    public nuint blockSizeMax;

    /* this way, 0 (default) == unknown */
    public ulong pledgedSrcSizePlusOne;
    public ulong consumedSrcSize;
    public ulong producedCSize;
    public XXH64_state_s xxhState;
    public ZSTD_customMem customMem;
    public void* pool;
    public nuint staticSize;
    public SeqCollector seqCollector;
    public int isFirstBlock;
    public int initialized;

    /* sequences storage ptrs */
    public SeqStore_t seqStore;

    /* long distance matching state */
    public ldmState_t ldmState;

    /* Storage for the ldm output sequences */
    public rawSeq* ldmSequences;
    public nuint maxNbLdmSequences;

    /* Mutable reference to external sequences */
    public RawSeqStore_t externSeqStore;
    public ZSTD_blockState_t blockState;

    /* used as substitute of stack space - must be aligned for S64 type */
    public void* tmpWorkspace;
    public nuint tmpWkspSize;

    /* Whether we are streaming or not */
    public ZSTD_buffered_policy_e bufferedPolicy;

    /* streaming */
    public sbyte* inBuff;
    public nuint inBuffSize;
    public nuint inToCompress;
    public nuint inBuffPos;
    public nuint inBuffTarget;
    public sbyte* outBuff;
    public nuint outBuffSize;
    public nuint outBuffContentSize;
    public nuint outBuffFlushedSize;
    public ZSTD_cStreamStage streamStage;
    public uint frameEnded;

    /* Stable in/out buffer verification */
    public ZSTD_inBuffer_s expectedInBuffer;

    /* nb bytes within stable input buffer that are said to be consumed but are not */
    public nuint stableIn_notConsumed;
    public nuint expectedOutBufferSize;

    /* Dictionary */
    public ZSTD_localDict localDict;
    public ZSTD_CDict_s* cdict;

    /* single-usage dictionary */
    public ZSTD_prefixDict_s prefixDict;
    public ZSTDMT_CCtx_s* mtctx;

    /* Workspace for block splitter */
    public ZSTD_blockSplitCtx blockSplitCtx;

    /* Buffer for output from external sequence producer */
    public ZSTD_Sequence* extSeqBuf;
    public nuint extSeqBufCapacity;
}
