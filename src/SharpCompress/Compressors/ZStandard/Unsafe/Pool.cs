using SharpCompress.Compressors.ZStandard.Unsafe;
using static SharpCompress.Compressors.ZStandard.UnsafeHelper;

namespace SharpCompress.Compressors.ZStandard.Unsafe;

public static unsafe partial class Methods
{
    private static JobThreadPool GetThreadPool(void* ctx) =>
        UnmanagedObject.Unwrap<JobThreadPool>(ctx);

    /* ZSTD_createThreadPool() : public access point */
    public static void* ZSTD_createThreadPool(nuint numThreads)
    {
        return POOL_create(numThreads, 0);
    }

    /*! POOL_create() :
     *  Create a thread pool with at most `numThreads` threads.
     * `numThreads` must be at least 1.
     *  The maximum number of queued jobs before blocking is `queueSize`.
     * @return : POOL_ctx pointer on success, else NULL.
     */
    private static void* POOL_create(nuint numThreads, nuint queueSize)
    {
        return POOL_create_advanced(numThreads, queueSize, Unsafe.Methods.ZSTD_defaultCMem);
    }

    private static void* POOL_create_advanced(
        nuint numThreads,
        nuint queueSize,
        ZSTD_customMem customMem
    )
    {
        var jobThreadPool = new JobThreadPool((int)numThreads, (int)queueSize);
        return UnmanagedObject.Wrap(jobThreadPool);
    }

    /*! POOL_join() :
    Shutdown the queue, wake any sleeping threads, and join all of the threads.
     */
    private static void POOL_join(void* ctx)
    {
        GetThreadPool(ctx).Join();
    }

    /*! POOL_free() :
     *  Free a thread pool returned by POOL_create().
     */
    private static void POOL_free(void* ctx)
    {
        if (ctx == null)
        {
            return;
        }

        var jobThreadPool = GetThreadPool(ctx);
        jobThreadPool.Join();
        jobThreadPool.Dispose();
        UnmanagedObject.Free(ctx);
    }

    /*! POOL_joinJobs() :
     *  Waits for all queued jobs to finish executing.
     */
    private static void POOL_joinJobs(void* ctx)
    {
        var jobThreadPool = GetThreadPool(ctx);
        jobThreadPool.Join(false);
    }

    public static void ZSTD_freeThreadPool(void* pool)
    {
        POOL_free(pool);
    }

    /*! POOL_sizeof() :
     * @return threadpool memory usage
     *  note : compatible with NULL (returns 0 in this case)
     */
    private static nuint POOL_sizeof(void* ctx)
    {
        if (ctx == null)
            return 0;
        var jobThreadPool = GetThreadPool(ctx);
        return (nuint)jobThreadPool.Size();
    }

    /* @return : 0 on success, 1 on error */
    private static int POOL_resize(void* ctx, nuint numThreads)
    {
        if (ctx == null)
            return 1;
        var jobThreadPool = GetThreadPool(ctx);
        jobThreadPool.Resize((int)numThreads);
        return 0;
    }

    /*! POOL_add() :
     *  Add the job `function(opaque)` to the thread pool. `ctx` must be valid.
     *  Possibly blocks until there is room in the queue.
     *  Note : The function may be executed asynchronously,
     *         therefore, `opaque` must live until function has been completed.
     */
    private static void POOL_add(void* ctx, void* function, void* opaque)
    {
        assert(ctx != null);
        var jobThreadPool = GetThreadPool(ctx);
        jobThreadPool.Add(function, opaque);
    }

    /*! POOL_tryAdd() :
     *  Add the job `function(opaque)` to thread pool _if_ a queue slot is available.
     *  Returns immediately even if not (does not block).
     * @return : 1 if successful, 0 if not.
     */
    private static int POOL_tryAdd(void* ctx, void* function, void* opaque)
    {
        assert(ctx != null);
        var jobThreadPool = GetThreadPool(ctx);
        return jobThreadPool.TryAdd(function, opaque) ? 1 : 0;
    }
}
