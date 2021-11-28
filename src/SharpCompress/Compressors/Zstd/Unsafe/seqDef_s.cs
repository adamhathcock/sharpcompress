using System;

namespace ZstdSharp.Unsafe
{
    /*-*******************************************
    *  Private declarations
    *********************************************/
    public partial struct seqDef_s
    {
        /* offset == rawOffset + ZSTD_REP_NUM, or equivalently, offCode + 1 */
        public uint offset;

        public ushort litLength;

        public ushort matchLength;
    }
}
