using System;

namespace ZstdSharp.Unsafe
{
    public enum BIT_DStream_status
    {
        BIT_DStream_unfinished = 0,
        BIT_DStream_endOfBuffer = 1,
        BIT_DStream_completed = 2,
        BIT_DStream_overflow = 3,
    }
}
