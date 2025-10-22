namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct optState_t
{
    /* table of literals statistics, of size 256 */
    public uint* litFreq;

    /* table of litLength statistics, of size (MaxLL+1) */
    public uint* litLengthFreq;

    /* table of matchLength statistics, of size (MaxML+1) */
    public uint* matchLengthFreq;

    /* table of offCode statistics, of size (MaxOff+1) */
    public uint* offCodeFreq;

    /* list of found matches, of size ZSTD_OPT_SIZE */
    public ZSTD_match_t* matchTable;

    /* All positions tracked by optimal parser, of size ZSTD_OPT_SIZE */
    public ZSTD_optimal_t* priceTable;

    /* nb of literals */
    public uint litSum;

    /* nb of litLength codes */
    public uint litLengthSum;

    /* nb of matchLength codes */
    public uint matchLengthSum;

    /* nb of offset codes */
    public uint offCodeSum;

    /* to compare to log2(litfreq) */
    public uint litSumBasePrice;

    /* to compare to log2(llfreq)  */
    public uint litLengthSumBasePrice;

    /* to compare to log2(mlfreq)  */
    public uint matchLengthSumBasePrice;

    /* to compare to log2(offreq)  */
    public uint offCodeSumBasePrice;

    /* prices can be determined dynamically, or follow a pre-defined cost structure */
    public ZSTD_OptPrice_e priceType;

    /* pre-calculated dictionary statistics */
    public ZSTD_entropyCTables_t* symbolCosts;
    public ZSTD_paramSwitch_e literalCompressionMode;
}
