namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct HUF_CStream_t
{
    public _bitContainer_e__FixedBuffer bitContainer;
    public _bitPos_e__FixedBuffer bitPos;
    public byte* startPtr;
    public byte* ptr;
    public byte* endPtr;

    public unsafe struct _bitContainer_e__FixedBuffer
    {
        public nuint e0;
        public nuint e1;
    }

    public unsafe struct _bitPos_e__FixedBuffer
    {
        public nuint e0;
        public nuint e1;
    }
}
