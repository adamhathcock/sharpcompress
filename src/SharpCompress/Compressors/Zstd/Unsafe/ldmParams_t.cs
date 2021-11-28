using System;

namespace ZstdSharp.Unsafe
{
    public partial struct ldmParams_t
    {
        /* 1 if enable long distance matching */
        public uint enableLdm;

        /* Log size of hashTable */
        public uint hashLog;

        /* Log bucket size for collision resolution, at most 8 */
        public uint bucketSizeLog;

        /* Minimum match length */
        public uint minMatchLength;

        /* Log number of entries to skip */
        public uint hashRateLog;

        /* Window log for the LDM */
        public uint windowLog;
    }
}
