using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_frameParameters
    {
        /**< 1: content size will be in frame header (when known) */
        public int contentSizeFlag;

        /**< 1: generate a 32-bits checksum using XXH64 algorithm at end of frame, for error detection */
        public int checksumFlag;

        /**< 1: no dictID will be saved into frame header (dictID is only useful for dictionary compression) */
        public int noDictIDFlag;
    }
}
