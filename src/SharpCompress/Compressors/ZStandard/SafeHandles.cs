using System;
using System.Runtime.InteropServices;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

/// <summary>
/// Provides the base class for ZstdSharp <see cref="SafeHandle"/> implementations.
/// </summary>
/// <remarks>
/// Even though ZstdSharp is a managed library, its internals are using unmanaged
/// memory and we are using safe handles in the library's high-level API to ensure
/// proper disposal of unmanaged resources and increase safety.
/// </remarks>
/// <seealso cref="SafeCctxHandle"/>
/// <seealso cref="SafeDctxHandle"/>
internal abstract unsafe class SafeZstdHandle : SafeHandle
{
    /// <summary>
    /// Parameterless constructor is hidden. Use the static <c>Create</c> factory
    /// method to create a new safe handle instance.
    /// </summary>
    protected SafeZstdHandle()
        : base(IntPtr.Zero, true) { }

    public sealed override bool IsInvalid => handle == IntPtr.Zero;
}

/// <summary>
/// Safely wraps an unmanaged Zstd compression context.
/// </summary>
internal sealed unsafe class SafeCctxHandle : SafeZstdHandle
{
    /// <inheritdoc/>
    private SafeCctxHandle() { }

    /// <summary>
    /// Creates a new instance of <see cref="SafeCctxHandle"/>.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ZstdException">Creation failed.</exception>
    public static SafeCctxHandle Create()
    {
        var safeHandle = new SafeCctxHandle();
        bool success = false;
        try
        {
            var cctx = Unsafe.Methods.ZSTD_createCCtx();
            if (cctx == null)
                throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Failed to create cctx");
            safeHandle.SetHandle((IntPtr)cctx);
            success = true;
        }
        finally
        {
            if (!success)
            {
                safeHandle.SetHandleAsInvalid();
            }
        }
        return safeHandle;
    }

    /// <summary>
    /// Acquires a reference to the safe handle.
    /// </summary>
    /// <returns>
    /// A <see cref="SafeHandleHolder{T}"/> instance that can be implicitly converted to a pointer
    /// to <see cref="ZSTD_CCtx_s"/>.
    /// </returns>
    public SafeHandleHolder<ZSTD_CCtx_s> Acquire() => new(this);

    protected override bool ReleaseHandle()
    {
        return Unsafe.Methods.ZSTD_freeCCtx((ZSTD_CCtx_s*)handle) == 0;
    }
}

/// <summary>
/// Safely wraps an unmanaged Zstd compression context.
/// </summary>
internal sealed unsafe class SafeDctxHandle : SafeZstdHandle
{
    /// <inheritdoc/>
    private SafeDctxHandle() { }

    /// <summary>
    /// Creates a new instance of <see cref="SafeDctxHandle"/>.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ZstdException">Creation failed.</exception>
    public static SafeDctxHandle Create()
    {
        var safeHandle = new SafeDctxHandle();
        bool success = false;
        try
        {
            var dctx = Unsafe.Methods.ZSTD_createDCtx();
            if (dctx == null)
                throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Failed to create dctx");
            safeHandle.SetHandle((IntPtr)dctx);
            success = true;
        }
        finally
        {
            if (!success)
            {
                safeHandle.SetHandleAsInvalid();
            }
        }
        return safeHandle;
    }

    /// <summary>
    /// Acquires a reference to the safe handle.
    /// </summary>
    /// <returns>
    /// A <see cref="SafeHandleHolder{T}"/> instance that can be implicitly converted to a pointer
    /// to <see cref="ZSTD_DCtx_s"/>.
    /// </returns>
    public SafeHandleHolder<ZSTD_DCtx_s> Acquire() => new(this);

    protected override bool ReleaseHandle()
    {
        return Unsafe.Methods.ZSTD_freeDCtx((ZSTD_DCtx_s*)handle) == 0;
    }
}

/// <summary>
/// Provides a convenient interface to safely acquire pointers of a specific type
/// from a <see cref="SafeHandle"/>, by utilizing <see langword="using"/> blocks.
/// </summary>
/// <typeparam name="T">The type of pointers to return.</typeparam>
/// <remarks>
/// Safe handle holders can be <see cref="Dispose"/>d to decrement the safe handle's
/// reference count, and can be implicitly converted to pointers to <see cref="T"/>.
/// </remarks>
internal unsafe ref struct SafeHandleHolder<T>
    where T : unmanaged
{
    private readonly SafeHandle _handle;

    private bool _refAdded;

    public SafeHandleHolder(SafeHandle safeHandle)
    {
        _handle = safeHandle;
        _refAdded = false;
        safeHandle.DangerousAddRef(ref _refAdded);
    }

    public static implicit operator T*(SafeHandleHolder<T> holder) =>
        (T*)holder._handle.DangerousGetHandle();

    public void Dispose()
    {
        if (_refAdded)
        {
            _handle.DangerousRelease();
            _refAdded = false;
        }
    }
}
