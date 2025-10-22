namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_blockState_t
{
    public ZSTD_compressedBlockState_t* prevCBlock;
    public ZSTD_compressedBlockState_t* nextCBlock;
    public ZSTD_MatchState_t matchState;
}
