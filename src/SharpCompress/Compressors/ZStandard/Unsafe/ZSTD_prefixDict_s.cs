namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_prefixDict_s
{
    public void* dict;
    public nuint dictSize;
    public ZSTD_dictContentType_e dictContentType;
}
