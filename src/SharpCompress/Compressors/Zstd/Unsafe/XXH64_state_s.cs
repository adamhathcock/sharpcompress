using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct XXH64_state_s
    {
        public ulong total_len;

        public ulong v1;

        public ulong v2;

        public ulong v3;

        public ulong v4;

        /* buffer defined as U64 for alignment */
        public fixed ulong mem64[4];

        public uint memsize;

        /* never read nor write, will be removed in a future version */
        public fixed uint reserved[2];
    }
}
