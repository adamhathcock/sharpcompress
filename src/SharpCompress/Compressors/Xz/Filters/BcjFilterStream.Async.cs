using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz.Filters;

internal sealed partial class BcjFilterStream
{
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => ReadCoreAsync(buffer, offset, count, cancellationToken).AsTask();

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    internal async ValueTask<int> ReadCoreAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ValidateRead(buffer, offset, count);
        cancellationToken.ThrowIfCancellationRequested();

        var total = 0;
        // XZBlock treats a short filter read as EOF, so fill the request unless input really ends.
        while (total < count)
        {
            if (_position == _filtered)
            {
                if (_endReached)
                {
                    break;
                }
                await FillAsync(cancellationToken).ConfigureAwait(false);
            }
            total += CopyBuffered(buffer.AsSpan(offset + total, count - total));
        }
        return total;
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        KeepUnfilteredTail();
        while (_filtered == 0 && !_endReached)
        {
            var bytesRead = await BaseStream
                .ReadAsync(_buffer, _size, BufferSize - _size, cancellationToken)
                .ConfigureAwait(false);
            ConvertBuffered(bytesRead);
        }
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var total = 0;
        while (total < buffer.Length)
        {
            if (_position == _filtered)
            {
                if (_endReached)
                {
                    break;
                }
                await FillAsync(cancellationToken).ConfigureAwait(false);
            }
            total += CopyBuffered(buffer.Span.Slice(total));
        }
        return total;
    }
#endif
}
