namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Struct used for the dictionary selection function.
 */
public unsafe struct COVER_dictSelection
{
    public byte* dictContent;
    public nuint dictSize;
    public nuint totalCompressedSize;
}
