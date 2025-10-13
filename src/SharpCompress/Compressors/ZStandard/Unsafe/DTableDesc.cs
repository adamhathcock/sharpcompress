namespace ZstdSharp.Unsafe
{
    /*-***************************/
    /*  generic DTableDesc       */
    /*-***************************/
    public struct DTableDesc
    {
        public byte maxTableLog;
        public byte tableType;
        public byte tableLog;
        public byte reserved;
    }
}