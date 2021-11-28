using System;

namespace ZstdSharp.Unsafe
{
    /*-*******************************************************
    *  Types
    *********************************************************/
    public unsafe partial struct ZSTD_DDict_s
    {
        public void* dictBuffer;

        public void* dictContent;

        public nuint dictSize;

        public ZSTD_entropyDTables_t entropy;

        public uint dictID;

        public uint entropyPresent;

        public ZSTD_customMem cMem;
    }
}
