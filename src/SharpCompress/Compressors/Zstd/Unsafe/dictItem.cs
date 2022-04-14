using System;

namespace ZstdSharp.Unsafe
{
    public partial struct dictItem
    {
        public uint pos;

        public uint length;

        public uint savings;
    }
}
