using System;
using System.IO;
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

    void IStreamStack.SetPostion(long position) { }

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
    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => stream.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
