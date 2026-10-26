#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public sealed partial class XZStream
{
    public static async ValueTask<bool> IsXZStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return null
                != await XZHeader.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Asynchronously reads bytes from the current stream into a buffer.
    /// </summary>
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        var bytesRead = 0;
        if (_endOfStream)
        {
            return bytesRead;
        }

        if (!HeaderIsRead)
        {
            await ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
        }

        bytesRead = await ReadBlocksAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead < count)
        {
            _endOfStream = true;
            await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            await ReadFooterAsync(cancellationToken).ConfigureAwait(false);
        }
        return bytesRead;
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Read and validate XZ data without adapting memory reads through Task-based array overloads.
    /// </summary>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _endOfStream)
        {
            return 0;
        }
        if (!HeaderIsRead)
        {
            await ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
        }
        if (
            _currentBlock is not null
            && _currentBlock.TryReadBuffered(buffer, cancellationToken, out var buffered)
        )
        {
            return buffered;
        }
        var bytesRead = await ReadBlocksAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead < buffer.Length)
        {
            _endOfStream = true;
            await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            await ReadFooterAsync(cancellationToken).ConfigureAwait(false);
        }
        return bytesRead;
    }

    private async ValueTask<int> ReadBlocksAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        var bytesRead = 0;
        if (_currentBlock is null)
        {
            NextBlock();
        }
        while (bytesRead < buffer.Length)
        {
            try
            {
                var remaining = buffer.Slice(bytesRead);
                var justRead = await _currentBlock
                    .ReadAsync(remaining, cancellationToken)
                    .ConfigureAwait(false);
                bytesRead += justRead;
                if (justRead < remaining.Length)
                {
                    NextBlock();
                }
            }
            catch (XZIndexMarkerReachedException)
            {
                break;
            }
        }
        return bytesRead;
    }
#endif

    /// <summary>
    /// Asynchronously reads and validates the XZ header.
    /// </summary>
    private async ValueTask ReadHeaderAsync(CancellationToken cancellationToken = default)
    {
        Header = await XZHeader
            .FromStreamAsync(BaseStream, cancellationToken)
            .ConfigureAwait(false);
        AssertBlockCheckTypeIsSupported();
        HeaderIsRead = true;
    }

    /// <summary>
    /// Asynchronously reads the XZ index.
    /// </summary>
    private async ValueTask ReadIndexAsync(CancellationToken cancellationToken = default) =>
        Index = await XZIndex
            .FromStreamAsync(BaseStream, true, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously reads the XZ footer.
    /// </summary>
    private async ValueTask ReadFooterAsync(CancellationToken cancellationToken = default) =>
        Footer = await XZFooter
            .FromStreamAsync(BaseStream, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Asynchronously reads blocks of data from the stream.
    /// </summary>
    private async ValueTask<int> ReadBlocksAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        var bytesRead = 0;
        if (_currentBlock is null)
        {
            NextBlock();
        }

        for (; ; )
        {
            try
            {
                if (bytesRead >= count)
                {
                    break;
                }

                var remaining = count - bytesRead;
                var newOffset = offset + bytesRead;
                var justRead = await _currentBlock
                    .ReadAsync(buffer, newOffset, remaining, cancellationToken)
                    .ConfigureAwait(false);
                if (justRead < remaining)
                {
                    NextBlock();
                }

                bytesRead += justRead;
            }
            catch (XZIndexMarkerReachedException)
            {
                break;
            }
        }
        return bytesRead;
    }
}
