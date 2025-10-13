namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct RSyncState_t
{
    public ulong hash;
    public ulong hitMask;
    public ulong primePower;
}
