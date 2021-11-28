using System;

namespace ZstdSharp.Unsafe
{
    /* These definitions are only meant to allow allocation of XXH state
       statically, on stack, or in a struct for example.
       Do not use members directly. */
    public unsafe partial struct XXH32_state_s
    {
        public uint total_len_32;

        public uint large_len;

        public uint v1;

        public uint v2;

        public uint v3;

        public uint v4;

        /* buffer defined as U32 for alignment */
        public fixed uint mem32[4];

        public uint memsize;

        /* never read nor write, will be removed in a future version */
        public uint reserved;
    }
}
