using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate64;

public sealed partial class Deflate64Stream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ValidateParameters(buffer, offset, count);
        EnsureNotDisposed();

        int bytesRead;
        var currentOffset = offset;
        var remainingCount = count;

        while (true)
        {
            bytesRead = _inflater.Inflate(buffer, currentOffset, remainingCount);
            currentOffset += bytesRead;
            remainingCount -= bytesRead;

            if (remainingCount == 0)
            {
                break;
            }

            if (_inflater.Finished())
            {
                // if we finished decompressing, we can't have anything left in the outputwindow.
                break;
            }

            var bytes = await _stream
                .ReadAsync(_buffer, 0, _buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            if (bytes <= 0)
            {
                break;
            }
            else if (bytes > _buffer.Length)
            {
                // The stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the buffer supplied to it.
                throw new InvalidFormatException("Deflate64: invalid data");
            }

            _inflater.SetInput(_buffer, 0, bytes);
        }

        return count - remainingCount;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        EnsureNotDisposed();

        // InflaterManaged doesn't have a Span-based Inflate method, so we need to work with arrays
        // For large buffers, we could rent from ArrayPool, but for simplicity we'll use the buffer's array if available
        if (
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>(
                buffer,
                out var arraySegment
            )
        )
        {
            // Fast path: the Memory<byte> is backed by an array
            return await ReadAsync(
                    arraySegment.Array!,
                    arraySegment.Offset,
                    arraySegment.Count,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            // Slow path: rent a temporary array
            var tempBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                var bytesRead = await ReadAsync(tempBuffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false);
                tempBuffer.AsMemory(0, bytesRead).CopyTo(buffer);
                return bytesRead;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }
    }
#endif
}
