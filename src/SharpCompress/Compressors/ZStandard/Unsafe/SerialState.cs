namespace SharpCompress.Compressors.ZStandard.Unsafe;

public unsafe struct SerialState
{
    /* All variables in the struct are protected by mutex. */
    public void* mutex;
    public void* cond;
    public ZSTD_CCtx_params_s @params;
    public ldmState_t ldmState;
    public XXH64_state_s xxhState;
    public uint nextJobID;

    /* Protects ldmWindow.
     * Must be acquired after the main mutex when acquiring both.
     */
    public void* ldmWindowMutex;

    /* Signaled when ldmWindow is updated */
    public void* ldmWindowCond;

    /* A thread-safe copy of ldmState.window */
    public ZSTD_window_t ldmWindow;
}
