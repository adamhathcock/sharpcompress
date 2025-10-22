namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct FSE_decode_t
{
    public ushort newState;
    public byte symbol;
    public byte nbBits;
}
