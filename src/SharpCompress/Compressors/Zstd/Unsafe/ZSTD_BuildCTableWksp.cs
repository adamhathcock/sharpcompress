using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct ZSTD_BuildCTableWksp
    {
        public fixed short norm[53];

        public fixed uint wksp[182];
    }
}
