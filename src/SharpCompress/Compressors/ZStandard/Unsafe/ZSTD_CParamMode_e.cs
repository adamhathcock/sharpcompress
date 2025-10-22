namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_CParamMode_e
{
    /* Compression with ZSTD_noDict or ZSTD_extDict.
     * In this mode we use both the srcSize and the dictSize
     * when selecting and adjusting parameters.
     */
    ZSTD_cpm_noAttachDict = 0,

    /* Compression with ZSTD_dictMatchState or ZSTD_dedicatedDictSearch.
     * In this mode we only take the srcSize into account when selecting
     * and adjusting parameters.
     */
    ZSTD_cpm_attachDict = 1,

    /* Creating a CDict.
     * In this mode we take both the source size and the dictionary size
     * into account when selecting and adjusting the parameters.
     */
    ZSTD_cpm_createCDict = 2,

    /* ZSTD_getCParams, ZSTD_getParams, ZSTD_adjustParams.
     * We don't know what these parameters are for. We default to the legacy
     * behavior of taking both the source size and the dict size into account
     * when selecting and adjusting parameters.
     */
    ZSTD_cpm_unknown = 3,
}
