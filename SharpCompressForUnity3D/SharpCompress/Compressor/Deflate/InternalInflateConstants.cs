namespace SharpCompress.Compressor.Deflate
{
    using System;

    internal static class InternalInflateConstants
    {
        internal static readonly int[] InflateMask = new int[] { 
            0, 1, 3, 7, 15, 0x1f, 0x3f, 0x7f, 0xff, 0x1ff, 0x3ff, 0x7ff, 0xfff, 0x1fff, 0x3fff, 0x7fff, 
            0xffff
         };
    }
}

