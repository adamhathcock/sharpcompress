using System;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public unsafe class Decompressor : IDisposable
{
    private readonly SafeDctxHandle handle;

    public Decompressor()
    {
        handle = SafeDctxHandle.Create();
    }

    public void SetParameter(ZSTD_dParameter parameter, int value)
    {
        using var dctx = handle.Acquire();
        Unsafe.Methods.ZSTD_DCtx_setParameter(dctx, parameter, value).EnsureZstdSuccess();
    }

    public int GetParameter(ZSTD_dParameter parameter)
    {
        using var dctx = handle.Acquire();
        int value;
        Unsafe.Methods.ZSTD_DCtx_getParameter(dctx, parameter, &value).EnsureZstdSuccess();
        return value;
    }

    public void LoadDictionary(byte[] dict)
    {
        var dictReadOnlySpan = new ReadOnlySpan<byte>(dict);
        this.LoadDictionary(dictReadOnlySpan);
    }

    public void LoadDictionary(ReadOnlySpan<byte> dict)
    {
        using var dctx = handle.Acquire();
        fixed (byte* dictPtr = dict)
            Unsafe
                .Methods.ZSTD_DCtx_loadDictionary(dctx, dictPtr, (nuint)dict.Length)
                .EnsureZstdSuccess();
    }

    public static ulong GetDecompressedSize(ReadOnlySpan<byte> src)
    {
        fixed (byte* srcPtr = src)
            return Unsafe
                .Methods.ZSTD_decompressBound(srcPtr, (nuint)src.Length)
                .EnsureContentSizeOk();
    }

    public static ulong GetDecompressedSize(ArraySegment<byte> src) =>
        GetDecompressedSize((ReadOnlySpan<byte>)src);

    public static ulong GetDecompressedSize(byte[] src, int srcOffset, int srcLength) =>
        GetDecompressedSize(new ReadOnlySpan<byte>(src, srcOffset, srcLength));

    public Span<byte> Unwrap(ReadOnlySpan<byte> src, int maxDecompressedSize = int.MaxValue)
    {
        var expectedDstSize = GetDecompressedSize(src);
        if (expectedDstSize > (ulong)maxDecompressedSize)
            throw new ZstdException(
                ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall,
                $"Decompressed content size {expectedDstSize} is greater than {nameof(maxDecompressedSize)} {maxDecompressedSize}"
            );
        if (expectedDstSize > Constants.MaxByteArrayLength)
            throw new ZstdException(
                ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall,
                $"Decompressed content size {expectedDstSize} is greater than max possible byte array size {Constants.MaxByteArrayLength}"
            );

        var dest = new byte[expectedDstSize];
        var length = Unwrap(src, dest);
        return new Span<byte>(dest, 0, length);
    }

    public int Unwrap(byte[] src, byte[] dest, int offset) =>
        Unwrap(src, new Span<byte>(dest, offset, dest.Length - offset));

    public int Unwrap(ReadOnlySpan<byte> src, Span<byte> dest)
    {
        fixed (byte* srcPtr = src)
        fixed (byte* destPtr = dest)
        {
            using var dctx = handle.Acquire();
            return (int)
                Unsafe
                    .Methods.ZSTD_decompressDCtx(
                        dctx,
                        destPtr,
                        (nuint)dest.Length,
                        srcPtr,
                        (nuint)src.Length
                    )
                    .EnsureZstdSuccess();
        }
    }

    public int Unwrap(
        byte[] src,
        int srcOffset,
        int srcLength,
        byte[] dst,
        int dstOffset,
        int dstLength
    ) =>
        Unwrap(
            new ReadOnlySpan<byte>(src, srcOffset, srcLength),
            new Span<byte>(dst, dstOffset, dstLength)
        );

    public bool TryUnwrap(byte[] src, byte[] dest, int offset, out int written) =>
        TryUnwrap(src, new Span<byte>(dest, offset, dest.Length - offset), out written);

    public bool TryUnwrap(ReadOnlySpan<byte> src, Span<byte> dest, out int written)
    {
        fixed (byte* srcPtr = src)
        fixed (byte* destPtr = dest)
        {
            nuint returnValue;
            using (var dctx = handle.Acquire())
            {
                returnValue = Unsafe.Methods.ZSTD_decompressDCtx(
                    dctx,
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

    public bool TryUnwrap(
        byte[] src,
        int srcOffset,
        int srcLength,
        byte[] dst,
        int dstOffset,
        int dstLength,
        out int written
    ) =>
        TryUnwrap(
            new ReadOnlySpan<byte>(src, srcOffset, srcLength),
            new Span<byte>(dst, dstOffset, dstLength),
            out written
        );

    public void Dispose()
    {
        handle.Dispose();
        GC.SuppressFinalize(this);
    }

    internal nuint DecompressStream(ref ZSTD_inBuffer_s input, ref ZSTD_outBuffer_s output)
    {
        fixed (ZSTD_inBuffer_s* inputPtr = &input)
        fixed (ZSTD_outBuffer_s* outputPtr = &output)
        {
            using var dctx = handle.Acquire();
            return Unsafe
                .Methods.ZSTD_decompressStream(dctx, outputPtr, inputPtr)
                .EnsureZstdSuccess();
        }
    }
}
