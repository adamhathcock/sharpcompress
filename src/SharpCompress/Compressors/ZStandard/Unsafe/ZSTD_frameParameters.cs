namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct ZSTD_frameParameters
{
    /// <summary>1: content size will be in frame header (when known)</summary>
    public int contentSizeFlag;

    /// <summary>1: generate a 32-bits checksum using XXH64 algorithm at end of frame, for error detection</summary>
    public int checksumFlag;

    /// <summary>1: no dictID will be saved into frame header (dictID is only useful for dictionary compression)</summary>
    public int noDictIDFlag;
}
