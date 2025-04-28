using System;
using System.IO;

namespace SharpCompress.Test.Mocks;

public class ForwardOnlyStream(Stream stream) : Stream
{
    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
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
