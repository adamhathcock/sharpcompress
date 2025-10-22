namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*************************************
 *  Context memory management
 ***************************************/
public unsafe struct ZSTD_CDict_s
{
    public void* dictContent;
    public nuint dictContentSize;

    /* The dictContentType the CDict was created with */
    public ZSTD_dictContentType_e dictContentType;

    /* entropy workspace of HUF_WORKSPACE_SIZE bytes */
    public uint* entropyWorkspace;
    public ZSTD_cwksp workspace;
    public ZSTD_MatchState_t matchState;
    public ZSTD_compressedBlockState_t cBlockState;
    public ZSTD_customMem customMem;
    public uint dictID;

    /* 0 indicates that advanced API was used to select CDict params */
    public int compressionLevel;

    /* Indicates whether the CDict was created with params that would use
     * row-based matchfinder. Unless the cdict is reloaded, we will use
     * the same greedy/lazy matchfinder at compression time.
     */
    public ZSTD_paramSwitch_e useRowMatchFinder;
}
