using System.IO;

namespace SharpCompress.Test.Mocks;

public class TestStream : Stream
{
    private readonly Stream _stream;

    public TestStream(Stream stream)
        : this(stream, stream.CanRead, stream.CanWrite, stream.CanSeek) { }

    public bool IsDisposed { get; private set; }

    public TestStream(Stream stream, bool read, bool write, bool seek)
    {
        _stream = stream;
        CanRead = read;
        CanWrite = write;
        CanSeek = seek;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _stream.Dispose();
        IsDisposed = true;
    }

    public override bool CanRead { get; }

    public override bool CanSeek { get; }

    public override bool CanWrite { get; }

    public override void Flush() => _stream.Flush();

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        _stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    public override void SetLength(long value) => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        _stream.Write(buffer, offset, count);
}
