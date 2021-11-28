using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct ZSTD_matchState_t
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
        public ushort* tagTable;

        /* For row-based matchFinder: a cache of hashes to improve speed */
        public fixed uint hashCache[8];

        public uint* hashTable;

        public uint* hashTable3;

        public uint* chainTable;

        /* Non-zero if we should force non-contiguous load for the next window update. */
        public uint forceNonContiguous;

        /* Indicates whether this matchState is using the
                                       * dedicated dictionary search structure.
                                       */
        public int dedicatedDictSearch;

        /* optimal parser state */
        public optState_t opt;

        public ZSTD_matchState_t* dictMatchState;

        public ZSTD_compressionParameters cParams;

        public rawSeqStore_t* ldmSeqStore;
    }
}
