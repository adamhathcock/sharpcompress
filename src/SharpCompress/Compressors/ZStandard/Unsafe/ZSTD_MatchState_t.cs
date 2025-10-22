namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_MatchState_t
{
    /* State for window round buffer management */
    public ZSTD_window_t window;

    /* index of end of dictionary, within context's referential.
     * When loadedDictEnd != 0, a dictionary is in use, and still valid.
     * This relies on a mechanism to set loadedDictEnd=0 when dictionary is no longer within distance.
     * Such mechanism is provided within ZSTD_window_enforceMaxDist() and ZSTD_checkDictValidity().
     * When dict referential is copied into active context (i.e. not attached),
     * loadedDictEnd == dictSize, since referential starts from zero.
     */
    public uint loadedDictEnd;

    /* index from which to continue table update */
    public uint nextToUpdate;

    /* dispatch table for matches of len==3 : larger == faster, more memory */
    public uint hashLog3;

    /* For row-based matchfinder: Hashlog based on nb of rows in the hashTable.*/
    public uint rowHashLog;

    /* For row-based matchFinder: A row-based table containing the hashes and head index. */
    public byte* tagTable;

    /* For row-based matchFinder: a cache of hashes to improve speed */
    public fixed uint hashCache[8];

    /* For row-based matchFinder: salts the hash for reuse of tag table */
    public ulong hashSalt;

    /* For row-based matchFinder: collects entropy for salt generation */
    public uint hashSaltEntropy;
    public uint* hashTable;
    public uint* hashTable3;
    public uint* chainTable;

    /* Non-zero if we should force non-contiguous load for the next window update. */
    public int forceNonContiguous;

    /* Indicates whether this matchState is using the
     * dedicated dictionary search structure.
     */
    public int dedicatedDictSearch;

    /* optimal parser state */
    public optState_t opt;
    public ZSTD_MatchState_t* dictMatchState;
    public ZSTD_compressionParameters cParams;
    public RawSeqStore_t* ldmSeqStore;

    /* Controls prefetching in some dictMatchState matchfinders.
     * This behavior is controlled from the cctx ms.
     * This parameter has no effect in the cdict ms. */
    public int prefetchCDictTables;

    /* When == 0, lazy match finders insert every position.
     * When != 0, lazy match finders only insert positions they search.
     * This allows them to skip much faster over incompressible data,
     * at a small cost to compression ratio.
     */
    public int lazySkipping;
}
