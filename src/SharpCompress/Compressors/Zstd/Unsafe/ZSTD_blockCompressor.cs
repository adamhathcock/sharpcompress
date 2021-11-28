using System;
using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate nuint ZSTD_blockCompressor(ZSTD_matchState_t* bs, seqStore_t* seqStore, uint* rep, void* src, nuint srcSize);
}
