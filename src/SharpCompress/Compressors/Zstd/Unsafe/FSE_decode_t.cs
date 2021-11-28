using System;

namespace ZstdSharp.Unsafe
{
    public partial struct FSE_decode_t
    {
        public ushort newState;

        public byte symbol;

        public byte nbBits;
    }
}
