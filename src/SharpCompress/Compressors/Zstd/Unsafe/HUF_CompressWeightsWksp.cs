using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct HUF_CompressWeightsWksp
    {
        public fixed uint CTable[59];

        public fixed uint scratchBuffer[30];

        public fixed uint count[13];

        public fixed short norm[13];
    }
}
