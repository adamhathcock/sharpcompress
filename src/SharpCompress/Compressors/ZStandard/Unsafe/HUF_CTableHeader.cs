namespace ZstdSharp.Unsafe
{
    public unsafe struct HUF_CTableHeader
    {
        public byte tableLog;
        public byte maxSymbolValue;
        public fixed byte unused[6];
    }
}