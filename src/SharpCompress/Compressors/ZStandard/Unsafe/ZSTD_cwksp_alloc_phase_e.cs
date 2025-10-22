namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*-*************************************
 *  Structures
 ***************************************/
public enum ZSTD_cwksp_alloc_phase_e
{
    ZSTD_cwksp_alloc_objects,
    ZSTD_cwksp_alloc_aligned_init_once,
    ZSTD_cwksp_alloc_aligned,
    ZSTD_cwksp_alloc_buffers,
}
