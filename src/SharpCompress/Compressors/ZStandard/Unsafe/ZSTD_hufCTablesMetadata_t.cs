namespace SharpCompress.Compressors.ZStandard.Unsafe;

/***********************************************
 *  Entropy buffer statistics structs and funcs *
 ***********************************************/
/** ZSTD_hufCTablesMetadata_t :
 *  Stores Literals Block Type for a super-block in hType, and
 *  huffman tree description in hufDesBuffer.
 *  hufDesSize refers to the size of huffman tree description in bytes.
 *  This metadata is populated in ZSTD_buildBlockEntropyStats_literals() */
public unsafe struct ZSTD_hufCTablesMetadata_t
{
    public SymbolEncodingType_e hType;
    public fixed byte hufDesBuffer[128];
    public nuint hufDesSize;
}
