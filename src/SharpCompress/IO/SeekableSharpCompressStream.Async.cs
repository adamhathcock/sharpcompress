using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

internal sealed partial class SeekableSharpCompressStream
{
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.WriteAsync(buffer, cancellationToken);

    public override ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return base.DisposeAsync();
        }
        if (ThrowOnDispose)
        {
            throw new ArchiveOperationException(
                $"Attempt to dispose of a {nameof(SeekableSharpCompressStream)} when {nameof(ThrowOnDispose)} is true"
            );
        }
        _isDisposed = true;
        if (!LeaveStreamOpen)
        {
            _stream.Dispose();
        }
        return base.DisposeAsync();
    }
#endif

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _stream.WriteAsync(buffer, offset, count, cancellationToken);

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _stream.FlushAsync(cancellationToken);

    public override Task CopyToAsync(
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken
    ) => _stream.CopyToAsync(destination, bufferSize, cancellationToken);
}
