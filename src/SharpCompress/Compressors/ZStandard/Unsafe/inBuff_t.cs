namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* ------------------------------------------ */
/* =====   Multi-threaded compression   ===== */
/* ------------------------------------------ */
public struct InBuff_t
{
    /* read-only non-owned prefix buffer */
    public Range prefix;
    public buffer_s buffer;
    public nuint filled;
}
