namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_CCtx_params_s
{
    public ZSTD_format_e format;
    public ZSTD_compressionParameters cParams;
    public ZSTD_frameParameters fParams;
    public int compressionLevel;

    /* force back-references to respect limit of
     * 1<<wLog, even for dictionary */
    public int forceWindow;

    /* Tries to fit compressed block size to be around targetCBlockSize.
     * No target when targetCBlockSize == 0.
     * There is no guarantee on compressed block size */
    public nuint targetCBlockSize;

    /* User's best guess of source size.
     * Hint is not valid when srcSizeHint == 0.
     * There is no guarantee that hint is close to actual source size */
    public int srcSizeHint;
    public ZSTD_dictAttachPref_e attachDictPref;
    public ZSTD_paramSwitch_e literalCompressionMode;

    /* Multithreading: used to pass parameters to mtctx */
    public int nbWorkers;
    public nuint jobSize;
    public int overlapLog;
    public int rsyncable;

    /* Long distance matching parameters */
    public ldmParams_t ldmParams;

    /* Dedicated dict search algorithm trigger */
    public int enableDedicatedDictSearch;

    /* Input/output buffer modes */
    public ZSTD_bufferMode_e inBufferMode;
    public ZSTD_bufferMode_e outBufferMode;

    /* Sequence compression API */
    public ZSTD_sequenceFormat_e blockDelimiters;
    public int validateSequences;

    /* Block splitting
     * @postBlockSplitter executes split analysis after sequences are produced,
     * it's more accurate but consumes more resources.
     * @preBlockSplitter_level splits before knowing sequences,
     * it's more approximative but also cheaper.
     * Valid @preBlockSplitter_level values range from 0 to 6 (included).
     * 0 means auto, 1 means do not split,
     * then levels are sorted in increasing cpu budget, from 2 (fastest) to 6 (slowest).
     * Highest @preBlockSplitter_level combines well with @postBlockSplitter.
     */
    public ZSTD_paramSwitch_e postBlockSplitter;
    public int preBlockSplitter_level;

    /* Adjust the max block size*/
    public nuint maxBlockSize;

    /* Param for deciding whether to use row-based matchfinder */
    public ZSTD_paramSwitch_e useRowMatchFinder;

    /* Always load a dictionary in ext-dict mode (not prefix mode)? */
    public int deterministicRefPrefix;

    /* Internal use, for createCCtxParams() and freeCCtxParams() only */
    public ZSTD_customMem customMem;

    /* Controls prefetching in some dictMatchState matchfinders */
    public ZSTD_paramSwitch_e prefetchCDictTables;

    /* Controls whether zstd will fall back to an internal matchfinder
     * if the external matchfinder returns an error code. */
    public int enableMatchFinderFallback;

    /* Parameters for the external sequence producer API.
     * Users set these parameters through ZSTD_registerSequenceProducer().
     * It is not possible to set these parameters individually through the public API. */
    public void* extSeqProdState;
    public void* extSeqProdFunc;

    /* Controls repcode search in external sequence parsing */
    public ZSTD_paramSwitch_e searchForExternalRepcodes;
}
