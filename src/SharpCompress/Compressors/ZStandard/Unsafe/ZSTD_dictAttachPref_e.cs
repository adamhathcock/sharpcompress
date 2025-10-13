namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictAttachPref_e
{
    /* Use the default heuristic. */
    ZSTD_dictDefaultAttach = 0,

    /* Never copy the dictionary. */
    ZSTD_dictForceAttach = 1,

    /* Always copy the dictionary. */
    ZSTD_dictForceCopy = 2,

    /* Always reload the dictionary */
    ZSTD_dictForceLoad = 3,
}
