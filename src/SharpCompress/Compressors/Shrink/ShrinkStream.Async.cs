using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Shrink;

internal partial class ShrinkStream : Stream
{
    internal static async ValueTask<ShrinkStream> CreateAsync(
        Stream stream,
        CompressionMode compressionMode,
        long compressedSize,
        long uncompressedSize,
        CancellationToken cancellationToken = default
    )
    {
        var shrinkStream = new ShrinkStream(
            stream,
            compressionMode,
            compressedSize,
            uncompressedSize
        );
        await shrinkStream.DecompressAsync(cancellationToken).ConfigureAwait(false);
        return shrinkStream;
    }

    private async Task DecompressAsync(CancellationToken cancellationToken)
    {
        if (_decompressed)
        {
            return;
        }

        // Read actual compressed data from stream rather than pre-allocating based on the
        // declared compressed size, which may be crafted to cause an OutOfMemoryException.
        // The stream is already bounded by ReadOnlySubStream in ZipFilePart.
        using var srcMs = new MemoryStream();
        await inStream.CopyToAsync(srcMs, cancellationToken).ConfigureAwait(false);
        var src = srcMs.ToArray();
        var srcLen = src.Length;

        // Decompress synchronously (CPU-bound operation)
        var srcUsed = 0;
        var dstUsed = 0;

        HwUnshrink.Unshrink(
            src,
            srcLen,
            out srcUsed,
            _byteOut,
            (int)_uncompressedSize,
            out dstUsed
        );
        _outBytesCount = dstUsed;
        _decompressed = true;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_decompressed)
        {
            await DecompressAsync(cancellationToken).ConfigureAwait(false);
        }

        // Copy from decompressed buffer
        long remaining = _outBytesCount - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        int toCopy = (int)Math.Min(count, remaining);
        Buffer.BlockCopy(_byteOut, (int)_position, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_decompressed)
        {
            await DecompressAsync(cancellationToken).ConfigureAwait(false);
        }

        if (buffer.IsEmpty)
        {
            return 0;
        }

        long remaining = _outBytesCount - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        int toCopy = (int)Math.Min(buffer.Length, remaining);
        _byteOut.AsMemory((int)_position, toCopy).CopyTo(buffer);
        _position += toCopy;
        return toCopy;
    }
#endif
}
