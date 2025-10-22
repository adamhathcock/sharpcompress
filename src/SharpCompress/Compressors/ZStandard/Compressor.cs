using System;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public unsafe class Compressor : IDisposable
{
    /// <summary>
    /// Minimum negative compression level allowed
    /// </summary>
    public static int MinCompressionLevel => Unsafe.Methods.ZSTD_minCLevel();

    /// <summary>
    /// Maximum compression level available
    /// </summary>
    public static int MaxCompressionLevel => Unsafe.Methods.ZSTD_maxCLevel();

    /// <summary>
    /// Default compression level
    /// </summary>
    /// <see cref="Unsafe.Methods.ZSTD_defaultCLevel"/>
    public const int DefaultCompressionLevel = 3;

    private int level = DefaultCompressionLevel;

    private readonly SafeCctxHandle handle;

    public int Level
    {
        get => level;
        set
        {
            if (level != value)
            {
                level = value;
                SetParameter(ZSTD_cParameter.ZSTD_c_compressionLevel, value);
            }
        }
    }

    public void SetParameter(ZSTD_cParameter parameter, int value)
    {
        using var cctx = handle.Acquire();
        Unsafe.Methods.ZSTD_CCtx_setParameter(cctx, parameter, value).EnsureZstdSuccess();
    }

    public int GetParameter(ZSTD_cParameter parameter)
    {
        using var cctx = handle.Acquire();
        int value;
        Unsafe.Methods.ZSTD_CCtx_getParameter(cctx, parameter, &value).EnsureZstdSuccess();
        return value;
    }

    public void LoadDictionary(byte[] dict)
    {
        var dictReadOnlySpan = new ReadOnlySpan<byte>(dict);
        LoadDictionary(dictReadOnlySpan);
    }

    public void LoadDictionary(ReadOnlySpan<byte> dict)
    {
        using var cctx = handle.Acquire();
        fixed (byte* dictPtr = dict)
            Unsafe
                .Methods.ZSTD_CCtx_loadDictionary(cctx, dictPtr, (nuint)dict.Length)
                .EnsureZstdSuccess();
    }

    public Compressor(int level = DefaultCompressionLevel)
    {
        handle = SafeCctxHandle.Create();
        Level = level;
    }

    public static int GetCompressBound(int length) =>
        (int)Unsafe.Methods.ZSTD_compressBound((nuint)length);

    public static ulong GetCompressBoundLong(ulong length) =>
        Unsafe.Methods.ZSTD_compressBound((nuint)length);

    public Span<byte> Wrap(ReadOnlySpan<byte> src)
    {
        var dest = new byte[GetCompressBound(src.Length)];
        var length = Wrap(src, dest);
        return new Span<byte>(dest, 0, length);
    }

    public int Wrap(byte[] src, byte[] dest, int offset) =>
        Wrap(src, new Span<byte>(dest, offset, dest.Length - offset));

    public int Wrap(ReadOnlySpan<byte> src, Span<byte> dest)
    {
        fixed (byte* srcPtr = src)
        fixed (byte* destPtr = dest)
        {
            using var cctx = handle.Acquire();
            return (int)
                Unsafe
                    .Methods.ZSTD_compress2(
                        cctx,
                        destPtr,
                        (nuint)dest.Length,
                        srcPtr,
                        (nuint)src.Length
                    )
                    .EnsureZstdSuccess();
        }
    }

    public int Wrap(ArraySegment<byte> src, ArraySegment<byte> dest) =>
        Wrap((ReadOnlySpan<byte>)src, dest);

    public int Wrap(
        byte[] src,
        int srcOffset,
        int srcLength,
        byte[] dst,
        int dstOffset,
        int dstLength
    ) =>
        Wrap(
            new ReadOnlySpan<byte>(src, srcOffset, srcLength),
            new Span<byte>(dst, dstOffset, dstLength)
        );

    public bool TryWrap(byte[] src, byte[] dest, int offset, out int written) =>
        TryWrap(src, new Span<byte>(dest, offset, dest.Length - offset), out written);

    public bool TryWrap(ReadOnlySpan<byte> src, Span<byte> dest, out int written)
    {
        fixed (byte* srcPtr = src)
        fixed (byte* destPtr = dest)
        {
            nuint returnValue;
            using (var cctx = handle.Acquire())
            {
                returnValue = Unsafe.Methods.ZSTD_compress2(
                    cctx,
                    destPtr,
                    (nuint)dest.Length,
                    srcPtr,
                    (nuint)src.Length
                );
            }

            if (returnValue == unchecked(0 - (nuint)ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall))
            {
                written = default;
                return false;
            }

            returnValue.EnsureZstdSuccess();
            written = (int)returnValue;
            return true;
        }
    }

    public bool TryWrap(ArraySegment<byte> src, ArraySegment<byte> dest, out int written) =>
        TryWrap((ReadOnlySpan<byte>)src, dest, out written);

    public bool TryWrap(
        byte[] src,
        int srcOffset,
        int srcLength,
        byte[] dst,
        int dstOffset,
        int dstLength,
        out int written
    ) =>
        TryWrap(
            new ReadOnlySpan<byte>(src, srcOffset, srcLength),
            new Span<byte>(dst, dstOffset, dstLength),
            out written
        );

    public void Dispose()
    {
        handle.Dispose();
        GC.SuppressFinalize(this);
    }

    internal nuint CompressStream(
        ref ZSTD_inBuffer_s input,
        ref ZSTD_outBuffer_s output,
        ZSTD_EndDirective directive
    )
    {
        fixed (ZSTD_inBuffer_s* inputPtr = &input)
        fixed (ZSTD_outBuffer_s* outputPtr = &output)
        {
            using var cctx = handle.Acquire();
            return Unsafe
                .Methods.ZSTD_compressStream2(cctx, outputPtr, inputPtr, directive)
                .EnsureZstdSuccess();
        }
    }

    public void SetPledgedSrcSize(ulong pledgedSrcSize)
    {
        using var cctx = handle.Acquire();
        Unsafe.Methods.ZSTD_CCtx_setPledgedSrcSize(cctx, pledgedSrcSize).EnsureZstdSuccess();
    }
}
