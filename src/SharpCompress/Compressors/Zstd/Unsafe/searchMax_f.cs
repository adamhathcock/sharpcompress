using System;
using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate nuint searchMax_f(ZSTD_matchState_t* ms, byte* ip, byte* iLimit, nuint* offsetPtr);
}
