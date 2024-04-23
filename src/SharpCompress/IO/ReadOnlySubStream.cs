using System;
using System.IO;

namespace SharpCompress.IO;

internal class ReadOnlySubStream : NonDisposingStream
{
    private long _position;

    public ReadOnlySubStream(Stream stream, long bytesToRead)
        : this(stream, null, bytesToRead) { }

    public ReadOnlySubStream(Stream stream, long? origin, long bytesToRead)
        : base(stream, throwOnDispose: false)
    {
        if (origin != null)
        {
            stream.Position = origin.Value;
        }
        BytesLeftToRead = bytesToRead;
        _position = 0;
    }

    private long BytesLeftToRead { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position; //allow position to be read (XZ uses this to calculate alignment)
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
            _position += read;
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
            _position++;
        }
        return value;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override int Read(Span<byte> buffer)
    {
        var sliceLen = BytesLeftToRead < buffer.Length ? BytesLeftToRead : buffer.Length;
        var read = Stream.Read(buffer.Slice(0, (int)sliceLen));
        if (read > 0)
        {
            BytesLeftToRead -= read;
            _position += read;
        }
        return read;
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
