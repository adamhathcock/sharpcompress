using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Test.Mocks;

public class AsyncOnlyStream : Stream
{
    private readonly Stream _stream;

    public AsyncOnlyStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(AsyncOnlyStream));
#endif
    }

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

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Synchronous Read is not supported");

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

#if NET8_0_OR_GREATER
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    public override void SetLength(long value) => _stream.SetLength(value);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => _stream.WriteAsync(buffer, offset, count, cancellationToken);

#if NET8_0_OR_GREATER
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => _stream.WriteAsync(buffer, cancellationToken);
#endif

    public override void Write(byte[] buffer, int offset, int count) =>
        _stream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(AsyncOnlyStream));
#endif
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}
