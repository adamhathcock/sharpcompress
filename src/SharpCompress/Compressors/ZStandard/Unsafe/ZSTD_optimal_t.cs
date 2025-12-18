namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_optimal_t
{
    /* price from beginning of segment to this position */
    public int price;

    /* offset of previous match */
    public uint off;

    /* length of previous match */
    public uint mlen;

    /* nb of literals since previous match */
    public uint litlen;

    /* offset history after previous match */
    public fixed uint rep[3];
}
