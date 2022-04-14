using System;

namespace ZstdSharp.Unsafe
{
    /* static allocation of HUF's Compression Table */
    /* this is a private definition, just exposed for allocation and strict aliasing purpose. never EVER access its members directly */
    public partial struct HUF_CElt_s
    {
        public ushort val;

        public byte nbBits;
    }
}
