using System;

namespace ZstdSharp.Unsafe
{
    /**
     * COVER_best_t is used for two purposes:
     * 1. Synchronizing threads.
     * 2. Saving the best parameters and dictionary.
     *
     * All of the methods except COVER_best_init() are thread safe if zstd is
     * compiled with multithreaded support.
     */
    public unsafe partial struct COVER_best_s
    {
        public int mutex;

        public int cond;

        public nuint liveJobs;

        public void* dict;

        public nuint dictSize;

        public ZDICT_cover_params_t parameters;

        public nuint compressedSize;
    }
}
