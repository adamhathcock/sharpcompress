namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct seqState_t
{
    public BIT_DStream_t DStream;
    public ZSTD_fseState stateLL;
    public ZSTD_fseState stateOffb;
    public ZSTD_fseState stateML;
    public _prevOffset_e__FixedBuffer prevOffset;

    public unsafe struct _prevOffset_e__FixedBuffer
    {
        public nuint e0;
        public nuint e1;
        public nuint e2;
    }
}
