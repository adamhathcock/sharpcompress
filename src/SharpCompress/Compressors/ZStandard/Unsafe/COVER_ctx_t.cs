namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*************************************
 * Context
 ***************************************/
public unsafe struct COVER_ctx_t
{
    public byte* samples;
    public nuint* offsets;
    public nuint* samplesSizes;
    public nuint nbSamples;
    public nuint nbTrainSamples;
    public nuint nbTestSamples;
    public uint* suffix;
    public nuint suffixSize;
    public uint* freqs;
    public uint* dmerAt;
    public uint d;
}
