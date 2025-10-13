namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* Struct containing info needed to make decision about ldm inclusion */
public struct ZSTD_optLdm_t
{
    /* External match candidates store for this block */
    public RawSeqStore_t seqStore;

    /* Start position of the current match candidate */
    public uint startPosInBlock;

    /* End position of the current match candidate */
    public uint endPosInBlock;

    /* Offset of the match candidate */
    public uint offset;
}
