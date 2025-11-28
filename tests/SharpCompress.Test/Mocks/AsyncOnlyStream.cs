using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Test.Mocks;

/// <summary>
/// A stream wrapper that only allows async read operations.
/// Throws InvalidOperationException on synchronous Read calls to ensure
/// async code paths are being used.
/// </summary>
public class AsyncOnlyStream : Stream
{
    private readonly Stream _stream;
    private bool _isDisposed;

    public AsyncOnlyStream(Stream stream, bool throwOnSyncMethods = true)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        ThrowOnSyncMethods = throwOnSyncMethods;
    }

    public bool ThrowOnSyncMethods { get; set; }

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush() => _stream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _stream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (ThrowOnSyncMethods)
        {
            throw new InvalidOperationException(
                "Synchronous Read is not allowed on AsyncOnlyStream. Use ReadAsync instead."
            );
        }
        return _stream.Read(buffer, offset, count);
    }

    public override int ReadByte()
    {
        if (ThrowOnSyncMethods)
        {
            throw new InvalidOperationException(
                "Synchronous ReadByte is not allowed on AsyncOnlyStream. Use ReadAsync instead."
            );
        }
        return _stream.ReadByte();
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, cancellationToken);

    public override int Read(Span<byte> buffer)
    {
        if (ThrowOnSyncMethods)
        {
            throw new InvalidOperationException(
                "Synchronous Read is not allowed on AsyncOnlyStream. Use ReadAsync instead."
            );
        }
        return _stream.Read(buffer);
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    public override void SetLength(long value) => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (ThrowOnSyncMethods)
        {
            throw new InvalidOperationException(
                "Synchronous Write is not allowed on AsyncOnlyStream. Use WriteAsync instead."
            );
        }
        _stream.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        if (ThrowOnSyncMethods)
        {
            throw new InvalidOperationException(
                "Synchronous WriteByte is not allowed on AsyncOnlyStream. Use WriteAsync instead."
            );
        }
        _stream.WriteByte(value);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _stream.WriteAsync(buffer, offset, count, cancellationToken);

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.WriteAsync(buffer, cancellationToken);

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (ThrowOnSyncMethods)
        {
            throw new InvalidOperationException(
                "Synchronous Write is not allowed on AsyncOnlyStream. Use WriteAsync instead."
            );
        }
        _stream.Write(buffer);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (disposing)
            {
                _stream.Dispose();
            }
        }
        base.Dispose(disposing);
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif
}
