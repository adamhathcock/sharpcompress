using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_entropyCTablesMetadata_t
    {
        public ZSTD_hufCTablesMetadata_t hufMetadata;

        public ZSTD_fseCTablesMetadata_t fseMetadata;
    }
}
