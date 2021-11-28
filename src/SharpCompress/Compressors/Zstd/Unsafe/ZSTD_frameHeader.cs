using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_frameHeader
    {
        /* if == ZSTD_CONTENTSIZE_UNKNOWN, it means this field is not available. 0 means "empty" */
        public ulong frameContentSize;

        /* can be very large, up to <= frameContentSize */
        public ulong windowSize;

        public uint blockSizeMax;

        /* if == ZSTD_skippableFrame, frameContentSize is the size of skippable content */
        public ZSTD_frameType_e frameType;

        public uint headerSize;

        public uint dictID;

        public uint checksumFlag;
    }
}
