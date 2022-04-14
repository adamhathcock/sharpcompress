using System;

namespace ZstdSharp.Unsafe
{
    public enum ZSTD_cwksp_alloc_phase_e
    {
        ZSTD_cwksp_alloc_objects,
        ZSTD_cwksp_alloc_buffers,
        ZSTD_cwksp_alloc_aligned,
    }
}
