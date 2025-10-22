namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct ZSTD_blockSplitCtx
{
    public SeqStore_t fullSeqStoreChunk;
    public SeqStore_t firstHalfSeqStore;
    public SeqStore_t secondHalfSeqStore;
    public SeqStore_t currSeqStore;
    public SeqStore_t nextSeqStore;
    public fixed uint partitions[196];
    public ZSTD_entropyCTablesMetadata_t entropyMetadata;
}
