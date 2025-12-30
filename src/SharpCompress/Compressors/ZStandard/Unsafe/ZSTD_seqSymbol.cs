namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_seqSymbol
{
    public ushort nextState;
    public byte nbAdditionalBits;
    public byte nbBits;
    public uint baseValue;

    public ZSTD_seqSymbol(ushort nextState, byte nbAdditionalBits, byte nbBits, uint baseValue)
    {
        this.nextState = nextState;
        this.nbAdditionalBits = nbAdditionalBits;
        this.nbBits = nbBits;
        this.baseValue = baseValue;
    }
}
