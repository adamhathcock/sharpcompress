using System.Diagnostics;
using System.Runtime.CompilerServices;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("DEBUG")]
    private static void ZSTD_cwksp_assert_internal_consistency(ZSTD_cwksp* ws)
    {
        assert(ws->workspace <= ws->objectEnd);
        assert(ws->objectEnd <= ws->tableEnd);
        assert(ws->objectEnd <= ws->tableValidEnd);
        assert(ws->tableEnd <= ws->allocStart);
        assert(ws->tableValidEnd <= ws->allocStart);
        assert(ws->allocStart <= ws->workspaceEnd);
        assert(ws->initOnceStart <= ZSTD_cwksp_initialAllocStart(ws));
        assert(ws->workspace <= ws->initOnceStart);
    }

    /**
     * Align must be a power of 2.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_align(nuint size, nuint align)
    {
        nuint mask = align - 1;
        assert(ZSTD_isPower2(align) != 0);
        return size + mask & ~mask;
    }

    /**
     * Use this to determine how much space in the workspace we will consume to
     * allocate this object. (Normally it should be exactly the size of the object,
     * but under special conditions, like ASAN, where we pad each object, it might
     * be larger.)
     *
     * Since tables aren't currently redzoned, you don't need to call through this
     * to figure out how much space you need for the matchState tables. Everything
     * else is though.
     *
     * Do not use for sizing aligned buffers. Instead, use ZSTD_cwksp_aligned64_alloc_size().
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_alloc_size(nuint size)
    {
        if (size == 0)
            return 0;
        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_aligned_alloc_size(nuint size, nuint alignment)
    {
        return ZSTD_cwksp_alloc_size(ZSTD_cwksp_align(size, alignment));
    }

    /**
     * Returns an adjusted alloc size that is the nearest larger multiple of 64 bytes.
     * Used to determine the number of bytes required for a given "aligned".
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_aligned64_alloc_size(nuint size)
    {
        return ZSTD_cwksp_aligned_alloc_size(size, 64);
    }

    /**
     * Returns the amount of additional space the cwksp must allocate
     * for internal purposes (currently only alignment).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_slack_space_required()
    {
        /* For alignment, the wksp will always allocate an additional 2*ZSTD_CWKSP_ALIGNMENT_BYTES
         * bytes to align the beginning of tables section and end of buffers;
         */
        const nuint slackSpace = 64 * 2;
        return slackSpace;
    }

    /**
     * Return the number of additional bytes required to align a pointer to the given number of bytes.
     * alignBytes must be a power of two.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_bytes_to_align_ptr(void* ptr, nuint alignBytes)
    {
        nuint alignBytesMask = alignBytes - 1;
        nuint bytes = alignBytes - ((nuint)ptr & alignBytesMask) & alignBytesMask;
        assert(ZSTD_isPower2(alignBytes) != 0);
        assert(bytes < alignBytes);
        return bytes;
    }

    /**
     * Returns the initial value for allocStart which is used to determine the position from
     * which we can allocate from the end of the workspace.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_initialAllocStart(ZSTD_cwksp* ws)
    {
        sbyte* endPtr = (sbyte*)ws->workspaceEnd;
        assert(ZSTD_isPower2(64) != 0);
        endPtr = endPtr - (nuint)endPtr % 64;
        return endPtr;
    }

    /**
     * Internal function. Do not use directly.
     * Reserves the given number of bytes within the aligned/buffer segment of the wksp,
     * which counts from the end of the wksp (as opposed to the object/table segment).
     *
     * Returns a pointer to the beginning of that space.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_internal_buffer_space(ZSTD_cwksp* ws, nuint bytes)
    {
        void* alloc = (byte*)ws->allocStart - bytes;
        void* bottom = ws->tableEnd;
        ZSTD_cwksp_assert_internal_consistency(ws);
        assert(alloc >= bottom);
        if (alloc < bottom)
        {
            ws->allocFailed = 1;
            return null;
        }

        if (alloc < ws->tableValidEnd)
        {
            ws->tableValidEnd = alloc;
        }

        ws->allocStart = alloc;
        return alloc;
    }

    /**
     * Moves the cwksp to the next phase, and does any necessary allocations.
     * cwksp initialization must necessarily go through each phase in order.
     * Returns a 0 on success, or zstd error
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_internal_advance_phase(
        ZSTD_cwksp* ws,
        ZSTD_cwksp_alloc_phase_e phase
    )
    {
        assert(phase >= ws->phase);
        if (phase > ws->phase)
        {
            if (
                ws->phase < ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned_init_once
                && phase >= ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned_init_once
            )
            {
                ws->tableValidEnd = ws->objectEnd;
                ws->initOnceStart = ZSTD_cwksp_initialAllocStart(ws);
                {
                    void* alloc = ws->objectEnd;
                    nuint bytesToAlign = ZSTD_cwksp_bytes_to_align_ptr(alloc, 64);
                    void* objectEnd = (byte*)alloc + bytesToAlign;
                    if (objectEnd > ws->workspaceEnd)
                    {
                        return unchecked(
                            (nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation)
                        );
                    }

                    ws->objectEnd = objectEnd;
                    ws->tableEnd = objectEnd;
                    if (ws->tableValidEnd < ws->tableEnd)
                    {
                        ws->tableValidEnd = ws->tableEnd;
                    }
                }
            }

            ws->phase = phase;
            ZSTD_cwksp_assert_internal_consistency(ws);
        }

        return 0;
    }

    /**
     * Returns whether this object/buffer/etc was allocated in this workspace.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_cwksp_owns_buffer(ZSTD_cwksp* ws, void* ptr)
    {
        return ptr != null && ws->workspace <= ptr && ptr < ws->workspaceEnd ? 1 : 0;
    }

    /**
     * Internal function. Do not use directly.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_internal(
        ZSTD_cwksp* ws,
        nuint bytes,
        ZSTD_cwksp_alloc_phase_e phase
    )
    {
        void* alloc;
        if (ERR_isError(ZSTD_cwksp_internal_advance_phase(ws, phase)) || bytes == 0)
        {
            return null;
        }

        alloc = ZSTD_cwksp_reserve_internal_buffer_space(ws, bytes);
        return alloc;
    }

    /**
     * Reserves and returns unaligned memory.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte* ZSTD_cwksp_reserve_buffer(ZSTD_cwksp* ws, nuint bytes)
    {
        return (byte*)ZSTD_cwksp_reserve_internal(
            ws,
            bytes,
            ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_buffers
        );
    }

    /**
     * Reserves and returns memory sized on and aligned on ZSTD_CWKSP_ALIGNMENT_BYTES (64 bytes).
     * This memory has been initialized at least once in the past.
     * This doesn't mean it has been initialized this time, and it might contain data from previous
     * operations.
     * The main usage is for algorithms that might need read access into uninitialized memory.
     * The algorithm must maintain safety under these conditions and must make sure it doesn't
     * leak any of the past data (directly or in side channels).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_aligned_init_once(ZSTD_cwksp* ws, nuint bytes)
    {
        nuint alignedBytes = ZSTD_cwksp_align(bytes, 64);
        void* ptr = ZSTD_cwksp_reserve_internal(
            ws,
            alignedBytes,
            ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned_init_once
        );
        assert(((nuint)ptr & 64 - 1) == 0);
        if (ptr != null && ptr < ws->initOnceStart)
        {
            memset(
                ptr,
                0,
                (uint)(
                    (nuint)((byte*)ws->initOnceStart - (byte*)ptr) < alignedBytes
                        ? (nuint)((byte*)ws->initOnceStart - (byte*)ptr)
                        : alignedBytes
                )
            );
            ws->initOnceStart = ptr;
        }

        return ptr;
    }

    /**
     * Reserves and returns memory sized on and aligned on ZSTD_CWKSP_ALIGNMENT_BYTES (64 bytes).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_aligned64(ZSTD_cwksp* ws, nuint bytes)
    {
        void* ptr = ZSTD_cwksp_reserve_internal(
            ws,
            ZSTD_cwksp_align(bytes, 64),
            ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned
        );
        assert(((nuint)ptr & 64 - 1) == 0);
        return ptr;
    }

    /**
     * Aligned on 64 bytes. These buffers have the special property that
     * their values remain constrained, allowing us to reuse them without
     * memset()-ing them.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_table(ZSTD_cwksp* ws, nuint bytes)
    {
        ZSTD_cwksp_alloc_phase_e phase =
            ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned_init_once;
        void* alloc;
        void* end;
        void* top;
        if (ws->phase < phase)
        {
            if (ERR_isError(ZSTD_cwksp_internal_advance_phase(ws, phase)))
            {
                return null;
            }
        }

        alloc = ws->tableEnd;
        end = (byte*)alloc + bytes;
        top = ws->allocStart;
        assert((bytes & sizeof(uint) - 1) == 0);
        ZSTD_cwksp_assert_internal_consistency(ws);
        assert(end <= top);
        if (end > top)
        {
            ws->allocFailed = 1;
            return null;
        }

        ws->tableEnd = end;
        assert((bytes & 64 - 1) == 0);
        assert(((nuint)alloc & 64 - 1) == 0);
        return alloc;
    }

    /**
     * Aligned on sizeof(void*).
     * Note : should happen only once, at workspace first initialization
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_object(ZSTD_cwksp* ws, nuint bytes)
    {
        nuint roundedBytes = ZSTD_cwksp_align(bytes, (nuint)sizeof(void*));
        void* alloc = ws->objectEnd;
        void* end = (byte*)alloc + roundedBytes;
        assert((nuint)alloc % (nuint)sizeof(void*) == 0);
        assert(bytes % (nuint)sizeof(void*) == 0);
        ZSTD_cwksp_assert_internal_consistency(ws);
        if (
            ws->phase != ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_objects
            || end > ws->workspaceEnd
        )
        {
            ws->allocFailed = 1;
            return null;
        }

        ws->objectEnd = end;
        ws->tableEnd = end;
        ws->tableValidEnd = end;
        return alloc;
    }

    /**
     * with alignment control
     * Note : should happen only once, at workspace first initialization
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* ZSTD_cwksp_reserve_object_aligned(
        ZSTD_cwksp* ws,
        nuint byteSize,
        nuint alignment
    )
    {
        nuint mask = alignment - 1;
        nuint surplus = alignment > (nuint)sizeof(void*) ? alignment - (nuint)sizeof(void*) : 0;
        void* start = ZSTD_cwksp_reserve_object(ws, byteSize + surplus);
        if (start == null)
            return null;
        if (surplus == 0)
            return start;
        assert(ZSTD_isPower2(alignment) != 0);
        return (void*)((nuint)start + surplus & ~mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_mark_tables_dirty(ZSTD_cwksp* ws)
    {
        assert(ws->tableValidEnd >= ws->objectEnd);
        assert(ws->tableValidEnd <= ws->allocStart);
        ws->tableValidEnd = ws->objectEnd;
        ZSTD_cwksp_assert_internal_consistency(ws);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_mark_tables_clean(ZSTD_cwksp* ws)
    {
        assert(ws->tableValidEnd >= ws->objectEnd);
        assert(ws->tableValidEnd <= ws->allocStart);
        if (ws->tableValidEnd < ws->tableEnd)
        {
            ws->tableValidEnd = ws->tableEnd;
        }

        ZSTD_cwksp_assert_internal_consistency(ws);
    }

    /**
     * Zero the part of the allocated tables not already marked clean.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_clean_tables(ZSTD_cwksp* ws)
    {
        assert(ws->tableValidEnd >= ws->objectEnd);
        assert(ws->tableValidEnd <= ws->allocStart);
        if (ws->tableValidEnd < ws->tableEnd)
        {
            memset(
                ws->tableValidEnd,
                0,
                (uint)(nuint)((byte*)ws->tableEnd - (byte*)ws->tableValidEnd)
            );
        }

        ZSTD_cwksp_mark_tables_clean(ws);
    }

    /**
     * Invalidates table allocations.
     * All other allocations remain valid.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_clear_tables(ZSTD_cwksp* ws)
    {
        ws->tableEnd = ws->objectEnd;
        ZSTD_cwksp_assert_internal_consistency(ws);
    }

    /**
     * Invalidates all buffer, aligned, and table allocations.
     * Object allocations remain valid.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_clear(ZSTD_cwksp* ws)
    {
        ws->tableEnd = ws->objectEnd;
        ws->allocStart = ZSTD_cwksp_initialAllocStart(ws);
        ws->allocFailed = 0;
        if (ws->phase > ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned_init_once)
        {
            ws->phase = ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_aligned_init_once;
        }

        ZSTD_cwksp_assert_internal_consistency(ws);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_sizeof(ZSTD_cwksp* ws)
    {
        return (nuint)((byte*)ws->workspaceEnd - (byte*)ws->workspace);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_used(ZSTD_cwksp* ws)
    {
        return (nuint)((byte*)ws->tableEnd - (byte*)ws->workspace)
            + (nuint)((byte*)ws->workspaceEnd - (byte*)ws->allocStart);
    }

    /**
     * The provided workspace takes ownership of the buffer [start, start+size).
     * Any existing values in the workspace are ignored (the previously managed
     * buffer, if present, must be separately freed).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_init(
        ZSTD_cwksp* ws,
        void* start,
        nuint size,
        ZSTD_cwksp_static_alloc_e isStatic
    )
    {
        assert(((nuint)start & (nuint)(sizeof(void*) - 1)) == 0);
        ws->workspace = start;
        ws->workspaceEnd = (byte*)start + size;
        ws->objectEnd = ws->workspace;
        ws->tableValidEnd = ws->objectEnd;
        ws->initOnceStart = ZSTD_cwksp_initialAllocStart(ws);
        ws->phase = ZSTD_cwksp_alloc_phase_e.ZSTD_cwksp_alloc_objects;
        ws->isStatic = isStatic;
        ZSTD_cwksp_clear(ws);
        ws->workspaceOversizedDuration = 0;
        ZSTD_cwksp_assert_internal_consistency(ws);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_create(ZSTD_cwksp* ws, nuint size, ZSTD_customMem customMem)
    {
        void* workspace = ZSTD_customMalloc(size, customMem);
        if (workspace == null)
        {
            return unchecked((nuint)(-(int)ZSTD_ErrorCode.ZSTD_error_memory_allocation));
        }

        ZSTD_cwksp_init(ws, workspace, size, ZSTD_cwksp_static_alloc_e.ZSTD_cwksp_dynamic_alloc);
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_free(ZSTD_cwksp* ws, ZSTD_customMem customMem)
    {
        void* ptr = ws->workspace;
        *ws = new ZSTD_cwksp();
        ZSTD_customFree(ptr, customMem);
    }

    /**
     * Moves the management of a workspace from one cwksp to another. The src cwksp
     * is left in an invalid state (src must be re-init()'ed before it's used again).
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_move(ZSTD_cwksp* dst, ZSTD_cwksp* src)
    {
        *dst = *src;
        *src = new ZSTD_cwksp();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_cwksp_reserve_failed(ZSTD_cwksp* ws)
    {
        return ws->allocFailed;
    }

    /* ZSTD_alignmentSpaceWithinBounds() :
     * Returns if the estimated space needed for a wksp is within an acceptable limit of the
     * actual amount of space used.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_cwksp_estimated_space_within_bounds(
        ZSTD_cwksp* ws,
        nuint estimatedSpace
    )
    {
        return
            estimatedSpace - ZSTD_cwksp_slack_space_required() <= ZSTD_cwksp_used(ws)
            && ZSTD_cwksp_used(ws) <= estimatedSpace
            ? 1
            : 0;
    }

    /*-*************************************
     *  Functions
     ***************************************/
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint ZSTD_cwksp_available_space(ZSTD_cwksp* ws)
    {
        return (nuint)((byte*)ws->allocStart - (byte*)ws->tableEnd);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_cwksp_check_available(ZSTD_cwksp* ws, nuint additionalNeededSpace)
    {
        return ZSTD_cwksp_available_space(ws) >= additionalNeededSpace ? 1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_cwksp_check_too_large(ZSTD_cwksp* ws, nuint additionalNeededSpace)
    {
        return ZSTD_cwksp_check_available(ws, additionalNeededSpace * 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZSTD_cwksp_check_wasteful(ZSTD_cwksp* ws, nuint additionalNeededSpace)
    {
        return
            ZSTD_cwksp_check_too_large(ws, additionalNeededSpace) != 0
            && ws->workspaceOversizedDuration > 128
            ? 1
            : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZSTD_cwksp_bump_oversized_duration(
        ZSTD_cwksp* ws,
        nuint additionalNeededSpace
    )
    {
        if (ZSTD_cwksp_check_too_large(ws, additionalNeededSpace) != 0)
        {
            ws->workspaceOversizedDuration++;
        }
        else
        {
            ws->workspaceOversizedDuration = 0;
        }
    }
}
