using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_seqSymbol
    {
        public ushort nextState;

        public byte nbAdditionalBits;

        public byte nbBits;

        public uint baseValue;
    }
}
