#nullable disable

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Rar;

internal partial class RarStream
{
    /// <summary>
    /// Asynchronously initializes the RAR stream for reading.
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        fetch = true;
        await unpack
            .DoUnpackAsync(fileHeader, readStream, this, cancellationToken)
            .ConfigureAwait(false);
        fetch = false;
        _position = 0;
    }

    /// <summary>
    /// Asynchronously reads bytes from the current stream into a buffer.
    /// </summary>
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => await ReadImplAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Internal async implementation of ReadAsync.
    /// </summary>
    private async Task<int> ReadImplAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        outTotal = 0;
        if (tmpCount > 0)
        {
            var toCopy = tmpCount < count ? tmpCount : count;
            Buffer.BlockCopy(tmpBuffer, tmpOffset, buffer, offset, toCopy);
            tmpOffset += toCopy;
            tmpCount -= toCopy;
            offset += toCopy;
            count -= toCopy;
            outTotal += toCopy;
        }
        if (count > 0 && unpack.DestSize > 0)
        {
            outBuffer = buffer;
            outOffset = offset;
            outCount = count;
            fetch = true;
            await unpack.DoUnpackAsync(cancellationToken).ConfigureAwait(false);
            fetch = false;
        }
        _position += outTotal;
        if (count > 0 && outTotal == 0 && _position < Length)
        {
            // sanity check, eg if we try to decompress a redir entry
            throw new ArchiveOperationException(
                $"unpacked file size does not match header: expected {Length} found {_position}"
            );
        }
        return outTotal;
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Asynchronously reads bytes from the current stream into a memory buffer.
    /// </summary>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            var bytesRead = await ReadImplAsync(array, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            new ReadOnlySpan<byte>(array, 0, bytesRead).CopyTo(buffer.Span);
            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }
#endif

    /// <summary>
    /// Asynchronously writes bytes to the current stream.
    /// </summary>
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }
}
