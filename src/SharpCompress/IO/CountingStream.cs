using System;
using System.IO;

namespace SharpCompress.IO;

/// <summary>
/// A simple stream wrapper that counts bytes written without buffering.
/// </summary>
internal class CountingStream : Stream
{
    private readonly Stream _stream;
    private long _bytesWritten;

    public CountingStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Gets the total number of bytes written to this stream.
    /// </summary>
    public long BytesWritten => _bytesWritten;

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
        _stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

    public override void SetLength(long value) => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
        _bytesWritten += count;
    }

    public override void WriteByte(byte value)
    {
        _stream.WriteByte(value);
        _bytesWritten++;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}
