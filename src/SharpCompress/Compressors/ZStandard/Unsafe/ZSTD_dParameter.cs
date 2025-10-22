namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* The advanced API pushes parameters one by one into an existing DCtx context.
 * Parameters are sticky, and remain valid for all following frames
 * using the same DCtx context.
 * It's possible to reset parameters to default values using ZSTD_DCtx_reset().
 * Note : This API is compatible with existing ZSTD_decompressDCtx() and ZSTD_decompressStream().
 *        Therefore, no new decompression function is necessary.
 */
public enum ZSTD_dParameter
{
    /* Select a size limit (in power of 2) beyond which
     * the streaming API will refuse to allocate memory buffer
     * in order to protect the host from unreasonable memory requirements.
     * This parameter is only useful in streaming mode, since no internal buffer is allocated in single-pass mode.
     * By default, a decompression context accepts window sizes <= (1 << ZSTD_WINDOWLOG_LIMIT_DEFAULT).
     * Special: value 0 means "use default maximum windowLog". */
    ZSTD_d_windowLogMax = 100,

    /* note : additional experimental parameters are also available
     * within the experimental section of the API.
     * At the time of this writing, they include :
     * ZSTD_d_format
     * ZSTD_d_stableOutBuffer
     * ZSTD_d_forceIgnoreChecksum
     * ZSTD_d_refMultipleDDicts
     * ZSTD_d_disableHuffmanAssembly
     * ZSTD_d_maxBlockSize
     * Because they are not stable, it's necessary to define ZSTD_STATIC_LINKING_ONLY to access them.
     * note : never ever use experimentalParam? names directly
     */
    ZSTD_d_experimentalParam1 = 1000,
    ZSTD_d_experimentalParam2 = 1001,
    ZSTD_d_experimentalParam3 = 1002,
    ZSTD_d_experimentalParam4 = 1003,
    ZSTD_d_experimentalParam5 = 1004,
    ZSTD_d_experimentalParam6 = 1005,
}
