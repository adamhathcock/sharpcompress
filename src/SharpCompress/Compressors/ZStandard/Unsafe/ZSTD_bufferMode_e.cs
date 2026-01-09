namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* Controls whether the input/output buffer is buffered or stable. */
public enum ZSTD_bufferMode_e
{
    /* Buffer the input/output */
    ZSTD_bm_buffered = 0,

    /* ZSTD_inBuffer/ZSTD_outBuffer is stable */
    ZSTD_bm_stable = 1,
}
