using System;

namespace ZstdSharp.Unsafe
{
    /**
     *Number of epochs and size of each epoch.
     */
    public partial struct COVER_epoch_info_t
    {
        public uint num;

        public uint size;
    }
}
