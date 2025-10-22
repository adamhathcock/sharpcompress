namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct HUF_ReadDTableX1_Workspace
{
    public fixed uint rankVal[13];
    public fixed uint rankStart[13];
    public fixed uint statsWksp[219];
    public fixed byte symbols[256];
    public fixed byte huffWeight[256];
}
