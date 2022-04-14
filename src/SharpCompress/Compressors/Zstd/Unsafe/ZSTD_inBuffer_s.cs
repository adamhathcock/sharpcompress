using System;

namespace ZstdSharp.Unsafe
{
    /****************************
    *  Streaming
    ****************************/
    public unsafe partial struct ZSTD_inBuffer_s
    {
        /**< start of input buffer */
        public void* src;

        /**< size of input buffer */
        public nuint size;

        /**< position where reading stopped. Will be updated. Necessarily 0 <= pos <= size */
        public nuint pos;
    }
}
