namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct SyncPoint
{
    /* The number of bytes to load from the input. */
    public nuint toLoad;

    /* Boolean declaring if we must flush because we found a synchronization point. */
    public int flush;
}
