using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_parameters
    {
        public ZSTD_compressionParameters cParams;

        public ZSTD_frameParameters fParams;
    }
}
