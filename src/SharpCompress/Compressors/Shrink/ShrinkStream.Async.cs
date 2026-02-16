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

        // Read all compressed data asynchronously
        var src = new byte[_compressedSize];
        int bytesRead = 0;
        int totalBytesRead = 0;

        while (totalBytesRead < (int)_compressedSize)
        {
            bytesRead = await inStream
                .ReadAsync(
                    src,
                    totalBytesRead,
                    (int)_compressedSize - totalBytesRead,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new IncompleteArchiveException(
                    "Unexpected end of stream while reading compressed data"
                );
            }
            totalBytesRead += bytesRead;
        }

        // Decompress synchronously (CPU-bound operation)
        var srcUsed = 0;
        var dstUsed = 0;

        HwUnshrink.Unshrink(
            src,
            (int)_compressedSize,
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
