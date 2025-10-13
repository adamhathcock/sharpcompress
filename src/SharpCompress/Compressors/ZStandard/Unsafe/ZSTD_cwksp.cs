namespace SharpCompress.Compressors.ZStandard.Unsafe;

/**
 * Zstd fits all its internal datastructures into a single continuous buffer,
 * so that it only needs to perform a single OS allocation (or so that a buffer
 * can be provided to it and it can perform no allocations at all). This buffer
 * is called the workspace.
 *
 * Several optimizations complicate that process of allocating memory ranges
 * from this workspace for each internal datastructure:
 *
 * - These different internal datastructures have different setup requirements:
 *
 *   - The static objects need to be cleared once and can then be trivially
 *     reused for each compression.
 *
 *   - Various buffers don't need to be initialized at all--they are always
 *     written into before they're read.
 *
 *   - The matchstate tables have a unique requirement that they don't need
 *     their memory to be totally cleared, but they do need the memory to have
 *     some bound, i.e., a guarantee that all values in the memory they've been
 *     allocated is less than some maximum value (which is the starting value
 *     for the indices that they will then use for compression). When this
 *     guarantee is provided to them, they can use the memory without any setup
 *     work. When it can't, they have to clear the area.
 *
 * - These buffers also have different alignment requirements.
 *
 * - We would like to reuse the objects in the workspace for multiple
 *   compressions without having to perform any expensive reallocation or
 *   reinitialization work.
 *
 * - We would like to be able to efficiently reuse the workspace across
 *   multiple compressions **even when the compression parameters change** and
 *   we need to resize some of the objects (where possible).
 *
 * To attempt to manage this buffer, given these constraints, the ZSTD_cwksp
 * abstraction was created. It works as follows:
 *
 * Workspace Layout:
 *
 * [                        ... workspace ...                           ]
 * [objects][tables ->] free space [<- buffers][<- aligned][<- init once]
 *
 * The various objects that live in the workspace are divided into the
 * following categories, and are allocated separately:
 *
 * - Static objects: this is optionally the enclosing ZSTD_CCtx or ZSTD_CDict,
 *   so that literally everything fits in a single buffer. Note: if present,
 *   this must be the first object in the workspace, since ZSTD_customFree{CCtx,
 *   CDict}() rely on a pointer comparison to see whether one or two frees are
 *   required.
 *
 * - Fixed size objects: these are fixed-size, fixed-count objects that are
 *   nonetheless "dynamically" allocated in the workspace so that we can
 *   control how they're initialized separately from the broader ZSTD_CCtx.
 *   Examples:
 *   - Entropy Workspace
 *   - 2 x ZSTD_compressedBlockState_t
 *   - CDict dictionary contents
 *
 * - Tables: these are any of several different datastructures (hash tables,
 *   chain tables, binary trees) that all respect a common format: they are
 *   uint32_t arrays, all of whose values are between 0 and (nextSrc - base).
 *   Their sizes depend on the cparams. These tables are 64-byte aligned.
 *
 * - Init once: these buffers require to be initialized at least once before
 *   use. They should be used when we want to skip memory initialization
 *   while not triggering memory checkers (like Valgrind) when reading from
 *   from this memory without writing to it first.
 *   These buffers should be used carefully as they might contain data
 *   from previous compressions.
 *   Buffers are aligned to 64 bytes.
 *
 * - Aligned: these buffers don't require any initialization before they're
 *   used. The user of the buffer should make sure they write into a buffer
 *   location before reading from it.
 *   Buffers are aligned to 64 bytes.
 *
 * - Buffers: these buffers are used for various purposes that don't require
 *   any alignment or initialization before they're used. This means they can
 *   be moved around at no cost for a new compression.
 *
 * Allocating Memory:
 *
 * The various types of objects must be allocated in order, so they can be
 * correctly packed into the workspace buffer. That order is:
 *
 * 1. Objects
 * 2. Init once / Tables
 * 3. Aligned / Tables
 * 4. Buffers / Tables
 *
 * Attempts to reserve objects of different types out of order will fail.
 */
public unsafe struct ZSTD_cwksp
{
    public void* workspace;
    public void* workspaceEnd;
    public void* objectEnd;
    public void* tableEnd;
    public void* tableValidEnd;
    public void* allocStart;
    public void* initOnceStart;
    public byte allocFailed;
    public int workspaceOversizedDuration;
    public ZSTD_cwksp_alloc_phase_e phase;
    public ZSTD_cwksp_static_alloc_e isStatic;
}
