using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

/// <summary>
/// Exposes the remainder of a seekable stream as an archive that begins at position zero.
/// </summary>
internal sealed class ArchiveOffsetStream : Stream
{
    private readonly Stream stream;
    private readonly long origin;

    public ArchiveOffsetStream(Stream stream)
    {
        this.stream = stream;
        origin = stream.Position;
    }

    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => stream.Length - origin;

    public override long Position
    {
        get => stream.Position - origin;
        set => stream.Position = origin + value;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer) => stream.Read(buffer);
#endif

    public override int ReadByte() => stream.ReadByte();

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => stream.ReadAsync(buffer, offset, count, cancellationToken);

#if !NET48 && !NETSTANDARD2_0
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => stream.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin seekOrigin)
    {
        var position = seekOrigin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(seekOrigin)),
        };

        if (position < 0)
        {
            throw new IOException("Attempted to seek before the archive origin.");
        }

        stream.Seek(origin + position, SeekOrigin.Begin);
        return position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        // The caller owns the source stream.
        base.Dispose(disposing);
    }
}
