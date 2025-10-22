namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct dictItem
{
    public uint pos;
    public uint length;
    public uint savings;
}
