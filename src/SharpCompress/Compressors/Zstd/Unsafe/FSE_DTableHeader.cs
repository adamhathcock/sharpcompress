using System;

namespace ZstdSharp.Unsafe
{
    /* ======    Decompression    ====== */
    public partial struct FSE_DTableHeader
    {
        public ushort tableLog;

        public ushort fastMode;
    }
}
