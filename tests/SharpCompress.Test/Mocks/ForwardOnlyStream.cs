using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Test.Mocks;

public class ForwardOnlyStream : SharpCompressStream, IStreamStack
{
    private readonly Stream stream;
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif

    Stream IStreamStack.BaseStream() => stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    public bool IsDisposed { get; private set; }

    public ForwardOnlyStream(Stream stream, int bufferSize = ReaderOptions.DefaultBufferSize)
        : base(stream, bufferSize: bufferSize)
    {
        this.stream = stream;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(ForwardOnlyStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
#if DEBUG_STREAMS
                this.DebugDispose(typeof(ForwardOnlyStream));
#endif
                stream.Dispose();
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => stream.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => stream.ReadAsync(buffer, offset, count, cancellationToken);

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => stream.ReadAsync(buffer, cancellationToken);
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        stream.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => stream.WriteAsync(buffer, offset, count, cancellationToken);

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => stream.WriteAsync(buffer, cancellationToken);
#endif

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        stream.FlushAsync(cancellationToken);
}
