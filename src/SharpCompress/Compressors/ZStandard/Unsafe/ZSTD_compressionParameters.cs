namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_compressionParameters
{
    /// <summary>largest match distance : larger == more compression, more memory needed during decompression</summary>
    public uint windowLog;

    /// <summary>fully searched segment : larger == more compression, slower, more memory (useless for fast)</summary>
    public uint chainLog;

    /// <summary>dispatch table : larger == faster, more memory</summary>
    public uint hashLog;

    /// <summary>nb of searches : larger == more compression, slower</summary>
    public uint searchLog;

    /// <summary>match length searched : larger == faster decompression, sometimes less compression</summary>
    public uint minMatch;

    /// <summary>acceptable match size for optimal parser (only) : larger == more compression, slower</summary>
    public uint targetLength;

    /// <summary>see ZSTD_strategy definition above</summary>
    public ZSTD_strategy strategy;

    public ZSTD_compressionParameters(
        uint windowLog,
        uint chainLog,
        uint hashLog,
        uint searchLog,
        uint minMatch,
        uint targetLength,
        ZSTD_strategy strategy
    )
    {
        this.windowLog = windowLog;
        this.chainLog = chainLog;
        this.hashLog = hashLog;
        this.searchLog = searchLog;
        this.minMatch = minMatch;
        this.targetLength = targetLength;
        this.strategy = strategy;
    }
}
