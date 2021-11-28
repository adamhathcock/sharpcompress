using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct HUF_WriteCTableWksp
    {
        public HUF_CompressWeightsWksp wksp;

        /* precomputed conversion table */
        public fixed byte bitsToWeight[13];

        public fixed byte huffWeight[255];
    }
}
