using System;

namespace ZstdSharp.Unsafe
{
    public partial struct blockProperties_t
    {
        public blockType_e blockType;

        public uint lastBlock;

        public uint origSize;
    }
}
