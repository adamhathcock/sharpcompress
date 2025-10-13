namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*************************************
 * Context
 ***************************************/
public unsafe struct FASTCOVER_ctx_t
{
    public byte* samples;
    public nuint* offsets;
    public nuint* samplesSizes;
    public nuint nbSamples;
    public nuint nbTrainSamples;
    public nuint nbTestSamples;
    public nuint nbDmers;
    public uint* freqs;
    public uint d;
    public uint f;
    public FASTCOVER_accel_t accelParams;
}
