using System;
using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate nuint ZSTD_decompressSequences_t(ZSTD_DCtx_s* dctx, void* dst, nuint maxDstSize, void* seqStart, nuint seqSize, int nbSeq, ZSTD_longOffset_e isLongOffset, int frame);
}
