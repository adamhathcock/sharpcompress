using System;
using System.IO;

namespace SharpCompress.Test.Mocks;

/// <summary>
/// A stream wrapper that truncates the underlying stream after reading a specified number of bytes.
/// Used for testing error handling when streams end prematurely.
/// </summary>
public class TruncatedStream : Stream
{
    private readonly Stream baseStream;
    private readonly long truncateAfterBytes;
    private long bytesRead;

    public TruncatedStream(Stream baseStream, long truncateAfterBytes)
    {
        this.baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        this.truncateAfterBytes = truncateAfterBytes;
        bytesRead = 0;
    }

    public override bool CanRead => baseStream.CanRead;
    public override bool CanSeek => baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => baseStream.Length;

    public override long Position
    {
        get => baseStream.Position;
        set => baseStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (bytesRead >= truncateAfterBytes)
        {
            // Simulate premature end of stream
            return 0;
        }

        var maxBytesToRead = (int)Math.Min(count, truncateAfterBytes - bytesRead);
        var actualBytesRead = baseStream.Read(buffer, offset, maxBytesToRead);
        bytesRead += actualBytesRead;
        return actualBytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override void Flush() => baseStream.Flush();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            baseStream?.Dispose();
        }
        base.Dispose(disposing);
    }
}
