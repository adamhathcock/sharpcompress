using System;
using System.Runtime.Intrinsics;

namespace ZstdSharp.Unsafe
{
    public partial struct ZSTD_Vec256
    {
        public Vector128<byte> fst;

        public Vector128<byte> snd;
    }
}
