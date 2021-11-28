using System;

namespace ZstdSharp.Unsafe
{
    /*-***************************/
    /*  generic DTableDesc       */
    /*-***************************/
    public partial struct DTableDesc
    {
        public byte maxTableLog;

        public byte tableType;

        public byte tableLog;

        public byte reserved;
    }
}
