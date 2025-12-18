namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct HUF_CompressWeightsWksp
{
    public fixed uint CTable[59];
    public fixed uint scratchBuffer[41];
    public fixed uint count[13];
    public fixed short norm[13];
}
