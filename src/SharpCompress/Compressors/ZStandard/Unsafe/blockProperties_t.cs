namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct blockProperties_t
{
    public blockType_e blockType;
    public uint lastBlock;
    public uint origSize;
}
