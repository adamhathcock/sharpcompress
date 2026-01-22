using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate;

public partial class DeflateStream
{
#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (!_leaveOpen)
            {
                await _baseStream.DisposeAsync().ConfigureAwait(false);
            }
            _disposed = true;
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }
        await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }
        return await _baseStream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }
        return await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }
        await _baseStream
            .WriteAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }
        await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif
}
