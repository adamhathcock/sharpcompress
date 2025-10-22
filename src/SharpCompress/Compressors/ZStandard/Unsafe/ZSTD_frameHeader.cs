namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_frameHeader
{
    /* if == ZSTD_CONTENTSIZE_UNKNOWN, it means this field is not available. 0 means "empty" */
    public ulong frameContentSize;

    /* can be very large, up to <= frameContentSize */
    public ulong windowSize;
    public uint blockSizeMax;

    /* if == ZSTD_skippableFrame, frameContentSize is the size of skippable content */
    public ZSTD_frameType_e frameType;
    public uint headerSize;

    /* for ZSTD_skippableFrame, contains the skippable magic variant [0-15] */
    public uint dictID;
    public uint checksumFlag;
    public uint _reserved1;
    public uint _reserved2;
}
