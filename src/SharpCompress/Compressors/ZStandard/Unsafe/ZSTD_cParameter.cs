namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_cParameter
{
    /// <summary>
    /// Set compression parameters according to pre-defined cLevel table.
    /// Note that exact compression parameters are dynamically determined,
    /// depending on both compression level and srcSize (when known).
    /// Default level is ZSTD_CLEVEL_DEFAULT==3.
    /// Special: value 0 means default, which is controlled by ZSTD_CLEVEL_DEFAULT.
    /// Note 1: it's possible to pass a negative compression level.
    /// Note 2: setting a level does not automatically set all other compression parameters
    ///   to default. Setting this will however eventually dynamically impact the compression
    ///   parameters which have not been manually set. The manually set ones will 'stick'.
    /// </summary>
    ZSTD_c_compressionLevel = 100,

    /// <summary>
    /// Maximum allowed back-reference distance, expressed as power of 2.
    /// This will set a memory budget for streaming decompression,
    /// with larger values requiring more memory and typically compressing more.
    /// Must be clamped between ZSTD_WINDOWLOG_MIN and ZSTD_WINDOWLOG_MAX.
    /// Special: value 0 means "use default windowLog".
    /// </summary>
    ZSTD_c_windowLog = 101,

    /// <summary>
    /// Size of the initial probe table, as a power of 2.
    /// Resulting memory usage is (1 &lt;&lt; (hashLog+2)).
    /// Must be clamped between ZSTD_HASHLOG_MIN and ZSTD_HASHLOG_MAX.
    /// Special: value 0 means "use default hashLog".
    /// </summary>
    ZSTD_c_hashLog = 102,

    /// <summary>
    /// Size of the multi-probe search table, as a power of 2.
    /// Resulting memory usage is (1 &lt;&lt; (chainLog+2)).
    /// Must be clamped between ZSTD_CHAINLOG_MIN and ZSTD_CHAINLOG_MAX.
    /// Special: value 0 means "use default chainLog".
    /// </summary>
    ZSTD_c_chainLog = 103,

    /// <summary>
    /// Number of search attempts, as a power of 2.
    /// More attempts result in better and slower compression.
    /// This parameter is useless for "fast" and "dFast" strategies.
    /// Special: value 0 means "use default searchLog".
    /// </summary>
    ZSTD_c_searchLog = 104,

    /// <summary>
    /// Minimum size of searched matches.
    /// Must be clamped between ZSTD_MINMATCH_MIN and ZSTD_MINMATCH_MAX.
    /// Special: value 0 means "use default minMatchLength".
    /// </summary>
    ZSTD_c_minMatch = 105,

    /// <summary>
    /// Impact of this field depends on strategy.
    /// For strategies btopt, btultra and btultra2: Length of Match considered "good enough" to stop search.
    /// For strategy fast: Distance between match sampling.
    /// Special: value 0 means "use default targetLength".
    /// </summary>
    ZSTD_c_targetLength = 106,

    /// <summary>
    /// See ZSTD_strategy enum definition.
    /// The higher the value of selected strategy, the more complex it is,
    /// resulting in stronger and slower compression.
    /// Special: value 0 means "use default strategy".
    /// </summary>
    ZSTD_c_strategy = 107,

    /// <summary>
    /// Attempts to fit compressed block size into approximately targetCBlockSize.
    /// Bound by ZSTD_TARGETCBLOCKSIZE_MIN and ZSTD_TARGETCBLOCKSIZE_MAX.
    /// Note that it's not a guarantee, just a convergence target (default:0).
    /// </summary>
    ZSTD_c_targetCBlockSize = 130,

    /// <summary>
    /// Enable long distance matching.
    /// This parameter is designed to improve compression ratio for large inputs.
    /// </summary>
    ZSTD_c_enableLongDistanceMatching = 160,

    /// <summary>
    /// Size of the table for long distance matching, as a power of 2.
    /// </summary>
    ZSTD_c_ldmHashLog = 161,

    /// <summary>
    /// Minimum match size for long distance matcher.
    /// </summary>
    ZSTD_c_ldmMinMatch = 162,

    /// <summary>
    /// Log size of each bucket in the LDM hash table for collision resolution.
    /// </summary>
    ZSTD_c_ldmBucketSizeLog = 163,

    /// <summary>
    /// Frequency of inserting/looking up entries into the LDM hash table.
    /// </summary>
    ZSTD_c_ldmHashRateLog = 164,

    /// <summary>
    /// Content size will be written into frame header whenever known (default:1).
    /// </summary>
    ZSTD_c_contentSizeFlag = 200,

    /// <summary>
    /// A 32-bits checksum of content is written at end of frame (default:0).
    /// </summary>
    ZSTD_c_checksumFlag = 201,

    /// <summary>
    /// When applicable, dictionary's ID is written into frame header (default:1).
    /// </summary>
    ZSTD_c_dictIDFlag = 202,

    /// <summary>
    /// Select how many threads will be spawned to compress in parallel.
    /// Default value is 0, aka "single-threaded mode".
    /// </summary>
    ZSTD_c_nbWorkers = 400,

    /// <summary>
    /// Size of a compression job. This value is enforced only when nbWorkers >= 1.
    /// </summary>
    ZSTD_c_jobSize = 401,

    /// <summary>
    /// Control the overlap size, as a fraction of window size.
    /// </summary>
    ZSTD_c_overlapLog = 402,

    ZSTD_c_experimentalParam1 = 500,
    ZSTD_c_experimentalParam2 = 10,
    ZSTD_c_experimentalParam3 = 1000,
    ZSTD_c_experimentalParam4 = 1001,
    ZSTD_c_experimentalParam5 = 1002,
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
