namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* *************************/
/* double-symbols decoding */
/* *************************/
public struct HUF_DEltX2
{
    /* double-symbols decoding */
    public ushort sequence;
    public byte nbBits;
    public byte length;
}
