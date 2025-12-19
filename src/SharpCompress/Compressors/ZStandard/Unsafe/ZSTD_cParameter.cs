namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_cParameter
{
    /* Set compression parameters according to pre-defined cLevel table.
     * Note that exact compression parameters are dynamically determined,
     * depending on both compression level and srcSize (when known).
     * Default level is ZSTD_CLEVEL_DEFAULT==3.
     * Special: value 0 means default, which is controlled by ZSTD_CLEVEL_DEFAULT.
     * Note 1 : it's possible to pass a negative compression level.
     * Note 2 : setting a level does not automatically set all other compression parameters
     *   to default. Setting this will however eventually dynamically impact the compression
     *   parameters which have not been manually set. The manually set
     *   ones will 'stick'. */
    ZSTD_c_compressionLevel = 100,

    /* Maximum allowed back-reference distance, expressed as power of 2.
     * This will set a memory budget for streaming decompression,
     * with larger values requiring more memory
     * and typically compressing more.
     * Must be clamped between ZSTD_WINDOWLOG_MIN and ZSTD_WINDOWLOG_MAX.
     * Special: value 0 means "use default windowLog".
     * Note: Using a windowLog greater than ZSTD_WINDOWLOG_LIMIT_DEFAULT
     *       requires explicitly allowing such size at streaming decompression stage. */
    ZSTD_c_windowLog = 101,

    /* Size of the initial probe table, as a power of 2.
     * Resulting memory usage is (1 << (hashLog+2)).
     * Must be clamped between ZSTD_HASHLOG_MIN and ZSTD_HASHLOG_MAX.
     * Larger tables improve compression ratio of strategies <= dFast,
     * and improve speed of strategies > dFast.
     * Special: value 0 means "use default hashLog". */
    ZSTD_c_hashLog = 102,

    /* Size of the multi-probe search table, as a power of 2.
     * Resulting memory usage is (1 << (chainLog+2)).
     * Must be clamped between ZSTD_CHAINLOG_MIN and ZSTD_CHAINLOG_MAX.
     * Larger tables result in better and slower compression.
     * This parameter is useless for "fast" strategy.
     * It's still useful when using "dfast" strategy,
     * in which case it defines a secondary probe table.
     * Special: value 0 means "use default chainLog". */
    ZSTD_c_chainLog = 103,

    /* Number of search attempts, as a power of 2.
     * More attempts result in better and slower compression.
     * This parameter is useless for "fast" and "dFast" strategies.
     * Special: value 0 means "use default searchLog". */
    ZSTD_c_searchLog = 104,

    /* Minimum size of searched matches.
     * Note that Zstandard can still find matches of smaller size,
     * it just tweaks its search algorithm to look for this size and larger.
     * Larger values increase compression and decompression speed, but decrease ratio.
     * Must be clamped between ZSTD_MINMATCH_MIN and ZSTD_MINMATCH_MAX.
     * Note that currently, for all strategies < btopt, effective minimum is 4.
     *                    , for all strategies > fast, effective maximum is 6.
     * Special: value 0 means "use default minMatchLength". */
    ZSTD_c_minMatch = 105,

    /* Impact of this field depends on strategy.
     * For strategies btopt, btultra & btultra2:
     *     Length of Match considered "good enough" to stop search.
     *     Larger values make compression stronger, and slower.
     * For strategy fast:
     *     Distance between match sampling.
     *     Larger values make compression faster, and weaker.
     * Special: value 0 means "use default targetLength". */
    ZSTD_c_targetLength = 106,

    /* See ZSTD_strategy enum definition.
     * The higher the value of selected strategy, the more complex it is,
     * resulting in stronger and slower compression.
     * Special: value 0 means "use default strategy". */
    ZSTD_c_strategy = 107,

    /* v1.5.6+
     * Attempts to fit compressed block size into approximately targetCBlockSize.
     * Bound by ZSTD_TARGETCBLOCKSIZE_MIN and ZSTD_TARGETCBLOCKSIZE_MAX.
     * Note that it's not a guarantee, just a convergence target (default:0).
     * No target when targetCBlockSize == 0.
     * This is helpful in low bandwidth streaming environments to improve end-to-end latency,
     * when a client can make use of partial documents (a prominent example being Chrome).
     * Note: this parameter is stable since v1.5.6.
     * It was present as an experimental parameter in earlier versions,
     * but it's not recommended using it with earlier library versions
     * due to massive performance regressions.
     */
    ZSTD_c_targetCBlockSize = 130,

    /* Enable long distance matching.
     * This parameter is designed to improve compression ratio
     * for large inputs, by finding large matches at long distance.
     * It increases memory usage and window size.
     * Note: enabling this parameter increases default ZSTD_c_windowLog to 128 MB
     * except when expressly set to a different value.
     * Note: will be enabled by default if ZSTD_c_windowLog >= 128 MB and
     * compression strategy >= ZSTD_btopt (== compression level 16+) */
    ZSTD_c_enableLongDistanceMatching = 160,

    /* Size of the table for long distance matching, as a power of 2.
     * Larger values increase memory usage and compression ratio,
     * but decrease compression speed.
     * Must be clamped between ZSTD_HASHLOG_MIN and ZSTD_HASHLOG_MAX
     * default: windowlog - 7.
     * Special: value 0 means "automatically determine hashlog". */
    ZSTD_c_ldmHashLog = 161,

    /* Minimum match size for long distance matcher.
     * Larger/too small values usually decrease compression ratio.
     * Must be clamped between ZSTD_LDM_MINMATCH_MIN and ZSTD_LDM_MINMATCH_MAX.
     * Special: value 0 means "use default value" (default: 64). */
    ZSTD_c_ldmMinMatch = 162,

    /* Log size of each bucket in the LDM hash table for collision resolution.
     * Larger values improve collision resolution but decrease compression speed.
     * The maximum value is ZSTD_LDM_BUCKETSIZELOG_MAX.
     * Special: value 0 means "use default value" (default: 3). */
    ZSTD_c_ldmBucketSizeLog = 163,

    /* Frequency of inserting/looking up entries into the LDM hash table.
     * Must be clamped between 0 and (ZSTD_WINDOWLOG_MAX - ZSTD_HASHLOG_MIN).
     * Default is MAX(0, (windowLog - ldmHashLog)), optimizing hash table usage.
     * Larger values improve compression speed.
     * Deviating far from default value will likely result in a compression ratio decrease.
     * Special: value 0 means "automatically determine hashRateLog". */
    ZSTD_c_ldmHashRateLog = 164,

    /* Content size will be written into frame header _whenever known_ (default:1)
     * Content size must be known at the beginning of compression.
     * This is automatically the case when using ZSTD_compress2(),
     * For streaming scenarios, content size must be provided with ZSTD_CCtx_setPledgedSrcSize() */
    ZSTD_c_contentSizeFlag = 200,

    /* A 32-bits checksum of content is written at end of frame (default:0) */
    ZSTD_c_checksumFlag = 201,

    /* When applicable, dictionary's ID is written into frame header (default:1) */
    ZSTD_c_dictIDFlag = 202,

    /* Select how many threads will be spawned to compress in parallel.
     * When nbWorkers >= 1, triggers asynchronous mode when invoking ZSTD_compressStream*() :
     * ZSTD_compressStream*() consumes input and flush output if possible, but immediately gives back control to caller,
     * while compression is performed in parallel, within worker thread(s).
     * (note : a strong exception to this rule is when first invocation of ZSTD_compressStream2() sets ZSTD_e_end :
     *  in which case, ZSTD_compressStream2() delegates to ZSTD_compress2(), which is always a blocking call).
     * More workers improve speed, but also increase memory usage.
     * Default value is `0`, aka "single-threaded mode" : no worker is spawned,
     * compression is performed inside Caller's thread, and all invocations are blocking */
    ZSTD_c_nbWorkers = 400,

    /* Size of a compression job. This value is enforced only when nbWorkers >= 1.
     * Each compression job is completed in parallel, so this value can indirectly impact the nb of active threads.
     * 0 means default, which is dynamically determined based on compression parameters.
     * Job size must be a minimum of overlap size, or ZSTDMT_JOBSIZE_MIN (= 512 KB), whichever is largest.
     * The minimum size is automatically and transparently enforced. */
    ZSTD_c_jobSize = 401,

    /* Control the overlap size, as a fraction of window size.
     * The overlap size is an amount of data reloaded from previous job at the beginning of a new job.
     * It helps preserve compression ratio, while each job is compressed in parallel.
     * This value is enforced only when nbWorkers >= 1.
     * Larger values increase compression ratio, but decrease speed.
     * Possible values range from 0 to 9 :
     * - 0 means "default" : value will be determined by the library, depending on strategy
     * - 1 means "no overlap"
     * - 9 means "full overlap", using a full window size.
     * Each intermediate rank increases/decreases load size by a factor 2 :
     * 9: full window;  8: w/2;  7: w/4;  6: w/8;  5:w/16;  4: w/32;  3:w/64;  2:w/128;  1:no overlap;  0:default
     * default value varies between 6 and 9, depending on strategy */
    ZSTD_c_overlapLog = 402,

    /* note : additional experimental parameters are also available
     * within the experimental section of the API.
     * At the time of this writing, they include :
     * ZSTD_c_rsyncable
     * ZSTD_c_format
     * ZSTD_c_forceMaxWindow
     * ZSTD_c_forceAttachDict
     * ZSTD_c_literalCompressionMode
     * ZSTD_c_srcSizeHint
     * ZSTD_c_enableDedicatedDictSearch
     * ZSTD_c_stableInBuffer
     * ZSTD_c_stableOutBuffer
     * ZSTD_c_blockDelimiters
     * ZSTD_c_validateSequences
     * ZSTD_c_blockSplitterLevel
     * ZSTD_c_splitAfterSequences
     * ZSTD_c_useRowMatchFinder
     * ZSTD_c_prefetchCDictTables
     * ZSTD_c_enableSeqProducerFallback
     * ZSTD_c_maxBlockSize
     * Because they are not stable, it's necessary to define ZSTD_STATIC_LINKING_ONLY to access them.
     * note : never ever use experimentalParam? names directly;
     *        also, the enums values themselves are unstable and can still change.
     */
    ZSTD_c_experimentalParam1 = 500,
    ZSTD_c_experimentalParam2 = 10,
    ZSTD_c_experimentalParam3 = 1000,
    ZSTD_c_experimentalParam4 = 1001,
    ZSTD_c_experimentalParam5 = 1002,

    /* was ZSTD_c_experimentalParam6=1003; is now ZSTD_c_targetCBlockSize */
    ZSTD_c_experimentalParam7 = 1004,
    ZSTD_c_experimentalParam8 = 1005,
    ZSTD_c_experimentalParam9 = 1006,
    ZSTD_c_experimentalParam10 = 1007,
    ZSTD_c_experimentalParam11 = 1008,
    ZSTD_c_experimentalParam12 = 1009,
    ZSTD_c_experimentalParam13 = 1010,
    ZSTD_c_experimentalParam14 = 1011,
    ZSTD_c_experimentalParam15 = 1012,
    ZSTD_c_experimentalParam16 = 1013,
    ZSTD_c_experimentalParam17 = 1014,
    ZSTD_c_experimentalParam18 = 1015,
    ZSTD_c_experimentalParam19 = 1016,
    ZSTD_c_experimentalParam20 = 1017,
}
