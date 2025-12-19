namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* ====   Serial State   ==== */
public unsafe struct Range
{
    public void* start;
    public nuint size;

    public Range(void* start, nuint size)
    {
        this.start = start;
        this.size = size;
    }
}
