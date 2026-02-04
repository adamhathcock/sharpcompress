using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

internal sealed partial class SeekableRewindableStream
{
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _underlyingStream.ReadAsync(buffer, offset, count, cancellationToken);

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _underlyingStream.ReadAsync(buffer, cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _underlyingStream.WriteAsync(buffer, cancellationToken);

    public override ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return base.DisposeAsync();
        }
        if (ThrowOnDispose)
        {
            throw new InvalidOperationException(
                $"Attempt to dispose of a {nameof(SeekableRewindableStream)} when {nameof(ThrowOnDispose)} is true"
            );
        }
        _isDisposed = true;
        if (!LeaveStreamOpen)
        {
            _underlyingStream.Dispose();
        }
        return base.DisposeAsync();
    }
#endif

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _underlyingStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _underlyingStream.FlushAsync(cancellationToken);

    public override Task CopyToAsync(
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken
    ) => _underlyingStream.CopyToAsync(destination, bufferSize, cancellationToken);
}
