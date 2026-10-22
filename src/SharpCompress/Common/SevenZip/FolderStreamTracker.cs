using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

// Observes a borrowed decoder: the database, not this wrapper or an entry, owns its lifetime.
internal sealed class FolderStreamTracker(Stream stream) : Stream, IStreamStack
{
    private long _position;

    internal bool? IsAsync { get; private set; }
    internal bool IsFaulted { get; private set; }

    Stream IStreamStack.BaseStream() => stream;

    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => stream.Length;
    public override long Position
    {
        get => _position;
        set => _position = stream.Position = value;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        try
        {
            if (count > 0)
            {
                IsAsync = false;
            }
            var read = stream.Read(buffer, offset, count);
            _position += read;
            return read;
        }
        catch
        {
            IsFaulted = true;
            throw;
        }
    }

    public override int ReadByte()
    {
        try
        {
            IsAsync = false;
            var value = stream.ReadByte();
            if (value >= 0)
            {
                _position++;
            }
            return value;
        }
        catch
        {
            IsFaulted = true;
            throw;
        }
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (count > 0)
            {
                IsAsync = true;
            }
            var read = await stream
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            _position += read;
            return read;
        }
        catch
        {
            // A decoder may advance before throwing, including on cancellation. Its position and
            // internal state are no longer safe to reuse, even if no bytes reached the caller.
            IsFaulted = true;
            throw;
        }
    }

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer)
    {
        try
        {
            if (!buffer.IsEmpty)
            {
                IsAsync = false;
            }
            var read = stream.Read(buffer);
            _position += read;
            return read;
        }
        catch
        {
            IsFaulted = true;
            throw;
        }
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!buffer.IsEmpty)
            {
                IsAsync = true;
            }
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += read;
            return read;
        }
        catch
        {
            IsFaulted = true;
            throw;
        }
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) =>
        _position = stream.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
