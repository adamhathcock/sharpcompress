using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    /* for hashLog > 8, hash 2 bytes.
     * for hashLog == 8, just take the byte, no hashing.
     * The speed of this method relies on compile-time constant propagation */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint hash2(void* p, uint hashLog)
    {
        assert(hashLog >= 8);
        if (hashLog == 8)
            return ((byte*)p)[0];
        assert(hashLog <= 10);
        return MEM_read16(p) * 0x9e3779b9 >> (int)(32 - hashLog);
    }

    private static void initStats(FPStats* fpstats)
    {
        *fpstats = new FPStats();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void addEvents_generic(
        Fingerprint* fp,
        void* src,
        nuint srcSize,
        nuint samplingRate,
        uint hashLog
    )
    {
        sbyte* p = (sbyte*)src;
        nuint limit = srcSize - 2 + 1;
        nuint n;
        assert(srcSize >= 2);
        for (n = 0; n < limit; n += samplingRate)
        {
            fp->events[hash2(p + n, hashLog)]++;
        }

        fp->nbEvents += limit / samplingRate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void recordFingerprint_generic(
        Fingerprint* fp,
        void* src,
        nuint srcSize,
        nuint samplingRate,
        uint hashLog
    )
    {
        memset(fp, 0, (uint)(sizeof(uint) * ((nuint)1 << (int)hashLog)));
        fp->nbEvents = 0;
        addEvents_generic(fp, src, srcSize, samplingRate, hashLog);
    }

    private static void ZSTD_recordFingerprint_1(Fingerprint* fp, void* src, nuint srcSize)
    {
        recordFingerprint_generic(fp, src, srcSize, 1, 10);
    }

    private static void ZSTD_recordFingerprint_5(Fingerprint* fp, void* src, nuint srcSize)
    {
        recordFingerprint_generic(fp, src, srcSize, 5, 10);
    }

    private static void ZSTD_recordFingerprint_11(Fingerprint* fp, void* src, nuint srcSize)
    {
        recordFingerprint_generic(fp, src, srcSize, 11, 9);
    }

    private static void ZSTD_recordFingerprint_43(Fingerprint* fp, void* src, nuint srcSize)
    {
        recordFingerprint_generic(fp, src, srcSize, 43, 8);
    }

    private static ulong abs64(long s64)
    {
        return (ulong)(s64 < 0 ? -s64 : s64);
    }

    private static ulong fpDistance(Fingerprint* fp1, Fingerprint* fp2, uint hashLog)
    {
        ulong distance = 0;
        nuint n;
        assert(hashLog <= 10);
        for (n = 0; n < (nuint)1 << (int)hashLog; n++)
        {
            distance += abs64(
                fp1->events[n] * (long)fp2->nbEvents - fp2->events[n] * (long)fp1->nbEvents
            );
        }

        return distance;
    }

    /* Compare newEvents with pastEvents
     * return 1 when considered "too different"
     */
    private static int compareFingerprints(
        Fingerprint* @ref,
        Fingerprint* newfp,
        int penalty,
        uint hashLog
    )
    {
        assert(@ref->nbEvents > 0);
        assert(newfp->nbEvents > 0);
        {
            ulong p50 = @ref->nbEvents * (ulong)newfp->nbEvents;
            ulong deviation = fpDistance(@ref, newfp, hashLog);
            ulong threshold = p50 * (ulong)(16 - 2 + penalty) / 16;
            return deviation >= threshold ? 1 : 0;
        }
    }

    private static void mergeEvents(Fingerprint* acc, Fingerprint* newfp)
    {
        nuint n;
        for (n = 0; n < 1 << 10; n++)
        {
            acc->events[n] += newfp->events[n];
        }

        acc->nbEvents += newfp->nbEvents;
    }

    private static void flushEvents(FPStats* fpstats)
    {
        nuint n;
        for (n = 0; n < 1 << 10; n++)
        {
            fpstats->pastEvents.events[n] = fpstats->newEvents.events[n];
        }

        fpstats->pastEvents.nbEvents = fpstats->newEvents.nbEvents;
        fpstats->newEvents = new Fingerprint();
    }

    private static void removeEvents(Fingerprint* acc, Fingerprint* slice)
    {
        nuint n;
        for (n = 0; n < 1 << 10; n++)
        {
            assert(acc->events[n] >= slice->events[n]);
            acc->events[n] -= slice->events[n];
        }

        acc->nbEvents -= slice->nbEvents;
    }

    private static readonly void*[] records_fs = new void*[4]
    {
        (delegate* managed<Fingerprint*, void*, nuint, void>)(&ZSTD_recordFingerprint_43),
        (delegate* managed<Fingerprint*, void*, nuint, void>)(&ZSTD_recordFingerprint_11),
        (delegate* managed<Fingerprint*, void*, nuint, void>)(&ZSTD_recordFingerprint_5),
        (delegate* managed<Fingerprint*, void*, nuint, void>)(&ZSTD_recordFingerprint_1),
    };
#if NET7_0_OR_GREATER
    private static ReadOnlySpan<uint> Span_hashParams => new uint[4] { 8, 9, 10, 10 };
    private static uint* hashParams =>
        (uint*)
            System.Runtime.CompilerServices.Unsafe.AsPointer(
                ref MemoryMarshal.GetReference(Span_hashParams)
            );
#else

    private static readonly uint* hashParams = GetArrayPointer(new uint[4] { 8, 9, 10, 10 });
#endif

    private static nuint ZSTD_splitBlock_byChunks(
        void* blockStart,
        nuint blockSize,
        int level,
        void* workspace,
        nuint wkspSize
    )
    {
        assert(0 <= level && level <= 3);
        void* record_f = records_fs[level];
        FPStats* fpstats = (FPStats*)workspace;
        sbyte* p = (sbyte*)blockStart;
        int penalty = 3;
        nuint pos = 0;
        assert(blockSize == 128 << 10);
        assert(workspace != null);
        assert((nuint)workspace % (nuint)Math.Max(sizeof(uint), sizeof(ulong)) == 0);
        assert(wkspSize >= (nuint)sizeof(FPStats));
        initStats(fpstats);
        ((delegate* managed<Fingerprint*, void*, nuint, void>)record_f)(
            &fpstats->pastEvents,
            p,
            8 << 10
        );
        for (pos = 8 << 10; pos <= blockSize - (8 << 10); pos += 8 << 10)
        {
            ((delegate* managed<Fingerprint*, void*, nuint, void>)record_f)(
                &fpstats->newEvents,
                p + pos,
                8 << 10
            );
            if (
                compareFingerprints(
                    &fpstats->pastEvents,
                    &fpstats->newEvents,
                    penalty,
                    hashParams[level]
                ) != 0
            )
            {
                return pos;
            }
            else
            {
                mergeEvents(&fpstats->pastEvents, &fpstats->newEvents);
                if (penalty > 0)
                    penalty--;
            }
        }

        assert(pos == blockSize);
        return blockSize;
    }

    /* ZSTD_splitBlock_fromBorders(): very fast strategy :
     * compare fingerprint from beginning and end of the block,
     * derive from their difference if it's preferable to split in the middle,
     * repeat the process a second time, for finer grained decision.
     * 3 times did not brought improvements, so I stopped at 2.
     * Benefits are good enough for a cheap heuristic.
     * More accurate splitting saves more, but speed impact is also more perceptible.
     * For better accuracy, use more elaborate variant *_byChunks.
     */
    private static nuint ZSTD_splitBlock_fromBorders(
        void* blockStart,
        nuint blockSize,
        void* workspace,
        nuint wkspSize
    )
    {
        FPStats* fpstats = (FPStats*)workspace;
        Fingerprint* middleEvents = (Fingerprint*)(void*)((sbyte*)workspace + 512 * sizeof(uint));
        assert(blockSize == 128 << 10);
        assert(workspace != null);
        assert((nuint)workspace % (nuint)Math.Max(sizeof(uint), sizeof(ulong)) == 0);
        assert(wkspSize >= (nuint)sizeof(FPStats));
        initStats(fpstats);
        HIST_add(fpstats->pastEvents.events, blockStart, 512);
        HIST_add(fpstats->newEvents.events, (sbyte*)blockStart + blockSize - 512, 512);
        fpstats->pastEvents.nbEvents = fpstats->newEvents.nbEvents = 512;
        if (compareFingerprints(&fpstats->pastEvents, &fpstats->newEvents, 0, 8) == 0)
            return blockSize;
        HIST_add(middleEvents->events, (sbyte*)blockStart + blockSize / 2 - 512 / 2, 512);
        middleEvents->nbEvents = 512;
        {
            ulong distFromBegin = fpDistance(&fpstats->pastEvents, middleEvents, 8);
            ulong distFromEnd = fpDistance(&fpstats->newEvents, middleEvents, 8);
            const ulong minDistance = 512 * 512 / 3;
            if (abs64((long)distFromBegin - (long)distFromEnd) < minDistance)
                return 64 * (1 << 10);
            return (nuint)(distFromBegin > distFromEnd ? 32 * (1 << 10) : 96 * (1 << 10));
        }
    }

    /* ZSTD_splitBlock():
     * @level must be a value between 0 and 4.
     *        higher levels spend more energy to detect block boundaries.
     * @workspace must be aligned for size_t.
     * @wkspSize must be at least >= ZSTD_SLIPBLOCK_WORKSPACESIZE
     * note:
     * For the time being, this function only accepts full 128 KB blocks.
     * Therefore, @blockSize must be == 128 KB.
     * While this could be extended to smaller sizes in the future,
     * it is not yet clear if this would be useful. TBD.
     */
    private static nuint ZSTD_splitBlock(
        void* blockStart,
        nuint blockSize,
        int level,
        void* workspace,
        nuint wkspSize
    )
    {
        assert(0 <= level && level <= 4);
        if (level == 0)
            return ZSTD_splitBlock_fromBorders(blockStart, blockSize, workspace, wkspSize);
        return ZSTD_splitBlock_byChunks(blockStart, blockSize, level - 1, workspace, wkspSize);
    }
}
