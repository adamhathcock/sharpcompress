using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

// Borrows memory for the lifetime of an encoder call; never copies or owns the backing storage.
internal sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) : Stream
{
    private ReadOnlyMemory<byte> _memory = memory;
    private int _position;
    private bool _disposed;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length =>
        !_disposed
            ? _memory.Length
            : throw new ObjectDisposedException(nameof(ReadOnlyMemoryStream));
    public override long Position
    {
        get =>
            !_disposed
                ? _position
                : throw new ObjectDisposedException(nameof(ReadOnlyMemoryStream));
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowHelper.ThrowIfNull(buffer, nameof(buffer));
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Invalid buffer range.");
        }
        return ReadCore(buffer.AsSpan(offset, count));
    }

    private int ReadCore(Span<byte> buffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadOnlyMemoryStream));
        }
        var count = Math.Min(buffer.Length, _memory.Length - _position);
        _memory.Span.Slice(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    public override int ReadByte()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReadOnlyMemoryStream));
        }
        return _position < _memory.Length ? _memory.Span[_position++] : -1;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) =>
        cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<int>(cancellationToken)
            : Task.FromResult(Read(buffer, offset, count));

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer) => ReadCore(buffer);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<int>(cancellationToken)
            : ValueTask.FromResult(ReadCore(buffer.Span));
#endif

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        _memory = default;
        base.Dispose(disposing);
    }
}
