using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ldmRollingHashState_t
    {
        public ulong rolling;

        public ulong stopMask;
    }
}
