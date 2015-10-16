namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal static class InternalConstants
    {
        internal static readonly int BL_CODES = 0x13;
        internal static readonly int D_CODES = 30;
        internal static readonly int L_CODES = ((LITERALS + 1) + LENGTH_CODES);
        internal static readonly int LENGTH_CODES = 0x1d;
        internal static readonly int LITERALS = 0x100;
        internal static readonly int MAX_BITS = 15;
        internal static readonly int MAX_BL_BITS = 7;
        internal static readonly int REP_3_6 = 0x10;
        internal static readonly int REPZ_11_138 = 0x12;
        internal static readonly int REPZ_3_10 = 0x11;
    }
}

