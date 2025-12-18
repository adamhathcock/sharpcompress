namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_dictMode_e
{
    ZSTD_noDict = 0,
    ZSTD_extDict = 1,
    ZSTD_dictMatchState = 2,
    ZSTD_dedicatedDictSearch = 3,
}
