namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* =====   Buffer Pool   ===== */
/* a single Buffer Pool can be invoked from multiple threads in parallel */
public unsafe struct buffer_s
{
    public void* start;
    public nuint capacity;

    public buffer_s(void* start, nuint capacity)
    {
        this.start = start;
        this.capacity = capacity;
    }
}
