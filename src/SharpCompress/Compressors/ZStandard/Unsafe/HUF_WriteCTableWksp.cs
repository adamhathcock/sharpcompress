namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct HUF_WriteCTableWksp
{
    public HUF_CompressWeightsWksp wksp;

    /* precomputed conversion table */
    public fixed byte bitsToWeight[13];
    public fixed byte huffWeight[255];
}
