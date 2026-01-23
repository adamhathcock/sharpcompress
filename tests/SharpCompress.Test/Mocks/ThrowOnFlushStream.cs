using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Test.Mocks;

/// <summary>
/// A stream wrapper that throws NotSupportedException on Flush() calls.
/// This is used to test that archive iteration handles streams that don't support flushing.
/// </summary>
public class ThrowOnFlushStream : Stream
{
    private readonly Stream inner;

    public ThrowOnFlushStream(Stream inner)
    {
        this.inner = inner;
    }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException("Flush not supported");

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("FlushAsync not supported");

    public override int Read(byte[] buffer, int offset, int count) =>
        inner.Read(buffer, offset, count);

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => inner.ReadAsync(buffer, offset, count, cancellationToken);

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => inner.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
