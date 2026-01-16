using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Test.Mocks;

public class AsyncOnlyStream : SharpCompressStream
{
    public AsyncOnlyStream(Stream stream)
        : base(stream)
    {
        // Console.WriteLine("AsyncOnlyStream created");
    }

    public override bool CanRead => Stream.CanRead;
    public override bool CanSeek => Stream.CanSeek;
    public override bool CanWrite => Stream.CanWrite;
    public override long Length => Stream.Length;
    public override long Position
    {
        get => Stream.Position;
        set => Stream.Position = value;
    }

    public override void Flush() => Stream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Synchronous Read is not supported");

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => Stream.ReadAsync(buffer, offset, count, cancellationToken);

#if NET8_0_OR_GREATER
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => Stream.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin) => Stream.Seek(offset, origin);

    public override void SetLength(long value) => Stream.SetLength(value);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException("Synchronous Read is not supported");

#if NET8_0_OR_GREATER
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => Stream.WriteAsync(buffer, cancellationToken);
#endif

    public override void Write(byte[] buffer, int offset, int count) =>
        Stream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
