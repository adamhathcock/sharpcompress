using System;
using System.Runtime.InteropServices;

namespace ZstdSharp.Unsafe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate nuint ZSTD_sequenceCopier(ZSTD_CCtx_s* cctx, ZSTD_sequencePosition* seqPos, ZSTD_Sequence* inSeqs, nuint inSeqsSize, void* src, nuint blockSize);
}
