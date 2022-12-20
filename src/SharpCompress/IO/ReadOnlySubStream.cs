using System;
using System.IO;

namespace SharpCompress.IO;

internal class ReadOnlySubStream : NonDisposingStream
{
    public ReadOnlySubStream(Stream stream, long bytesToRead) : this(stream, null, bytesToRead) { }

    public ReadOnlySubStream(Stream stream, long? origin, long bytesToRead)
        : base(stream, throwOnDispose: false)
    {
        if (origin != null)
        {
            stream.Position = origin.Value;
        }
        BytesLeftToRead = bytesToRead;
    }

    private long BytesLeftToRead { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() => throw new NotSupportedException();

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (BytesLeftToRead < count)
        {
            count = (int)BytesLeftToRead;
        }
        var read = Stream.Read(buffer, offset, count);
        if (read > 0)
        {
            BytesLeftToRead -= read;
        }
        return read;
    }

    public override int ReadByte()
    {
        if (BytesLeftToRead <= 0)
        {
            return -1;
        }
        var value = Stream.ReadByte();
        if (value != -1)
        {
            --BytesLeftToRead;
        }
        return value;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override int Read(Span<byte> buffer)
    {
        var slice_len = BytesLeftToRead < buffer.Length ? BytesLeftToRead : buffer.Length;
        var read = Stream.Read(buffer.Slice(0, (int)slice_len));
        if (read > 0)
        {
            BytesLeftToRead -= read;
        }
        return read;
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
