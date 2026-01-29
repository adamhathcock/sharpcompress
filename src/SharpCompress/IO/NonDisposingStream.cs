using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

/// <summary>
/// A stream wrapper that prevents disposal of the underlying stream.
/// This is useful when working with compression streams directly and you want
/// to keep the base stream open after the compression stream is disposed.
/// </summary>
internal class NonDisposingStream : Stream
{
    private readonly Stream _stream;
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception when the stream is disposed.
    /// This is useful for testing to ensure streams are not disposed prematurely.
    /// </summary>
    public bool ThrowOnDispose { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NonDisposingStream"/> class.
    /// </summary>
    /// <param name="stream">The stream to wrap. This stream will NOT be disposed when this wrapper is disposed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    public NonDisposingStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public override bool CanRead => !_isDisposed && _stream.CanRead;

    public override bool CanSeek =>
        !_isDisposed && _stream.CanSeek && _stream is not RewindableStream;

    public override bool CanWrite => !_isDisposed && _stream.CanWrite;

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _stream.Length;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _stream.Position;
        }
        set
        {
            ThrowIfDisposed();
            _stream.Position = value;
        }
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

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        return _stream.Read(buffer);
    }
#endif

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        return _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        _stream.Write(buffer, offset, count);
    }

#if !LEGACY_DOTNET
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();
        _stream.Write(buffer);
    }
#endif

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

    /// <summary>
    /// Disposes this wrapper without disposing the underlying stream.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException(
                    $"Attempt to dispose of a {nameof(NonDisposingStream)} when {nameof(ThrowOnDispose)} is true"
                );
            }
            _isDisposed = true;
            // Intentionally do NOT dispose _stream
        }
        base.Dispose(disposing);
    }

#if !LEGACY_DOTNET
    /// <summary>
    /// Asynchronously disposes this wrapper without disposing the underlying stream.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException(
                    $"Attempt to dispose of a {nameof(NonDisposingStream)} when {nameof(ThrowOnDispose)} is true"
                );
            }
            _isDisposed = true;
            // Intentionally do NOT dispose _stream
        }
        await base.DisposeAsync();
    }
#endif

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(NonDisposingStream));
        }
    }
}
