namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* ======    Decompression    ====== */
public struct FSE_DTableHeader
{
    public ushort tableLog;
    public ushort fastMode;
}
