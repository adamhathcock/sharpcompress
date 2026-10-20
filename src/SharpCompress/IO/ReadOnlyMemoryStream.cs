using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO;

// Borrows caller memory only until the awaited encoder operation completes.
internal sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) : Stream
{
    private int position;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => memory.Length;
    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    private int ReadMemory(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, memory.Length - position);
        memory.Span.Slice(position, count).CopyTo(buffer);
        position += count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadMemory(buffer.AsSpan(offset, count));

    public override int ReadByte() => position < memory.Length ? memory.Span[position++] : -1;

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read(buffer, offset, count));
    }

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer) => ReadMemory(buffer);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<int>(ReadMemory(buffer.Span));
    }
#endif

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
