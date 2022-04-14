using System;

namespace ZstdSharp.Unsafe
{
    public unsafe partial struct HUF_ReadDTableX1_Workspace
    {
        public fixed uint rankVal[16];

        public fixed uint rankStart[16];

        public fixed uint statsWksp[218];

        public fixed byte symbols[256];

        public fixed byte huffWeight[256];
    }
}
