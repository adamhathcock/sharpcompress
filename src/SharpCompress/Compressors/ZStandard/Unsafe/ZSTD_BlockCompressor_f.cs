using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate nuint ZSTD_BlockCompressor_f(ZSTD_MatchState_t* bs, SeqStore_t* seqStore, uint* rep, void* src, nuint srcSize);
}