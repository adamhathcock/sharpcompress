namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_frameProgression
{
    /* nb input bytes read and buffered */
    public ulong ingested;

    /* nb input bytes actually compressed */
    public ulong consumed;

    /* nb of compressed bytes generated and buffered */
    public ulong produced;

    /* nb of compressed bytes flushed : not provided; can be tracked from caller side */
    public ulong flushed;

    /* MT only : latest started job nb */
    public uint currentJobID;

    /* MT only : nb of workers actively compressing at probe time */
    public uint nbActiveWorkers;
}
