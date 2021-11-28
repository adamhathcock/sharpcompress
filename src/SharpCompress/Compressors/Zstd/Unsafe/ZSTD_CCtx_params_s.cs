using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_CCtx_params_s
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

        public ZSTD_literalCompressionMode_e literalCompressionMode;

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

        /* Block splitting */
        public int splitBlocks;

        /* Param for deciding whether to use row-based matchfinder */
        public ZSTD_useRowMatchFinderMode_e useRowMatchFinder;

        /* Always load a dictionary in ext-dict mode (not prefix mode)? */
        public int deterministicRefPrefix;

        /* Internal use, for createCCtxParams() and freeCCtxParams() only */
        public ZSTD_customMem customMem;
    }
}
