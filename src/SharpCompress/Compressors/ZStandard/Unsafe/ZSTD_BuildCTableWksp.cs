namespace ZstdSharp.Unsafe
{
    public unsafe struct ZSTD_BuildCTableWksp
    {
        public fixed short norm[53];
        public fixed uint wksp[285];
    }
}