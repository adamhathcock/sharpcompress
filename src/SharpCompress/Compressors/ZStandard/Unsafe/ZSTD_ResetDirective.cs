namespace SharpCompress.Compressors.ZStandard.Unsafe;

public enum ZSTD_ResetDirective
{
    ZSTD_reset_session_only = 1,
    ZSTD_reset_parameters = 2,
    ZSTD_reset_session_and_parameters = 3,
}
