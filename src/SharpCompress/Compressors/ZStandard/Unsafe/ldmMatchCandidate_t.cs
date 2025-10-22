namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ldmMatchCandidate_t
{
    public byte* split;
    public uint hash;
    public uint checksum;
    public ldmEntry_t* bucket;
}
