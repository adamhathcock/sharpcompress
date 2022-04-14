using System;

namespace ZstdSharp.Unsafe
{
    /**
     * Struct used for the dictionary selection function.
     */
    public unsafe partial struct COVER_dictSelection
    {
        public byte* dictContent;

        public nuint dictSize;

        public nuint totalCompressedSize;
    }
}
