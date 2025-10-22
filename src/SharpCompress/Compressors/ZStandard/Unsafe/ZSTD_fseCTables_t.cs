namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_fseCTables_t
{
    public fixed uint offcodeCTable[193];
    public fixed uint matchlengthCTable[363];
    public fixed uint litlengthCTable[329];
    public FSE_repeat offcode_repeatMode;
    public FSE_repeat matchlength_repeatMode;
    public FSE_repeat litlength_repeatMode;
}
