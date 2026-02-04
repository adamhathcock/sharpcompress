using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Test.Mocks;

/// <summary>
/// A forward-only stream wrapper that delegates directly to the underlying stream
/// without any buffering. Supports reading and writing but not seeking.
/// </summary>
public class ForwardOnlyStream : Stream
{
    private readonly Stream _stream;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardOnlyStream"/> class.
    /// </summary>
    /// <param name="stream">The underlying stream to wrap.</param>
    /// <param name="bufferSize">Buffer size parameter (ignored - this implementation does not buffer).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    public ForwardOnlyStream(Stream stream, int? bufferSize = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        // bufferSize is ignored - this implementation does not buffer
    }

    public override bool CanRead => !_isDisposed && _stream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => !_isDisposed && _stream.CanWrite;

    public override long Length
    {
        get => throw new NotSupportedException("Length is not supported on a forward-only stream.");
    }

    public override long Position
    {
        get =>
            throw new NotSupportedException("Position is not supported on a forward-only stream.");
        set =>
            throw new NotSupportedException("Position is not supported on a forward-only stream.");
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        _stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        return _stream.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ThrowIfDisposed();
        return _stream.ReadAsync(buffer, offset, count, cancellationToken);
    }

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        return _stream.ReadAsync(buffer, cancellationToken);
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("Seek is not supported on a forward-only stream.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("SetLength is not supported on a forward-only stream.");

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        _stream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        ThrowIfDisposed();
        return _stream.WriteAsync(buffer, offset, count, cancellationToken);
    }

#if !LEGACY_DOTNET
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ThrowIfDisposed();
        return _stream.WriteAsync(buffer, cancellationToken);
    }
#endif

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _stream.FlushAsync(cancellationToken);
    }

    public override Task CopyToAsync(
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken
    )
    {
        ThrowIfDisposed();
        return _stream.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
            _isDisposed = true;
            base.Dispose(disposing);
        }
    }

#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            await _stream.DisposeAsync();
            _isDisposed = true;
        }
        await base.DisposeAsync();
    }
#endif

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ForwardOnlyStream));
        }
    }
}
