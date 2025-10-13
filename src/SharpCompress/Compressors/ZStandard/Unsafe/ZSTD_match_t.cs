namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*********************************
 *  Compression internals structs *
 *********************************/
public struct ZSTD_match_t
{
    /* Offset sumtype code for the match, using ZSTD_storeSeq() format */
    public uint off;

    /* Raw length of match */
    public uint len;
}
