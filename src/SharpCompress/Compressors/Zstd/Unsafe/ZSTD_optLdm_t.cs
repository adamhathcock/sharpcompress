using System;

namespace ZstdSharp.Unsafe
{
    /* Struct containing info needed to make decision about ldm inclusion */
    public partial struct ZSTD_optLdm_t
    {
        /* External match candidates store for this block */
        public rawSeqStore_t seqStore;

        /* Start position of the current match candidate */
        public uint startPosInBlock;

        /* End position of the current match candidate */
        public uint endPosInBlock;

        /* Offset of the match candidate */
        public uint offset;
    }
}
